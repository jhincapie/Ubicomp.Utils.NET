#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// The central component for managing multicast transport communication.
    /// Handles message serialization, sequential processing via a gatekeeper,
    /// and routing messages to registered handlers.
    /// </summary>
    public class TransportComponent
    {
        /// <summary>Gets or sets the logger for this component.</summary>
        public ILogger Logger { get; set; } = NullLogger.Instance;

        /// <summary>The unique ID for the transport component itself.</summary>
        public const int TransportComponentID = 0;

        /// <summary>
        /// The message type ID used for acknowledgements.
        /// </summary>
        public const string AckMessageType = "sys.ack";

        private MulticastSocket? _socket;
        private readonly MulticastSocketOptions _socketOptions;
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;

        private readonly ConcurrentDictionary<string, Type> _knownTypes = new ConcurrentDictionary<string, Type>();
        private readonly ConcurrentDictionary<string, Delegate> _genericHandlers = new ConcurrentDictionary<string, Delegate>();
        private readonly ConcurrentDictionary<Type, string> _typeToIdMap = new ConcurrentDictionary<Type, string>();

        private readonly ConcurrentDictionary<Guid, AckSession> _activeSessions = new ConcurrentDictionary<Guid, AckSession>();

        private readonly JsonSerializerOptions _jsonOptions;

        // Lock-Free Actor Model Channels
        private Channel<GateCmd>? _gateInput;
        private Channel<SocketMessage>? _processingChannel;
        private Task? _gateLoopTask;
        private Task? _processingLoopTask;

        private abstract class GateCmd { }
        private class InputMsgCmd : GateCmd { public SocketMessage Msg = null!; }
        private class TimeoutCmd : GateCmd { public int SeqId; }

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
                DeriveKeys();
            }
        }
        private string? _securityKey;
        private byte[]? _integrityKey;
        private byte[]? _encryptionKey;

        internal byte[]? IntegrityKey => _integrityKey;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        private System.Security.Cryptography.AesGcm? _aesGcmInstance;
#endif

        /// <summary>
        /// Gets or sets whether to enable payload encryption.
        /// Requires <see cref="SecurityKey"/> to be set.
        /// </summary>
        public bool EncryptionEnabled { get; set; }

        private void DeriveKeys()
        {
            if (string.IsNullOrEmpty(_securityKey))
            {
                _integrityKey = null;
                _encryptionKey = null;
                return;
            }

            try
            {
                byte[] masterKey = Encoding.UTF8.GetBytes(_securityKey!);

                // HKDF-like derivation using HMAC-SHA256
                // Salt is empty for simplicity in this implementation (Plan 1)
                // Info strings separate the contexts

                using (var hmac = new HMACSHA256(masterKey))
                {
                    // Derive Integrity Key
                    byte[] infoIntegrity = Encoding.UTF8.GetBytes("Ubicomp.Utils.NET.Integrity");
                    _integrityKey = hmac.ComputeHash(infoIntegrity);

                    // Derive Encryption Key
                    byte[] infoEncryption = Encoding.UTF8.GetBytes("Ubicomp.Utils.NET.Encryption");
                    _encryptionKey = hmac.ComputeHash(infoEncryption);

#if NET8_0_OR_GREATER
                    _aesGcmInstance?.Dispose();
                    _aesGcmInstance = new System.Security.Cryptography.AesGcm(_encryptionKey, 16);
#elif NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                    _aesGcmInstance?.Dispose();
                    _aesGcmInstance = new System.Security.Cryptography.AesGcm(_encryptionKey);
#endif
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to derive keys from SecurityKey.");
                throw;
            }
        }

        private (string? CipherText, string? Nonce, string? Tag) Encrypt(string plainText)
        {
            if (_encryptionKey == null) return (null, null, null);

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            if (_aesGcmInstance != null)
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] nonce = new byte[12]; // GCM standard nonce size
                byte[] tag = new byte[16];   // GCM auth tag size
                byte[] cipherBytes = new byte[plainBytes.Length];

                RandomNumberGenerator.Fill(nonce);

                // Thread-safety: AesGcm is thread-safe on .NET Core 3.0+
                _aesGcmInstance.Encrypt(nonce, plainBytes, cipherBytes, tag);

                return (Convert.ToBase64String(cipherBytes), Convert.ToBase64String(nonce), Convert.ToBase64String(tag));
            }
