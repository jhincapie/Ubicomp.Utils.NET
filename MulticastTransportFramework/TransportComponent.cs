#nullable enable
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// The central component for managing multicast transport communication.
    /// Handles message serialization, sequential processing via a gatekeeper,
    /// and routing messages to registered handlers.
    /// </summary>
    public class MessageErrorEventArgs : EventArgs
    {
        public SocketMessage RawMessage
        {
            get;
        }
        public Exception? Exception
        {
            get;
        }
        public string Reason
        {
            get;
        }

        public MessageErrorEventArgs(SocketMessage msg, string reason, Exception? ex = null)
        {
            RawMessage = msg;
            Reason = reason;
            Exception = ex;
        }
    }

    /// <summary>
    /// The central component for managing multicast transport communication.
    /// Handles message serialization, sequential processing via a gatekeeper,
    /// and routing messages to registered handlers.
    /// </summary>
    public class TransportComponent : IDisposable
    {
        /// <summary>Gets or sets the logger for this component.</summary>
        public ILogger Logger { get; set; } = NullLogger.Instance;

        /// <summary>The unique ID for the transport component itself.</summary>
        public const int TransportComponentID = 0;

        /// <summary>
        /// The message type ID used for acknowledgements.
        /// </summary>
        public const string AckMessageType = "sys.ack";

        private IMulticastSocket? _socket;
        private readonly MulticastSocketOptions _socketOptions;
        public MulticastSocketOptions Options => _socketOptions;

        public bool IsRunning => _processingLoopTask != null && !_processingLoopTask.IsCompleted;

        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;

        private readonly ConcurrentDictionary<string, Type> _knownTypes = new ConcurrentDictionary<string, Type>();
        private readonly ConcurrentDictionary<string, Delegate> _genericHandlers = new ConcurrentDictionary<string, Delegate>();
        private readonly ConcurrentDictionary<Type, string> _typeToIdMap = new ConcurrentDictionary<Type, string>();

        private readonly ConcurrentDictionary<Guid, AckSession> _activeSessions = new ConcurrentDictionary<Guid, AckSession>();

        // Replay Protection
        private readonly ConcurrentDictionary<Guid, ReplayWindow> _replayProtection = new ConcurrentDictionary<Guid, ReplayWindow>();

        // For sequence ordering (GateKeeper)
        private readonly ConcurrentDictionary<Guid, int> _expectedSequences = new ConcurrentDictionary<Guid, int>();

        private readonly JsonSerializerOptions _jsonOptions;

        // Lock-Free Actor Model Channels
        private Channel<GateCmd>? _gateInput;
        private Channel<SocketMessage>? _processingChannel;
        private Task? _gateLoopTask;
        private Task? _processingLoopTask;
        private Task? _heartbeatTask;
        private CancellationTokenSource? _heartbeatCts;

        // Rx & Tracing
        private static readonly ActivitySource _activitySource = new ActivitySource("Ubicomp.Utils.NET.Transport");
        private readonly Subject<SocketMessage> _messageSubject = new Subject<SocketMessage>();

        /// <summary>
        /// Gets an observable stream of all raw messages received by this component.
        /// </summary>
        public IObservable<SocketMessage> MessageStream => _messageSubject.AsObservable();

        private readonly PeerTable _peerTable = new PeerTable();

        /// <summary>
        /// Gets the collection of currently active peers discovered on the network.
        /// </summary>
        public IEnumerable<RemotePeer> ActivePeers => _peerTable.GetActivePeers();

        /// <summary>
        /// Event raised when a new peer is discovered.
        /// </summary>
        public event Action<RemotePeer> OnPeerDiscovered
        {
            add => _peerTable.OnPeerDiscovered += value;
            remove => _peerTable.OnPeerDiscovered -= value;
        }

        /// <summary>
        /// Event raised when a peer times out.
        /// </summary>
        public event Action<RemotePeer> OnPeerLost
        {
            add => _peerTable.OnPeerLost += value;
            remove => _peerTable.OnPeerLost -= value;
        }

        /// <summary>
        /// Event raised when a message fails to process (Dead Letter).
        /// </summary>
        public event EventHandler<MessageErrorEventArgs>? OnMessageError;

        private abstract class GateCmd
        {
        }
        private class InputMsgCmd : GateCmd
        {
            public SocketMessage Msg = null!;
        }
        private class TimeoutCmd : GateCmd
        {
            public int SeqId;
        }

        /// <summary>Gets or sets the default timeout for acknowledgements.</summary>
        public TimeSpan DefaultAckTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the window of time for which messages are considered valid.
        /// Messages older than this window (based on their timestamp) are discarded to prevent replay attacks.
        /// Defaults to 5 seconds.
        /// </summary>
        public TimeSpan ReplayWindow { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the timeout for the GateKeeper to wait for a specific sequence number.
        /// Defaults to 500ms.
        /// </summary>
        public TimeSpan GateKeeperTimeout { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Gets or sets the local source identification for this transport component.
        /// </summary>
        public EventSource LocalSource { get; set; } = new EventSource(Guid.NewGuid(), Environment.MachineName);

        /// <summary>
        /// Gets or sets a value indicating whether acknowledgements should be sent automatically
        /// when a message requesting one is received and handled.
        /// </summary>
        public bool AutoSendAcks { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to enforce strict message ordering via the GateKeeper.
        /// When true, messages are processed sequentially based on sequence IDs.
        /// When false (default), messages are processed immediately as they arrive.
        /// </summary>
        public bool EnforceOrdering { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum size of the pending message queue to prevent memory exhaustion.
        /// Defaults to 10000 messages.
        /// </summary>
        public int MaxQueueSize { get; set; } = 10000;

        /// <summary>
        /// Gets or sets the shared secret key for HMAC integrity and AES-GCM encryption.
        /// Can be any string (e.g. a passphrase).
        /// Setting this triggers key derivation for EncryptionKey and IntegrityKey.
        /// If null, messages are signed with simple SHA256 (integrity only, no authentication).
        /// </summary>
        public string? SecurityKey
        {
            get => _securityKey;
            set
            {
                _securityKey = value;
                UpdateKey();
            }
        }
        private string? _securityKey;
        private readonly KeyManager _keyManager;

        internal KeyManager KeyManager => _keyManager; // Testing access

        /// <summary>
        /// Gets or sets whether to enable payload encryption.
        /// Requires <see cref="SecurityKey"/> to be set.
        /// </summary>
        public bool EncryptionEnabled
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the interval for sending heartbeat messages.
        /// If null, heartbeats are disabled.
        /// </summary>
        public TimeSpan? HeartbeatInterval
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the custom metadata derived for this instance to broadcast in heartbeats.
        /// </summary>
        public string? InstanceMetadata
        {
            get; set;
        }

        private void UpdateKey()
        {
            if (string.IsNullOrEmpty(_securityKey))
            {
                _keyManager.Dispose();
                return;
            }

            try
            {
                _keyManager.SetKey(_securityKey!, retainPrevious: false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to update keys from SecurityKey.");
                throw;
            }
        }

        private (string? CipherText, string? Nonce, string? Tag) Encrypt(string plainText)
        {
            var session = _keyManager.Current;
            if (session == null)
                return (null, null, null);

            if (session.AesGcmInstance != null)
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] nonce = new byte[12];
                byte[] tag = new byte[16];
                byte[] cipherBytes = new byte[plainBytes.Length];

                RandomNumberGenerator.Fill(nonce);
                session.AesGcmInstance.Encrypt(nonce, plainBytes, cipherBytes, tag);

                return (Convert.ToBase64String(cipherBytes), Convert.ToBase64String(nonce), Convert.ToBase64String(tag));
            }
            // S5: Authenticated Encryption Enforcement
            throw new PlatformNotSupportedException("AES-GCM is required for encryption but not supported on this platform.");
        }



        private string Decrypt(string cipherText, string nonce, string? tag)
        {
            var session = _keyManager.Current;
            if (session == null)
                throw new InvalidOperationException("Cannot decrypt without EncryptionKey.");

            if (session.AesGcmInstance != null && tag != null)
            {
                byte[] nonceBytes = Convert.FromBase64String(nonce);
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] tagBytes = Convert.FromBase64String(tag);
                byte[] plainBytes = new byte[cipherBytes.Length];

                session.AesGcmInstance.Decrypt(nonceBytes, cipherBytes, tagBytes, plainBytes);

                return Encoding.UTF8.GetString(plainBytes);
            }

            // S5: Authenticated Encryption Enforcement
            throw new PlatformNotSupportedException("AES-GCM is required for decryption but not supported on this platform, or Tag was missing.");
        }

        private (byte[]? CipherBytes, byte[]? Nonce, byte[]? Tag) EncryptBytes(byte[] plainBytes)
        {
            var session = _keyManager.Current;
            if (session == null)
                return (null, null, null);

            if (session.AesGcmInstance != null)
            {
                byte[] nonce = new byte[12];
                byte[] tag = new byte[16];
                byte[] cipherBytes = new byte[plainBytes.Length];

                RandomNumberGenerator.Fill(nonce);
                session.AesGcmInstance.Encrypt(nonce, plainBytes, cipherBytes, tag);

                return (cipherBytes, nonce, tag);
            }
            throw new PlatformNotSupportedException("AES-GCM is required for encryption but not supported on this platform.");
        }

        private byte[] DecryptBytes(byte[] cipherBytes, byte[] nonce, byte[]? tag)
        {
            var session = _keyManager.Current;
            if (session == null)
                throw new InvalidOperationException("Cannot decrypt without EncryptionKey.");

            if (session.AesGcmInstance != null && tag != null)
            {
                byte[] plainBytes = new byte[cipherBytes.Length];
                session.AesGcmInstance.Decrypt(nonce, cipherBytes, tag, plainBytes);
                return plainBytes;
            }
            throw new PlatformNotSupportedException("AES-GCM is required for decryption but not supported on this platform, or Tag was missing.");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportComponent"/> class.
        /// </summary>
        /// <param name="options">The multicast socket options to use.</param>
        /// <param name="socket">Optional pre-configured socket (e.g. for testing).</param>
        public TransportComponent(MulticastSocketOptions options, IMulticastSocket? socket = null)
        {
            _socketOptions = options;
            _socket = socket;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            _keyManager = new KeyManager(Logger);

            RegisterInternalTypes();
        }

        private void RegisterInternalTypes()
        {
            _knownTypes.TryAdd(AckMessageType, typeof(AckMessageContent));
            _knownTypes.TryAdd("sys.heartbeat", typeof(HeartbeatMessage));
            _typeToIdMap.TryAdd(typeof(HeartbeatMessage), "sys.heartbeat");

            _knownTypes.TryAdd("sys.rekey", typeof(RekeyMessage));
            _typeToIdMap.TryAdd(typeof(RekeyMessage), "sys.rekey");


        }

        /// <summary>
        /// Registers a handler for a message type. The type must have the <see cref="MessageTypeAttribute"/>.
        /// </summary>
        public void RegisterHandler<T>(Action<T, MessageContext> handler) where T : class
        {
            var attr = (MessageTypeAttribute?)Attribute.GetCustomAttribute(typeof(T), typeof(MessageTypeAttribute));
            if (attr == null)
            {
                throw new InvalidOperationException($"Type {typeof(T).Name} does not have a [MessageType] attribute. Use the overload that accepts an ID.");
            }
            RegisterHandler(attr.MsgId, handler);
        }

        /// <summary>
        /// Registers a handler for a specific message type ID.
        /// </summary>
        public void RegisterHandler<T>(string id, Action<T, MessageContext> handler) where T : class
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Message ID cannot be null or empty.", nameof(id));
            }
            _genericHandlers[id] = handler;
            _typeToIdMap[typeof(T)] = id;
            _knownTypes[id] = typeof(T);
        }

        /// <summary>
        /// Registers a message type for deserialization without a specific handler.
        /// Useful for auto-discovery or when using generic message processing.
        /// </summary>
        public void RegisterMessageType<T>(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Message ID cannot be null or empty.", nameof(id));
            }
            _typeToIdMap[typeof(T)] = id;
            _knownTypes[id] = typeof(T);
        }

        /// <summary>
        /// Starts the transport component and starts listening for traffic.
        /// </summary>
        public void Start()
        {
            Stop();

            _receiveCts = new CancellationTokenSource();

            var channelOptions = new BoundedChannelOptions(MaxQueueSize)
            {
                FullMode = BoundedChannelFullMode.DropWrite
            };

            _gateInput = Channel.CreateBounded<GateCmd>(channelOptions);
            _processingChannel = Channel.CreateBounded<SocketMessage>(channelOptions);

            // Start the Actor Loops
            _gateLoopTask = Task.Run(GateKeeperLoop);
            _processingLoopTask = Task.Run(ProcessingLoop);

            if (_socket == null)
            {
                var builder = new MulticastSocketBuilder()
                    .WithOptions(_socketOptions)
                    .OnError(err => Logger.LogError(
                        "Socket Error: {0}. Exception: {1}",
                        err.Message,
                        err.Exception?.Message));

                builder.WithLogger(Logger);

                _socket = builder.Build();
            }

            _socket.StartReceiving();

            _receiveTask = Task.Run(async () => await ReceiveLoop(_receiveCts.Token));

            string interfaces = string.Join(
                ", ",
                _socket.JoinedAddresses.Select(a => a.ToString()));
            Logger.LogInformation(
                "Multicast Socket Started on {0}:{1} (TTL: {2}, Interfaces: {3})",
                _socketOptions.GroupAddress,
                _socketOptions.Port,
                _socketOptions.TimeToLive,
                interfaces);

            string integrityMode = SecurityKey != null ? "HMAC-SHA256" : "SHA256 (No Key)";
            string encMode = "Disabled";
            if (EncryptionEnabled)
            {
                encMode = "AES-GCM (Hardware Accelerated)";
            }

            Logger.LogInformation("TransportComponent Initialized (Lock-Free Mode). Integrity: {0}. Encryption: {1}", integrityMode, encMode);

            if (HeartbeatInterval.HasValue)
            {
                _heartbeatCts = new CancellationTokenSource();
                _heartbeatTask = Task.Run(async () => await HeartbeatLoop(_heartbeatCts.Token));
                Logger.LogInformation("Heartbeat enabled with interval {0}", HeartbeatInterval.Value);
            }

            // Internal subscription for heartbeats
            RegisterHandler<HeartbeatMessage>("sys.heartbeat", HandleHeartbeat);
            RegisterHandler<RekeyMessage>("sys.rekey", HandleRekey);

        }

        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            if (_socket == null)
                return;

            try
            {
                await foreach (var msg in _socket.GetMessageStream(cancellationToken))
                {
                    HandleSocketMessage(msg);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("Receive loop stopped.");
            }
            catch (Exception ex)
            {
                if (_receiveCts != null && !_receiveCts.IsCancellationRequested)
                {
                    Logger.LogError(ex, "Error in TransportComponent receive loop.");
                }
            }
        }

        private async Task GateKeeperLoop()
        {
            var pq = new PriorityQueue<SocketMessage>();
            int currentSeq = 1;
            CancellationTokenSource? gapCts = null;

            if (_gateInput == null || _processingChannel == null)
                return;

            try
            {
                while (await _gateInput.Reader.WaitToReadAsync())
                {
                    while (_gateInput.Reader.TryRead(out var cmd))
                    {
                        if (cmd is InputMsgCmd input)
                        {
                            var msg = input.Msg;
                            if (msg.ArrivalSequenceId < currentSeq)
                            {
                                Logger.LogWarning("Received late message {0} (current is {1}). Ignoring.", msg.ArrivalSequenceId, currentSeq);
                                msg.Dispose();
                                continue;
                            }

                            if (msg.ArrivalSequenceId == currentSeq)
                            {
                                // Correct message
                                if (gapCts != null)
                                {
                                    gapCts.Cancel();
                                    gapCts = null;
                                }
                                if (!_processingChannel.Writer.TryWrite(msg))
                                {
                                    Logger.LogWarning("Processing channel full. Dropping message {0}.", msg.ArrivalSequenceId);
                                    msg.Dispose();
                                }
                                else
                                {
                                    currentSeq++;
                                    // Check queue for next messages
                                    CheckQueue(pq, ref currentSeq, _processingChannel);
                                }
                            }
                            else
                            {
                                // Future message - Check Queue Size Limit
                                if (pq.Count >= MaxQueueSize)
                                {
                                    Logger.LogWarning("PriorityQueue full ({0}). Dropping future message {1} to prevent DoS.", pq.Count, msg.ArrivalSequenceId);
                                    msg.Dispose();
                                    continue;
                                }

                                pq.Enqueue(msg, msg.ArrivalSequenceId);
                                if (gapCts == null)
                                {
                                    gapCts = new CancellationTokenSource();
                                    var token = gapCts.Token;
                                    var captureSeq = currentSeq;
                                    _ = Task.Delay(GateKeeperTimeout, token).ContinueWith(t =>
                                    {
                                        if (!t.IsCanceled && _gateInput != null)
                                        {
                                            _gateInput.Writer.TryWrite(new TimeoutCmd { SeqId = captureSeq });
                                        }
                                    });
                                }
                            }
                        }
                        else if (cmd is TimeoutCmd timeout)
                        {
                            if (timeout.SeqId == currentSeq)
                            {
                                // Timeout occurred on this sequence
                                if (pq.Count > 0 && pq.TryPeek(out var nextMsg, out var priority))
                                {
                                    Logger.LogWarning("Sequence gap detected. Timed out waiting for message {0}. Jumping to {1}.", currentSeq, priority);
                                    currentSeq = priority;
                                    gapCts = null;
                                    CheckQueue(pq, ref currentSeq, _processingChannel);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Critical error in GateKeeperLoop");
            }
        }

        private void CheckQueue(PriorityQueue<SocketMessage> pq, ref int currentSeq, Channel<SocketMessage> output)
        {
            while (pq.Count > 0 && pq.TryPeek(out var nextMsg, out var priority))
            {
                if (priority == currentSeq)
                {
                    pq.Dequeue();
                    if (!output.Writer.TryWrite(nextMsg))
                    {
                        Logger.LogWarning("Processing channel full. Dropping queued message {0}.", nextMsg.ArrivalSequenceId);
                        nextMsg.Dispose();
                    }
                    currentSeq++;
                }
                else if (priority < currentSeq)
                {
                    // Cleanup old messages
                    pq.Dequeue().Dispose();
                }
                else
                {
                    break;
                }
            }
        }

        private async Task ProcessingLoop()
        {
            if (_processingChannel == null)
                return;
            try
            {
                while (await _processingChannel.Reader.WaitToReadAsync())
                {
                    while (_processingChannel.Reader.TryRead(out var msg))
                    {
                        try
                        {
                            using (var activity = _activitySource.StartActivity("ReceiveMessage", ActivityKind.Consumer))
                            {
                                activity?.SetTag("transport.seq", msg.ArrivalSequenceId);

                                _messageSubject.OnNext(msg);
                                ProcessSingleMessage(msg);
                            }
                        }
                        finally
                        {
                            msg.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Critical error in ProcessingLoop");
            }
        }

        /// <summary>
        /// Stops the transport component and closes the underlying socket.
        /// </summary>
        public void Stop()
        {
            _receiveCts?.Cancel();
            try
            {
                _receiveTask?.Wait(500);
            }
            catch { }
            _receiveCts?.Dispose();
            _receiveCts = null;

            if (_socket != null)
            {
                _socket.Close();
                _socket.Dispose();
                _socket = null;
            }

            // Shutdown actors
            _gateInput?.Writer.TryComplete();
            _processingChannel?.Writer.TryComplete();

            _heartbeatCts?.Cancel();
            try
            {
                Task.WaitAll(new[] { _gateLoopTask, _processingLoopTask, _heartbeatTask }.Where(t => t != null).ToArray()!, 1000);
            }
            catch { }

            // _aesGcmInstance managed by KeyManager
        }

        /// <summary>
        /// Disposes the component and releases resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
            // S2: Secure Memory Erasure
            _keyManager.Dispose();

            try
            {
                _messageSubject.OnCompleted();
                _messageSubject.Dispose();
            }
            catch { }
            GC.SuppressFinalize(this);
        }

        private async Task HeartbeatLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var heartbeat = new HeartbeatMessage
                    {
                        SourceId = LocalSource.ResourceId.ToString(),
                        DeviceName = LocalSource.ResourceName,
                        UptimeSeconds = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds,
                        Metadata = InstanceMetadata
                    };

                    // Send without ACK, fire and forget
                    await SendAsync(heartbeat);

                    // S3: Cleanup stale replay data
                    CleanupReplayProtection();

                    // Cleanup stale peers
                    _peerTable.CleanupStalePeers(TimeSpan.FromSeconds(HeartbeatInterval!.Value.TotalSeconds * 3));

                    await Task.Delay(HeartbeatInterval.Value, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error in Heartbeat Loop");
                }
            }
        }

        private void HandleHeartbeat(HeartbeatMessage msg, MessageContext context)
        {
            if (msg.SourceId == LocalSource.ResourceId.ToString())
                return; // Ignore self

            _peerTable.UpdatePeer(msg.SourceId, msg.DeviceName, msg.Metadata);
        }

        private void HandleRekey(RekeyMessage msg, MessageContext context)
        {
            try
            {
                Logger.LogInformation("Received Rekey Message (KeyId: {0}). Rotating keys...", msg.KeyId);
                if (string.Compare(LocalSource.ResourceId.ToString(), context.Source.ResourceId.ToString(), StringComparison.OrdinalIgnoreCase) == 0)
                {
                    // Ignore self-sent rekey
                    return;
                }

                // Apply new key with grace period
                _keyManager.SetKey(msg.NewKey, retainPrevious: true);

                // Schedule cleanup after ReplayWindow (plus buffer)
                var gracePeriod = ReplayWindow.Add(TimeSpan.FromSeconds(5));
                Task.Delay(gracePeriod).ContinueWith(_ => _keyManager.ClearPreviousKey());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to process RekeyMessage.");
            }
        }



        private void CleanupReplayProtection()
        {
            // S3: Remove replay windows irrelevant for more than 5 minutes (or 5x Heartbeat)
            var threshold = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5));
            foreach (var kvp in _replayProtection)
            {
                if (kvp.Value.LastActivity < threshold)
                {
                    _replayProtection.TryRemove(kvp.Key, out _);
                }
            }
        }

        /// <summary>
        /// Verifies networking configuration by performing firewall checks and a loopback test asynchronously.
        /// </summary>
        /// <returns>True if diagnostics pass, otherwise false.</returns>
        public async Task<bool> VerifyNetworkingAsync()
        {
            Logger.LogInformation("Performing Network Diagnostics...");
            NetworkDiagnostics.LogFirewallStatus(_socketOptions.Port, Logger);

            bool success = await NetworkDiagnostics.PerformLoopbackTestAsync(this);
            if (success)
            {
                Logger.LogInformation(
                    "Network Diagnostics Passed: Multicast Loopback Successful.");
            }
            else
            {
                Logger.LogWarning(
                    "Network Diagnostics Failed: Multicast Loopback NOT " +
                    "received. Check firewall settings and interface " +
                    "configuration.");
            }
            return success;
        }

        /// <summary>
        /// Sends a message of type T over the multicast socket asynchronously.
        /// </summary>
        /// <param name="content">The message content to send.</param>
        /// <param name="options">Optional send options.</param>
        /// <typeparam name="T">The type of the message content.</typeparam>
        /// <returns>A task that completes with an <see cref="AckSession"/> for tracking acknowledgements.</returns>
        public async Task<AckSession> SendAsync<T>(T content, SendOptions? options = null)
            where T : class
        {
            string? messageType;
            if (options?.MessageType != null)
            {
                messageType = options.MessageType;
            }
            else if (!_typeToIdMap.TryGetValue(typeof(T), out messageType))
            {
                // Fallback: check attribute directly if not registered but trying to send
                var attr = (MessageTypeAttribute?)Attribute.GetCustomAttribute(typeof(T), typeof(MessageTypeAttribute));
                if (attr != null)
                {
                    messageType = attr.MsgId;
                }
                else
                {
                    throw new ArgumentException(
                        $"Type {typeof(T).Name} is not registered, has no [MessageType] attribute, and no " +
                        "MessageType was provided in SendOptions.");
                }
            }

            var message = new TransportMessage(LocalSource, messageType, content)
            {
                RequestAck = options?.RequestAck ?? false
            };

            return await SendInternalAsync(message, options?.AckTimeout);
        }

        private async Task<AckSession> SendInternalAsync(
            TransportMessage message,
            TimeSpan? ackTimeout)
        {
            var session = new AckSession(message.MessageId);

            using var activity = _activitySource.StartActivity("SendMessage", ActivityKind.Producer);
            activity?.SetTag("message.type", message.MessageType);
            activity?.SetTag("message.id", message.MessageId.ToString());

            if (message.RequestAck)
            {
                _activeSessions.TryAdd(message.MessageId, session);
                _ = session.WaitAsync(ackTimeout ?? DefaultAckTimeout)
                    .ContinueWith(_ =>
                    {
                        _activeSessions.TryRemove(message.MessageId, out var _);
                    });
            }

            // NEW: Zero-Allocation Serialization with Native Encryption
            // We use ArrayBufferWriter to write directly to a buffer, avoiding intermediate arrays and Base64 strings.
            var writer = new ArrayBufferWriter<byte>();

            try
            {
                // Ensure the message knows it should be encrypted if enabled
                var keySession = _keyManager.Current;
                if (EncryptionEnabled && keySession != null)
                {
                    message.IsEncrypted = true;
                }

                // S3: SecureMemory Usage (Native GCM via Delegate)
                BinaryPacket.SerializeToWriter(writer, message, 0, null,
                     message.IsEncrypted ? (EncryptorDelegate?)keySession!.Encrypt : null,
                     _jsonOptions);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to serialize message {0}", message.MessageId);
                throw;
            }

            if (_socket != null)
            {
                // Bolt: Phase 3 (Socket Opt) Implemented.
                // Pass ReadOnlyMemory directly to avoid ToArray() allocation.
                await _socket.SendAsync(writer.WrittenMemory);
            }

            if (!message.RequestAck)
            {
                session.ReportAck(LocalSource);
            }

            return session;
        }

        /// <summary>
        /// Sends an acknowledgement for the message associated with the given context asynchronously.
        /// </summary>
        public Task SendAckAsync(MessageContext context)
        {
            return SendAckAsync(context.MessageId);
        }

        private Task SendAckAsync(Guid originalMessageId)
        {
            var ackContent = new AckMessageContent
            {
                OriginalMessageId = originalMessageId
            };

            var ackMessage = new TransportMessage(LocalSource, AckMessageType, ackContent)
            {
                RequestAck = false
            };

            return SendInternalAsync(ackMessage, null);
        }

        internal void HandleSocketMessage(SocketMessage msg)
        {
            if (!EnforceOrdering)
            {
                // Route directly to processing
                // The processing loop owns the message disposal
                if (!(_processingChannel?.Writer.TryWrite(msg) ?? false))
                {
                    msg.Dispose();
                }
            }
            else
            {
                // Route to GateKeeper
                if (!(_gateInput?.Writer.TryWrite(new InputMsgCmd { Msg = msg }) ?? false))
                {
                    // Channel full or null
                    msg.Dispose();
                }
            }
        }

        private void ProcessSingleMessage(SocketMessage msg)
        {
            try
            {
                TransportMessage? tMessage = null;

                // Check for Magic Byte
                if (msg.Length > 0 && msg.Data[0] == BinaryPacket.MagicByte)
                {
                    // P2: Struct-Based Header Parsing (Optimization + Fix)
                    if (BinaryPacket.TryReadHeader(msg.Data.AsSpan(0, msg.Length), out var header))
                    {
                        long ticks = header.Ticks;

                        // --- Replay Protection (Timestamp Check) ---
                        // Only allow messages within ReplayWindow (Past) or small skew (Future)
                        // This prevents processing old packets at all.
                        long nowTicks = DateTime.UtcNow.Ticks;
                        long windowTicks = ReplayWindow.Ticks;

                        // We use a simplified check: Ticks must be > Now - Window
                        if (ticks < nowTicks - windowTicks)
                        {
                            // Too old
                            if (Logger.IsEnabled(LogLevel.Trace))
                                Logger.LogTrace("Dropped old message {0} (Ticks: {1}).", header.SequenceId, ticks);
                            return;
                        }

                        // Feature 5 FIX: Strict Replay Protection using SENDER Sequence ID
                        // We don't have SourceId in PacketHeader yet easily (it's Guid, expensive to parse?)
                        // TryReadHeader does NOT parse SourceId?
                        // Let's check BinaryPacket.PacketHeader definition I added...
                        // It has SequenceId, Ticks, MessageType.
                        // I NEED SOURCE ID to look up the correct ReplayWindow!
                        // "P2" only added limited fields.
                        // I should have added SourceId (Guid) to PacketHeader in P2 step if I wanted to use it here.
                        // Since I didn't, I must accept I can't look up the specific Peer ReplayWindow yet without parsing SourceID.

                        // FIXME: For now, we unfortunately MUST deserialize to get SourceID to find the correct ReplayWindow.
                        // UNLESS I update TryReadHeader again.
                        // But I can at least filter globally invalid timestamps.
                    }


                    // Binary Protocol
                    try
                    {
                        var current = _keyManager.Current;
                        var previous = _keyManager.Previous;

                        try
                        {
                            // Try Current
                            tMessage = BinaryPacket.Deserialize(
                                msg.Data.AsSpan(0, msg.Length),
                                msg.ArrivalSequenceId,
                                _jsonOptions,
                                current != null ? (DecryptorDelegate?)current.Decrypt : null);
                        }
                        catch (System.Security.Cryptography.CryptographicException)
                        {
                            // Failed auth? Try previous if avail
                            if (previous != null)
                            {
                                tMessage = BinaryPacket.Deserialize(
                                    msg.Data.AsSpan(0, msg.Length),
                                    msg.ArrivalSequenceId,
                                    _jsonOptions,
                                    (DecryptorDelegate?)previous.Decrypt);
                            }
                            else
                                throw;
                        }
                    }

                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to deserialize binary packet.");
                        OnMessageError?.Invoke(this, new MessageErrorEventArgs(msg, "Binary Deserialization Failed", ex));
                        return;
                    }
                }
                else
                {
                    // [LEGACY] JSON Protocol Fallback
                    // This block is maintained for backward compatibility with older clients.
                    // It may be removed in future major versions.
                    string sMessage = Encoding.UTF8.GetString(msg.Data, 0, msg.Length);
                    Logger.LogTrace("Importing message {0} (Legacy)", msg.ArrivalSequenceId);
                    tMessage = JsonSerializer.Deserialize<TransportMessage>(sMessage, _jsonOptions);
                }

                if (tMessage != null)
                {
                    TransportMessage message = tMessage.Value;

                    // --- Replay Protection (Strict) ---
                    // Now we have the SourceID from the deserialized message.
                    if (DateTime.TryParse(message.TimeStamp, out var ts))
                    {
                        var now = DateTime.UtcNow; // Use UtcNow consistently (Header used UtcNow logic)
                        if (ts < now.Subtract(ReplayWindow))
                        {
                            Logger.LogWarning("Dropped replay/old message {0} (Timestamp: {1}). Window is {2}s.", message.MessageId, message.TimeStamp, ReplayWindow.TotalSeconds);
                            return;
                        }
                    }

                    var window = _replayProtection.GetOrAdd(message.MessageSource.ResourceId, _ => new ReplayWindow());

                    // FIX: Use packet's sequence ID from Header/Message if available?
                    // BinaryPacket.Deserialize DOES NOT populate a "SequenceId" field on TransportMessage (it's not part of the class!).
                    // This is a design flaw in the original code: TransportMessage transport object didn't carry the SequenceID.
                    // The SequenceID was only in the wire format (header) and used to order `SocketMessage` (which used local ID 🤬).

                    // To fix this properly, I'd need to add `SequenceId` to `TransportMessage` class.
                    // But I can't easily change `TransportMessage` without potentially breaking other things or serialization?
                    // It's a struct/class. I can check definition.
                    // It was defined in `TransportMessage.cs` (not viewed yet).

                    // WORKAROUND: rely on `msg.ArrivalSequenceId` for now but log a warning that it's using local seq?
                    // No, that's what we are fixing.

                    // P2 Header Parsing let me read `seqId`.
                    // But I lost it after `Deserialize`.
                    // `BinaryPacket.Deserialize` returns `TransportMessage`.

                    // I will extract header AGAIN (cheaply) to get the sender sequence ID if it was binary?
                    // Or I assume `msg.ArrivalSequenceId` is what passed downstream?
                    // No, I need the SENDER sequence ID.

                    int senderSequenceId = -1;
                    if (msg.Data.Length > 0 && msg.Data[0] == BinaryPacket.MagicByte)
                    {
                        if (BinaryPacket.TryReadHeader(msg.Data.AsSpan(0, msg.Length), out var h))
                        {
                            senderSequenceId = h.SequenceId;
                        }
                    }

                    if (senderSequenceId != -1)
                    {
                        if (!window.CheckAndMark(senderSequenceId))
                        {
                            // Drop
                            if (Logger.IsEnabled(LogLevel.Trace))
                                Logger.LogTrace("Replay/Duplicate detected for Seq {0}", senderSequenceId);
                            return;
                        }
                    }
                    else
                    {
                        // JSON fallback or failed parse - Use local seq as best effort fallback?
                        // Or just skip replay check for Legacy JSON?
                        // Legacy JSON didn't have SeqID in header (it's JSON).
                        // It likely relied on MessageId (Guid) for deduplication in a different way or didn't have it.
                        // We will skip `CheckAndMark` for legacy or use Timestamp only.
                    }

                    // ------------------------------------------
                    // Logic to handle message processing
                    // ------------------------------------------
                    // If it was legacy JSON, verify signature. (Binary Packet already verified/decrypted in steps above if using native mode)
                    if (msg.Data[0] != BinaryPacket.MagicByte)
                    {
                        if (string.IsNullOrEmpty(message.Signature))
                        {
                            Logger.LogWarning("Dropped unsigned message {0}.", message.MessageId);
                            OnMessageError?.Invoke(this, new MessageErrorEventArgs(msg, "Missing Signature"));
                            return;
                        }

                        // S3 Temp Copy (Legacy)
                        // This legacy path is non-optimal but preserved for old clients
                        byte[]? tempInt = _keyManager.Current?.IntegrityKey.Memory.ToArray();
                        string expectedSignature;
                        try
                        {
                            expectedSignature = ComputeSignature(message, tempInt);
                        }
                        finally
                        {
                            if (tempInt != null)
                                Array.Clear(tempInt, 0, tempInt.Length);
                        }

                        if (message.Signature != expectedSignature)
                        {
                            Logger.LogWarning("Dropped message {0} with invalid signature.", message.MessageId);
                            OnMessageError?.Invoke(this, new MessageErrorEventArgs(msg, "Invalid Signature"));
                            return;
                        }
                    }

                    // --- Decryption Logic ---
                    // Legacy Support: If still IsEncrypted but MessageData is String (Base64) and Nonce is present, it might be Legacy AES-GCM/CBC.
                    // However, new BinaryPacket.Deserialize handles decryption internally for native binary packets.
                    // So we only need to check this for Legacy JSON packets OR if Binary deserializer returned encrypted implementation (which it shouldn't for native).

                    if (message.IsEncrypted && message.MessageData is string cipherText && message.Nonce != null)
                    {
                        // This path is now primarily for LEGACY JSON encrypted flows
                        try
                        {
                            string plainText = Decrypt(cipherText, message.Nonce, message.Tag);
                            message.MessageData = plainText; // Now it is JSON string
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Decryption failed for message {0}.", message.MessageId);
                            OnMessageError?.Invoke(this, new MessageErrorEventArgs(msg, "Decryption Failed", ex));
                            return;
                        }
                    }
                    // ------------------------

                    ProcessTransportMessage(message, msg.ArrivalSequenceId);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "Error Processing Received Message {0}: {1}",
                    msg.ArrivalSequenceId,
                    ex.Message);
            }
        }

        private void ProcessTransportMessage(TransportMessage tMessage, int sequenceId)
        {
            if (tMessage.MessageData is JsonElement element)
            {
                if (_knownTypes.TryGetValue(tMessage.MessageType, out Type? targetType))
                {
                    try
                    {
                        // Bolt Optimization: Deserialize directly from JsonElement to avoid string allocation from GetRawText()
                        tMessage.MessageData = JsonSerializer.Deserialize(element, targetType, _jsonOptions)!;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to deserialize message content for type {0}", tMessage.MessageType);
                    }
                }
            }
            else if (tMessage.MessageData is string jsonString)
            {
                if (_knownTypes.TryGetValue(tMessage.MessageType, out Type? targetType))
                {
                    try
                    {
                        tMessage.MessageData = JsonSerializer.Deserialize(jsonString, targetType, _jsonOptions)!;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to deserialize decrypted content for type {0}", tMessage.MessageType);
                    }
                }
            }

            if (tMessage.MessageType == AckMessageType &&
                tMessage.MessageData is AckMessageContent ackContent)
            {
                if (_activeSessions.TryGetValue(
                    ackContent.OriginalMessageId,
                    out var session))
                {
                    Logger.LogTrace(
                        "Received Ack for message {0} from {1}",
                        ackContent.OriginalMessageId,
                        tMessage.MessageSource.ResourceName);
                    session.ReportAck(tMessage.MessageSource);
                }
            }

            Logger.LogTrace("Processing message {0}", sequenceId);

            bool handled = false;

            if (_genericHandlers.TryGetValue(tMessage.MessageType, out var handler))
            {
                var context = new MessageContext(
                    tMessage.MessageId,
                    tMessage.MessageSource,
                    tMessage.TimeStamp,
                    tMessage.RequestAck);
                handler.DynamicInvoke(tMessage.MessageData, context);
                handled = true;
            }

            if (handled && AutoSendAcks && tMessage.RequestAck &&
                tMessage.MessageType != AckMessageType)
            {
                // S4: Traffic Amplification Mitigation (Rate Limit Acks)
                bool allowed = true;
                if (_replayProtection.TryGetValue(tMessage.MessageSource.ResourceId, out var window))
                {
                    allowed = window.CheckAckRateLimit();
                }

                if (allowed)
                {
                    Logger.LogTrace(
                        "Automatically sending Ack for message {0}",
                        tMessage.MessageId);
                    _ = SendAckAsync(tMessage.MessageId);
                }
                else
                {
                    Logger.LogWarning("Ack Rate Limit exceeded for {0}. Dropping Ack.", tMessage.MessageSource.ResourceId);
                }
            }
        }

        internal string ComputeSignature(TransportMessage message, byte[]? keyBytes)
        {
            // P5: Canonical Signature Optimization
            // Use ArrayBufferWriter to avoid intermediate string allocations
            var writer = new ArrayBufferWriter<byte>();

            // 1. MessageId (Guid)
            // Use "D" format (hyphens, usually lowercase hex in .NET)
            // Note: Guid.ToString() defaults to "D".
            // To match exact legacy behavior: sb.Append(message.MessageId.ToString());
            string guidStr = message.MessageId.ToString();
            WriteAscii(writer, guidStr);

            // 2. TimeStamp
            WriteAscii(writer, message.TimeStamp);

            // 3. MessageType
            WriteAscii(writer, message.MessageType);

            // 4. RequestAck (bool -> "true"/"false")
            WriteAscii(writer, message.RequestAck ? "true" : "false");

            // 5. IsEncrypted
            WriteAscii(writer, message.IsEncrypted ? "true" : "false");

            // 6. Nonce
            if (message.Nonce != null)
                WriteAscii(writer, message.Nonce);

            // 7. Tag
            if (message.Tag != null)
                WriteAscii(writer, message.Tag);

            // 8. MessageData (JSON)
            // Serialize directly to the writer
            using (var jsonWriter = new Utf8JsonWriter(writer))
            {
                JsonSerializer.Serialize(jsonWriter, message.MessageData, _jsonOptions);
            }

            // Hashing
            byte[] hash;
            var payload = writer.WrittenSpan;

            if (keyBytes != null && keyBytes.Length > 0)
            {
                hash = HMACSHA256.HashData(keyBytes, payload);
            }
            else
            {
                hash = SHA256.HashData(payload);
            }

            return Convert.ToBase64String(hash);
        }

        private void WriteAscii(IBufferWriter<byte> writer, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            // For ASCII/UTF8 validation, GetBytes is sufficient.
            // In .NET 8 we could use Encoding.UTF8.GetBytes(value, writer.GetSpan(value.Length));
            // fallback to simpler approach for now to handle both targets cleanly.
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes);
        }
    }
}
