#nullable enable
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.Sockets;
using Ubicomp.Utils.NET.MulticastTransportFramework.Components;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    public class MessageErrorEventArgs : EventArgs
    {
        public SocketMessage RawMessage { get; }
        public Exception? Exception { get; }
        public string Reason { get; }

        public MessageErrorEventArgs(SocketMessage msg, string reason, Exception? ex = null)
        {
            RawMessage = msg;
            Reason = reason;
            Exception = ex;
        }
    }

    public class TransportComponent : IDisposable
    {
        private ILogger _logger = NullLogger.Instance;

        public ILogger Logger
        {
            get => _logger;
            set
            {
                _logger = value ?? NullLogger.Instance;
                UpdateComponentLoggers();
            }
        }

        public const int TransportComponentID = 0;
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

        // Components
        private readonly ReplayProtector _replayProtector;
        private readonly AckManager _ackManager;
        private readonly PeerManager _peerManager;
        private readonly GateKeeper _gateKeeper;
        private readonly SecurityHandler _securityHandler;
        private readonly MessageSerializer _messageSerializer;

        // Rx & Tracing
        private static readonly System.Diagnostics.ActivitySource _activitySource = new System.Diagnostics.ActivitySource("Ubicomp.Utils.NET.Transport");
        private readonly Subject<SocketMessage> _messageSubject = new Subject<SocketMessage>();
        public IObservable<SocketMessage> MessageStream => _messageSubject.AsObservable();

        public IEnumerable<RemotePeer> ActivePeers => _peerManager.ActivePeers;

        public event Action<RemotePeer> OnPeerDiscovered
        {
            add => _peerManager.OnPeerDiscovered += value;
            remove => _peerManager.OnPeerDiscovered -= value;
        }

        public event Action<RemotePeer> OnPeerLost
        {
            add => _peerManager.OnPeerLost += value;
            remove => _peerManager.OnPeerLost -= value;
        }

        public event EventHandler<MessageErrorEventArgs>? OnMessageError;

        // Configuration Properties (delegated)
        public TimeSpan DefaultAckTimeout { get => _ackManager.DefaultAckTimeout; set => _ackManager.DefaultAckTimeout = value; }
        public TimeSpan ReplayWindow { get => _replayProtector.ReplayWindowDuration; set => _replayProtector.ReplayWindowDuration = value; }
        public TimeSpan GateKeeperTimeout { get => _gateKeeper.GateKeeperTimeout; set => _gateKeeper.GateKeeperTimeout = value; }
        public EventSource LocalSource { get => _peerManager.LocalSource; set => _peerManager.LocalSource = value; }
        public bool AutoSendAcks { get => _ackManager.AutoSendAcks; set => _ackManager.AutoSendAcks = value; }
        public bool EnforceOrdering { get; set; } = false;
        public int MaxQueueSize { get => _gateKeeper.MaxQueueSize; set => _gateKeeper.MaxQueueSize = value; }

        public string? SecurityKey
        {
            get => _securityHandler.SecurityKey;
            set => _securityHandler.SecurityKey = value;
        }

        internal KeyManager KeyManager => _securityHandler.KeyManager;

        public bool EncryptionEnabled
        {
            get => _securityHandler.EncryptionEnabled;
            set => _securityHandler.EncryptionEnabled = value;
        }

        public TimeSpan? HeartbeatInterval
        {
            get => _peerManager.HeartbeatInterval;
            set => _peerManager.HeartbeatInterval = value;
        }

        public string? InstanceMetadata
        {
            get => _peerManager.InstanceMetadata;
            set => _peerManager.InstanceMetadata = value;
        }

        // Processing Channel
        private Channel<SocketMessage>? _processingChannel;
        private Task? _processingLoopTask;

        public TransportComponent(MulticastSocketOptions options, IMulticastSocket? socket = null)
        {
            _socketOptions = options;
            _socket = socket;

            _replayProtector = new ReplayProtector(Logger);
            _ackManager = new AckManager(Logger);
            _peerManager = new PeerManager(Logger);
            _gateKeeper = new GateKeeper(Logger);
            _securityHandler = new SecurityHandler(Logger);
            _messageSerializer = new MessageSerializer(Logger);

            // Wire up internal events
            _peerManager.OnHeartbeatTick += () => _replayProtector.Cleanup();

            RegisterInternalTypes();
        }

        private void UpdateComponentLoggers()
        {
            if (_replayProtector != null) _replayProtector.Logger = _logger;
            if (_ackManager != null) _ackManager.Logger = _logger;
            if (_peerManager != null) _peerManager.Logger = _logger;
            if (_gateKeeper != null) _gateKeeper.Logger = _logger;
            if (_securityHandler != null) _securityHandler.Logger = _logger;
            if (_messageSerializer != null) _messageSerializer.Logger = _logger;
        }

        private void RegisterInternalTypes()
        {
            _knownTypes.TryAdd(AckMessageType, typeof(AckMessageContent));
            _knownTypes.TryAdd("sys.heartbeat", typeof(HeartbeatMessage));
            _typeToIdMap.TryAdd(typeof(HeartbeatMessage), "sys.heartbeat");
            _knownTypes.TryAdd("sys.rekey", typeof(RekeyMessage));
            _typeToIdMap.TryAdd(typeof(RekeyMessage), "sys.rekey");
        }

        public void RegisterHandler<T>(Action<T, MessageContext> handler) where T : class
        {
            var attr = (MessageTypeAttribute?)Attribute.GetCustomAttribute(typeof(T), typeof(MessageTypeAttribute));
            if (attr == null) throw new InvalidOperationException($"Type {typeof(T).Name} does not have a [MessageType] attribute.");
            RegisterHandler(attr.MsgId, handler);
        }

        public void RegisterHandler<T>(string id, Action<T, MessageContext> handler) where T : class
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Message ID cannot be null or empty.", nameof(id));
            _genericHandlers[id] = handler;
            _typeToIdMap[typeof(T)] = id;
            _knownTypes[id] = typeof(T);
        }

        public void RegisterMessageType<T>(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Message ID cannot be null or empty.", nameof(id));
            _typeToIdMap[typeof(T)] = id;
            _knownTypes[id] = typeof(T);
        }

        public void Start()
        {
            Stop();

            _receiveCts = new CancellationTokenSource();

            var channelOptions = new BoundedChannelOptions(MaxQueueSize) { FullMode = BoundedChannelFullMode.DropWrite };
            _processingChannel = Channel.CreateBounded<SocketMessage>(channelOptions);

            _processingLoopTask = Task.Run(ProcessingLoop);

            // GateKeeper
            _gateKeeper.Start(_processingChannel.Writer);

            if (_socket == null)
            {
                 var builder = new MulticastSocketBuilder().WithOptions(_socketOptions).OnError(err => Logger.LogError("Socket Error: {0}", err.Message));
                 builder.WithLogger(Logger);
                 _socket = builder.Build();
            }

            _socket.StartReceiving();
            _receiveTask = Task.Run(async () => await ReceiveLoop(_receiveCts.Token));

            Logger.LogInformation("Multicast Socket Started on {0}:{1}", _socketOptions.GroupAddress, _socketOptions.Port);

            // PeerManager
            _peerManager.Start(async msg => { await SendAsync(msg); });

            // Handlers for system messages
            RegisterHandler<HeartbeatMessage>("sys.heartbeat", (msg, ctx) => _peerManager.HandleHeartbeat(msg));
            RegisterHandler<RekeyMessage>("sys.rekey", HandleRekey);
        }

        private void HandleRekey(RekeyMessage msg, MessageContext context)
        {
             // Check if self
             if (string.Compare(LocalSource.ResourceId.ToString(), context.Source.ResourceId.ToString(), StringComparison.OrdinalIgnoreCase) == 0) return;

             Logger.LogInformation("Received Rekey Message (KeyId: {0}). Rotating keys...", msg.KeyId);
             _securityHandler.HandleRekey(msg.NewKey);

             Task.Delay(ReplayWindow.Add(TimeSpan.FromSeconds(5))).ContinueWith(_ => _securityHandler.ClearPreviousKey());
        }

        public void Stop()
        {
            _receiveCts?.Cancel();
            try { _receiveTask?.Wait(500); } catch { }
            _receiveCts?.Dispose();
            _receiveCts = null;

            if (_socket != null)
            {
                _socket.Close();
                _socket.Dispose();
                _socket = null;
            }

            _gateKeeper.Stop();
            _peerManager.Stop();
            _processingChannel?.Writer.TryComplete();
            try { _processingLoopTask?.Wait(1000); } catch { }
        }

        public void Dispose()
        {
            Stop();
            _securityHandler.Dispose();
            _peerManager.Dispose();
            _gateKeeper.Dispose();
            try { _messageSubject.OnCompleted(); _messageSubject.Dispose(); } catch { }
            GC.SuppressFinalize(this);
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
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (_receiveCts != null && !_receiveCts.IsCancellationRequested) Logger.LogError(ex, "Error in ReceiveLoop");
            }
        }

        internal void HandleSocketMessage(SocketMessage msg)
        {
            if (!EnforceOrdering)
            {
                if (!(_processingChannel?.Writer.TryWrite(msg) ?? false)) msg.Dispose();
            }
            else
            {
                if (!_gateKeeper.TryPush(msg)) msg.Dispose();
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
                            using (var activity = _activitySource.StartActivity("ReceiveMessage", System.Diagnostics.ActivityKind.Consumer))
                            {
                                activity?.SetTag("transport.seq", msg.ArrivalSequenceId);
                                _messageSubject.OnNext(msg);
                                ProcessSingleMessage(msg);
                            }
                        }
                        finally { msg.Dispose(); }
                    }
                }
            }
            catch (Exception ex) { Logger.LogError(ex, "Critical error in ProcessingLoop"); }
        }

        private void ProcessSingleMessage(SocketMessage msg)
        {
            try
            {
                // Deserialize
                TransportMessage? tMessage = _messageSerializer.Deserialize(msg, _securityHandler, out int senderSequenceId);

                if (tMessage == null)
                    return;

                TransportMessage message = tMessage.Value;

                // Replay Protection
                if (!_replayProtector.IsValid(message, senderSequenceId, out var reason))
                {
                    return;
                }

                // Legacy Signature Check (if not binary magic byte)
                if (msg.Length > 0 && msg.Data[0] != BinaryPacket.MagicByte)
                {
                    if (!ValidateLegacySignature(message, msg))
                        return;
                }

                // Legacy Decryption (if string cipher)
                if (message.IsEncrypted && message.MessageData is string cipherText && message.Nonce != null)
                {
                    if (!TryDecryptLegacy(ref message, cipherText, msg))
                        return;
                }

                ProcessTransportMessage(message, msg.ArrivalSequenceId);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error Processing Received Message {0}: {1}", msg.ArrivalSequenceId, ex.Message);
            }
        }

        private bool ValidateLegacySignature(TransportMessage message, SocketMessage msg)
        {
             if (string.IsNullOrEmpty(message.Signature))
             {
                 Logger.LogWarning("Dropped unsigned message {0}.", message.MessageId);
                 OnMessageError?.Invoke(this, new MessageErrorEventArgs(msg, "Missing Signature"));
                 return false;
             }

             byte[]? tempInt = _securityHandler.CurrentSession?.IntegrityKey.Memory.ToArray();
             string expected = _messageSerializer.ComputeSignature(message, tempInt);
             if (tempInt != null) Array.Clear(tempInt, 0, tempInt.Length);

             if (message.Signature != expected)
             {
                 Logger.LogWarning("Dropped message {0} with invalid signature.", message.MessageId);
                 OnMessageError?.Invoke(this, new MessageErrorEventArgs(msg, "Invalid Signature"));
                 return false;
             }
             return true;
        }

        private bool TryDecryptLegacy(ref TransportMessage message, string cipherText, SocketMessage msg)
        {
            try
            {
                string plainText = _securityHandler.Decrypt(cipherText, message.Nonce!, message.Tag);
                message.MessageData = plainText;
                return true;
            }
            catch (Exception ex)
            {
                 Logger.LogError(ex, "Decryption failed for message {0}.", message.MessageId);
                 OnMessageError?.Invoke(this, new MessageErrorEventArgs(msg, "Decryption Failed", ex));
                 return false;
            }
        }

        private void ProcessTransportMessage(TransportMessage tMessage, int sequenceId)
        {
            if (_knownTypes.TryGetValue(tMessage.MessageType, out Type? targetType))
            {
                try { _messageSerializer.DeserializeContent(ref tMessage, targetType); }
                catch (Exception ex) { Logger.LogError(ex, "Failed to deserialize message content"); }
            }

            if (tMessage.MessageType == AckMessageType && tMessage.MessageData is AckMessageContent ackContent)
            {
                _ackManager.ProcessIncomingAck(ackContent.OriginalMessageId, tMessage.MessageSource);
            }

            Logger.LogTrace("Processing message {0}", sequenceId);
            bool handled = false;

            if (_genericHandlers.TryGetValue(tMessage.MessageType, out var handler))
            {
                var context = new MessageContext(tMessage.MessageId, tMessage.MessageSource, tMessage.TimeStamp, tMessage.RequestAck);
                handler.DynamicInvoke(tMessage.MessageData, context);
                handled = true;
            }

            if (handled && _ackManager.ShouldAutoSendAck(tMessage, _replayProtector))
            {
                Logger.LogTrace("Automatically sending Ack for message {0}", tMessage.MessageId);
                _ = SendAckAsync(tMessage.MessageId);
            }
        }

        public async Task<AckSession> SendAsync<T>(T content, SendOptions? options = null) where T : class
        {
             string? messageType;
             if (options?.MessageType != null) messageType = options.MessageType;
             else if (!_typeToIdMap.TryGetValue(typeof(T), out messageType))
             {
                 var attr = (MessageTypeAttribute?)Attribute.GetCustomAttribute(typeof(T), typeof(MessageTypeAttribute));
                 messageType = attr?.MsgId ?? throw new ArgumentException($"Type {typeof(T).Name} is not registered.");
             }

             var message = new TransportMessage(LocalSource, messageType, content)
             {
                 RequestAck = options?.RequestAck ?? false
             };
             return await SendInternalAsync(message, options?.AckTimeout);
        }

        private async Task<AckSession> SendInternalAsync(TransportMessage message, TimeSpan? ackTimeout)
        {
            var session = _ackManager.CreateSession(message.MessageId, ackTimeout);

            using var activity = _activitySource.StartActivity("SendMessage", System.Diagnostics.ActivityKind.Producer);
            activity?.SetTag("message.type", message.MessageType);
            activity?.SetTag("message.id", message.MessageId.ToString());

            var writer = new Ubicomp.Utils.NET.MulticastTransportFramework.ArrayBufferWriter<byte>();

            try
            {
                _messageSerializer.SerializeToWriter(writer, message, 0, _securityHandler);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to serialize message {0}", message.MessageId);
                throw;
            }

            if (_socket != null) await _socket.SendAsync(writer.WrittenMemory);

            if (!message.RequestAck) session.ReportAck(LocalSource);

            return session;
        }

        public Task SendAckAsync(MessageContext context) => SendAckAsync(context.MessageId);

        private Task SendAckAsync(Guid originalMessageId)
        {
            var ackContent = new AckMessageContent { OriginalMessageId = originalMessageId };
            var ackMessage = new TransportMessage(LocalSource, AckMessageType, ackContent) { RequestAck = false };
            return SendInternalAsync(ackMessage, null);
        }

        public async Task<bool> VerifyNetworkingAsync()
        {
            Logger.LogInformation("Performing Network Diagnostics...");
            NetworkDiagnostics.LogFirewallStatus(_socketOptions.Port, Logger);
            bool success = await NetworkDiagnostics.PerformLoopbackTestAsync(this);
            if (success) Logger.LogInformation("Network Diagnostics Passed.");
            else Logger.LogWarning("Network Diagnostics Failed.");
            return success;
        }

        internal string ComputeSignature(TransportMessage message, byte[]? keyBytes)
        {
            return _messageSerializer.ComputeSignature(message, keyBytes);
        }
    }
}