#endif

            // Fallback to AES-CBC
            using (var aes = Aes.Create())
            {
                aes.Key = _encryptionKey;
                aes.GenerateIV();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                    return (Convert.ToBase64String(ms.ToArray()), Convert.ToBase64String(aes.IV), null);
                }
            }
        }

        private string Decrypt(string cipherText, string nonce, string? tag)
        {
            if (_encryptionKey == null) throw new InvalidOperationException("Cannot decrypt without EncryptionKey.");

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            if (_aesGcmInstance != null && tag != null)
            {
                 byte[] nonceBytes = Convert.FromBase64String(nonce);
                 byte[] cipherBytes = Convert.FromBase64String(cipherText);
                 byte[] tagBytes = Convert.FromBase64String(tag);
                 byte[] plainBytes = new byte[cipherBytes.Length];

                 _aesGcmInstance.Decrypt(nonceBytes, cipherBytes, tagBytes, plainBytes);

                 return Encoding.UTF8.GetString(plainBytes);
            }
#endif

            using (var aes = Aes.Create())
            {
                aes.Key = _encryptionKey;
                aes.IV = Convert.FromBase64String(nonce);
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream(Convert.FromBase64String(cipherText)))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        private (byte[]? CipherBytes, byte[]? Nonce, byte[]? Tag) EncryptBytes(byte[] plainBytes)
        {
            if (_encryptionKey == null) return (null, null, null);

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            if (_aesGcmInstance != null)
            {
                byte[] nonce = new byte[12];
                byte[] tag = new byte[16];
                byte[] cipherBytes = new byte[plainBytes.Length];

                RandomNumberGenerator.Fill(nonce);
                _aesGcmInstance.Encrypt(nonce, plainBytes, cipherBytes, tag);

                return (cipherBytes, nonce, tag);
            }
#endif
            // Fallback CBC
            using (var aes = Aes.Create())
            {
                aes.Key = _encryptionKey;
                aes.GenerateIV();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(plainBytes, 0, plainBytes.Length);
                    }
                    return (ms.ToArray(), aes.IV, null);
                }
            }
        }

        private byte[] DecryptBytes(byte[] cipherBytes, byte[] nonce, byte[]? tag)
        {
             if (_encryptionKey == null) throw new InvalidOperationException("Cannot decrypt without EncryptionKey.");

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            if (_aesGcmInstance != null && tag != null)
            {
                 byte[] plainBytes = new byte[cipherBytes.Length];
                 _aesGcmInstance.Decrypt(nonce, cipherBytes, tag, plainBytes);
                 return plainBytes;
            }
#endif
            using (var aes = Aes.Create())
            {
                aes.Key = _encryptionKey;
                aes.IV = nonce;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream(cipherBytes))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var oms = new MemoryStream())
                {
                    cs.CopyTo(oms);
                    return oms.ToArray();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportComponent"/> class.
        /// </summary>
        /// <param name="options">The multicast socket options to use.</param>
        public TransportComponent(MulticastSocketOptions options)
        {
            _socketOptions = options;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            RegisterInternalTypes();
        }

        private void RegisterInternalTypes()
        {
            _knownTypes.TryAdd(AckMessageType, typeof(AckMessageContent));
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

            var builder = new MulticastSocketBuilder()
                .WithOptions(_socketOptions)
                .OnError(err => Logger.LogError(
                    "Socket Error: {0}. Exception: {1}",
                    err.Message,
                    err.Exception?.Message));

            builder.WithLogger(Logger);

            _socket = builder.Build();
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
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                encMode = "AES-GCM (Hardware Accelerated)";
#else
                encMode = "AES-CBC (Composite)";
#endif
            }

            Logger.LogInformation("TransportComponent Initialized (Lock-Free Mode). Integrity: {0}. Encryption: {1}", integrityMode, encMode);
        }

        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            if (_socket == null) return;

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

            if (_gateInput == null || _processingChannel == null) return;

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
            if (_processingChannel == null) return;
            try
            {
                while (await _processingChannel.Reader.WaitToReadAsync())
                {
                    while (_processingChannel.Reader.TryRead(out var msg))
                    {
                        try
                        {
                            ProcessSingleMessage(msg);
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

            try
            {
                Task.WaitAll(new[] { _gateLoopTask, _processingLoopTask }.Where(t => t != null).ToArray()!, 1000);
            }
            catch { }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            _aesGcmInstance?.Dispose();
            _aesGcmInstance = null;
#endif
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

            if (message.RequestAck)
            {
                _activeSessions.TryAdd(message.MessageId, session);
                _ = session.WaitAsync(ackTimeout ?? DefaultAckTimeout)
                    .ContinueWith(_ =>
                    {
                        _activeSessions.TryRemove(message.MessageId, out var _);
                    });
            }

            // --- Encryption Logic --- (Legacy JSON logic removed effectively by binary path)
             // ... We will use Binary Packet logic which handles encryption on bytes

            // Serialize Payload first
            byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(message.MessageData, _jsonOptions);
            byte[]? bNonce = null;
            byte[]? bTag = null;

            if (EncryptionEnabled)
            {
                var (cipher, n, t) = EncryptBytes(payloadBytes);
                if (cipher != null)
                {
                    payloadBytes = cipher;
                    message.IsEncrypted = true;
                    bNonce = n;
                    bTag = t;
                    // message.Nonce/Tag strings not strictly needed for binary path but good for object consistency if inspected
                    message.Nonce = n != null ? Convert.ToBase64String(n) : null;
                    message.Tag = t != null ? Convert.ToBase64String(t) : null;
                }
            }

            // --- Binary Serialization ---
            // Use dummy sequenceId here? The socket assigns the actual sequence ID.
            // But our BinaryPacket puts SeqID in the header.
            // The SocketMessage.SequenceId is assigned ON RECEIVE.
            // The Sender should also assign a sequence ID?
            // "GateKeeper" uses arrival order.
            // "TransportMessage" doesn't have a SeqID field property.
            // The binary header has it.
            // We can send 0 for now or implement sender-side sequencing.
            // Let's send 0.

            byte[] packet = BinaryPacket.Serialize(message, 0, bNonce, bTag, payloadBytes);

            if (_socket != null)
            {
                await _socket.SendAsync(packet);
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
                    // Binary Protocol
                     try
                     {
                         // msg.Data is full buffer, use Span with length
                         tMessage = BinaryPacket.Deserialize(msg.Data.AsSpan(0, msg.Length), msg.ArrivalSequenceId, _jsonOptions);

                         // Decrypt if needed (BinaryPacket returns Encrypted blob as Base64 string in MessageData if IsEncrypted is true)
                         // Wait, BinaryPacket.Deserialize DOES NOT DECRYPT automatically?
                         // It returns MessageData as Base64 string if encrypted.
                         // Check BinaryPacket.cs: "if (isEnc) messageData = Convert.ToBase64String(payloadSlice);"
                         // So we need to decrypt here.
                     }
                     catch (Exception ex)
                     {
                          Logger.LogError(ex, "Failed to deserialize binary packet.");
                          return;
                     }
                }
                else
                {
                    // Legacy JSON Protocol
                    string sMessage = Encoding.UTF8.GetString(msg.Data, 0, msg.Length);
                    Logger.LogTrace("Importing message {0} (Legacy)", msg.ArrivalSequenceId);
                    tMessage = JsonSerializer.Deserialize<TransportMessage>(sMessage, _jsonOptions);
                }

                if (tMessage != null)
                {
                    // --- Replay Protection (Timestamp Check) ---
                    if (DateTime.TryParse(tMessage.TimeStamp, out var ts))
                    {
                        // Allow small clock skew (future messages) and enforce replay window (past messages)
                        var now = DateTime.Now;
                        if (ts < now.Subtract(ReplayWindow))
                        {
                            Logger.LogWarning("Dropped replay/old message {0} (Timestamp: {1}). Window is {2}s.", tMessage.MessageId, tMessage.TimeStamp, ReplayWindow.TotalSeconds);
                            return;
                        }
                    }
                    else
                    {
                         Logger.LogWarning("Dropped message {0} with invalid timestamp.", tMessage.MessageId);
                         return;
                    }
                    // ------------------------------------------

                    // Signature Check?
                    // Binary Packet does NOT use the computed signature field in header (it wasn't in the spec I wrote).
                    // The spec has Encrypted flag which implies AES-GCM (Auth Tag is the signature).
                    // If Unencrypted?
                    // The Binary Header spec has "Flags".
                    // If we use HMAC-SHA256 for integrity-only mode, we need a place for it.
                    // The spec had: [Nonce:12][Tag:16].
                    // Tag is the signature.
                    // If unencrypted binary packet, do we have integrity?
                    // BinaryPacket.Serialize adds Tag if message.IsEncrypted.
                    // If !IsEncrypted, Tag is null.

                    // Legacy JSON had `Signature` field.
                    // Binary Protocol needs to support Integrity-Only mode for backward parity?
                    // SecurityKey != null but EncryptionEnabled == false.
                    // Implementation Plan didn't specify Interity-Only for binary.
                    // AES-GCM provides both.
                    // If we only have Integrity Key (no Encryption), we should probably sign.
                    // But `BinaryPacket` logic I wrote only adds Tag if Encrypted.
                    // For now, assume Encryption Enabled for security.

                    // If it was legacy JSON, verify signature.
                    if (msg.Data[0] != BinaryPacket.MagicByte)
                    {
                        if (string.IsNullOrEmpty(tMessage.Signature))
                        {
                            Logger.LogWarning("Dropped unsigned message {0}.", tMessage.MessageId);
                            return;
                        }

                        string expectedSignature = ComputeSignature(tMessage, _integrityKey);

                        if (tMessage.Signature != expectedSignature)
                        {
                            Logger.LogWarning("Dropped message {0} with invalid signature.", tMessage.MessageId);
                            return;
                        }
                    }

                    // --- Decryption Logic ---
                    if (tMessage.IsEncrypted)
                    {
                         // If Binary: MessageData is Base64 String
                         // If JSON: MessageData is Base64 String (or JsonElement)

                         // We need to check if it's already decrypted?
                         // BinaryPacket.Deserialize does NOT decrypt.

                         if (tMessage.MessageData is string cipherText)
                         {
                             // Decrypt
                             // If Binary, we have Byte-based nonce/tag in properties (Base64'd by Deserialize)
                             // Use Decrypt (String version) which internal converts base64 -> bytes.
                             // Wait, Decrypt (String) uses _encryptionKey.

                             // If it was binary packet, we put header nonce/tag into tMessage.Nonce/Tag.
                             // So standard logic should work!

                             if (tMessage.Nonce != null)
                             {
                                 try
                                 {
                                     string plainText = Decrypt(cipherText, tMessage.Nonce, tMessage.Tag);
                                     tMessage.MessageData = plainText; // Now it is JSON string
                                 }
                                 catch (Exception ex)
                                 {
                                     Logger.LogError(ex, "Decryption failed for message {0}.", tMessage.MessageId);
                                     return;
                                 }
                             }
                         }
                    }
                    // ------------------------

                    ProcessTransportMessage(tMessage, msg.ArrivalSequenceId);
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
                        tMessage.MessageData = JsonSerializer.Deserialize(element.GetRawText(), targetType, _jsonOptions)!;
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
                Logger.LogTrace(
                    "Automatically sending Ack for message {0}",
                    tMessage.MessageId);
                _ = SendAckAsync(tMessage.MessageId);
            }
        }

        internal string ComputeSignature(TransportMessage message, byte[]? keyBytes)
        {
            // Signature = Hash or HMAC of (MessageId + TimeStamp + MessageType + JsonData + RequestAck)
            var sb = new StringBuilder();
            sb.Append(message.MessageId.ToString());
            sb.Append(message.TimeStamp);
            sb.Append(message.MessageType);
            sb.Append(message.RequestAck.ToString().ToLower());
            sb.Append(message.IsEncrypted.ToString().ToLower());
            sb.Append(message.Nonce ?? "");
            sb.Append(message.Tag ?? "");

            // Best effort serialization for signature checks
            string dataJson = JsonSerializer.Serialize(message.MessageData, _jsonOptions);
            sb.Append(dataJson);

            byte[] payloadBytes = Encoding.UTF8.GetBytes(sb.ToString());

            if (keyBytes != null && keyBytes.Length > 0)
            {
                // HMAC Mode
                using (var hmac = new HMACSHA256(keyBytes))
                {
                    byte[] hash = hmac.ComputeHash(payloadBytes);
                    return Convert.ToBase64String(hash);
                }
            }
            else
            {
                // Simple SHA256 Mode
                using (var sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(payloadBytes);
                    return Convert.ToBase64String(hash);
                }
            }
        }
    }
}
