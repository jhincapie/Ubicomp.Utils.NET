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
using Ubicomp.Utils.NET.MulticastTransportFramework.Components;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
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
        private const int MaxQueueSize = 10000;

        private IMulticastSocket? _socket;
        private readonly MulticastSocketOptions _socketOptions;
        public MulticastSocketOptions Options => _socketOptions;

        public bool IsRunning => _processingLoopTask != null && !_processingLoopTask.IsCompleted;

        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;

        private int _senderSequenceNumber = 0;
        private int _arrivalSequenceNumber = 0;

        private readonly ConcurrentDictionary<string, Type> _knownTypes = new ConcurrentDictionary<string, Type>();
        private readonly ConcurrentDictionary<string, Action<object, MessageContext>> _genericHandlers = new ConcurrentDictionary<string, Action<object, MessageContext>>();
        private readonly ConcurrentDictionary<Type, string> _typeToIdMap = new ConcurrentDictionary<Type, string>();

        // Components
        private readonly ReplayProtector _replayProtector;
        public ReplayProtector ReplayProtector => _replayProtector;

        private readonly AckManager _ackManager;
        public AckManager AckManager => _ackManager;

        private readonly PeerManager _peerManager;
        public PeerManager PeerManager => _peerManager;

        private readonly SecurityHandler _securityHandler;
        public SecurityHandler SecurityHandler => _securityHandler;

        private readonly MessageSerializer _messageSerializer;

        // Rx & Tracing
        private static readonly System.Diagnostics.ActivitySource _activitySource = new System.Diagnostics.ActivitySource("Ubicomp.Utils.NET.Transport");
        private readonly Subject<SocketMessage> _messageSubject = new Subject<SocketMessage>();
        public IObservable<SocketMessage> MessageStream => _messageSubject.AsObservable();

        public event EventHandler<MessageErrorEventArgs>? OnMessageError;

        public EventSource LocalSource { get; internal set; } = new EventSource(Guid.Empty, "Uninitialized");

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
            _securityHandler = new SecurityHandler(Logger);
            _messageSerializer = new MessageSerializer(Logger);

            // Wire up internal events
            _peerManager.OnHeartbeatTick += () => _replayProtector.Cleanup();

            RegisterInternalTypes();
        }

        private void UpdateComponentLoggers()
        {
            if (_replayProtector != null)
                _replayProtector.Logger = _logger;
            if (_ackManager != null)
                _ackManager.Logger = _logger;
            if (_peerManager != null)
                _peerManager.Logger = _logger;
            if (_securityHandler != null)
                _securityHandler.Logger = _logger;
            if (_messageSerializer != null)
                _messageSerializer.Logger = _logger;
        }

        private void RegisterInternalTypes()
        {
            // AckMessage has no handler (processed in pipeline), so we register it manually for deserialization
            _knownTypes.TryAdd(AckMessageType, typeof(AckMessageContent));

            // System handlers
            RegisterHandler<HeartbeatMessage>((msg, ctx) => _peerManager.HandleHeartbeat(msg, LocalSource.ResourceId.ToString()));
            RegisterHandler<RekeyMessage>(HandleRekey);
        }

        public void RegisterHandler<T>(Action<T, MessageContext> handler) where T : class
        {
            var attr = (MessageTypeAttribute?)Attribute.GetCustomAttribute(typeof(T), typeof(MessageTypeAttribute));
            if (attr == null)
                throw new InvalidOperationException($"Type {typeof(T).Name} does not have a [MessageType] attribute.");
            RegisterHandler(attr.MsgId, handler);
        }

        public void RegisterHandler<T>(string id, Action<T, MessageContext> handler) where T : class
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Message ID cannot be null or empty.", nameof(id));
            _genericHandlers[id] = (data, ctx) => handler((T)data, ctx);
            _typeToIdMap[typeof(T)] = id;
            _knownTypes[id] = typeof(T);
        }

        public void RegisterMessageType<T>(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Message ID cannot be null or empty.", nameof(id));
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
            _peerManager.Start(async msg => { await SendAsync(msg); }, LocalSource.ResourceId.ToString(), LocalSource.ResourceName);
        }

        private void HandleRekey(RekeyMessage msg, MessageContext context)
        {
            // Check if self
            if (string.Compare(LocalSource.ResourceId.ToString(), context.Source.ResourceId.ToString(), StringComparison.OrdinalIgnoreCase) == 0)
                return;

            Logger.LogInformation("Received Rekey Message (KeyId: {0}). Rotating keys...", msg.KeyId);
            _securityHandler.HandleRekey(msg.NewKey);

            Task.Delay(ReplayProtector.ReplayWindowDuration.Add(TimeSpan.FromSeconds(5))).ContinueWith(_ => _securityHandler.ClearPreviousKey());
        }

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

            _peerManager.Stop();
            _processingChannel?.Writer.TryComplete();
            try
            {
                _processingLoopTask?.Wait(1000);
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            _securityHandler.Dispose();
            _peerManager.Dispose();
            try
            {
                _messageSubject.OnCompleted();
                _messageSubject.Dispose();
            }
            catch { }
            GC.SuppressFinalize(this);
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
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (_receiveCts != null && !_receiveCts.IsCancellationRequested)
                    Logger.LogError(ex, "Error in ReceiveLoop");
            }
        }

        internal void HandleSocketMessage(SocketMessage msg)
        {
            if (!(_processingChannel?.Writer.TryWrite(msg) ?? false))
                msg.Dispose();
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
                            using (var activity = _activitySource.StartActivity("ReceiveMessage", System.Diagnostics.ActivityKind.Consumer))
                            {
                                int arrivalSeq = Interlocked.Increment(ref _arrivalSequenceNumber);
                                activity?.SetTag("transport.seq", arrivalSeq);
                                _messageSubject.OnNext(msg);
                                ProcessInboundPacket(msg, arrivalSeq);
                            }
                        }
                        finally { msg.Dispose(); }
                    }
                }
            }
            catch (Exception ex) { Logger.LogError(ex, "Critical error in ProcessingLoop"); }
        }

        /// <summary>
        /// Handles the raw incoming packet: Deserialization, Security Check, Replay Protection.
        /// </summary>
        private void ProcessInboundPacket(SocketMessage msg, int arrivalSeqNum)
        {
            try
            {
                // Bolt: Optimization - Early Rejection of Replays/Old Packets
                // Check header first before expensive deserialization and crypto.
                if (BinaryPacket.TryReadHeader(msg.Data.AsSpan(0, msg.Length), out var header))
                {
                    if (_replayProtector.IsKnownReplay(header.SourceId, header.SenderSequenceNumber, header.Ticks, out var earlyReason))
                    {
                        Logger.LogTrace("Dropped known replay/old message {0} (Seq {1}). Reason: {2}", header.SourceId, header.SenderSequenceNumber, earlyReason);
                        return;
                    }
                }
                else
                {
                    // Invalid header, drop immediately
                    return;
                }

                // Deserialize
                TransportMessage? tMessage = _messageSerializer.Deserialize(msg, _securityHandler);

                if (tMessage != null)
                {
                    var val = tMessage.Value;
                    val.ArrivalSequenceNumber = arrivalSeqNum;
                    tMessage = val;
                }

                if (tMessage == null)
                    return;

                TransportMessage message = tMessage.Value;

                // Replay Protection
                if (!_replayProtector.IsValid(message, out var reason))
                {
                    return;
                }

                DispatchMessage(message);
            }
            catch (Exception ex)
            {
                OnMessageError?.Invoke(this, new MessageErrorEventArgs(msg, "Inbound Packet Error", ex));
                Logger.LogError("Error Processing Received Message {0}: {1}", arrivalSeqNum, ex.Message);
            }
        }

        /// <summary>
        /// Routes the valid TransportMessage to appropriate handlers and manages ACKs.
        /// </summary>
        private void DispatchMessage(TransportMessage tMessage)
        {
            if (_knownTypes.TryGetValue(tMessage.MessageType, out Type? targetType))
            {
                try
                {
                    _messageSerializer.DeserializeContent(ref tMessage, targetType);
                }
                catch (Exception ex)
                {
                    OnMessageError?.Invoke(this, new MessageErrorEventArgs(null!, "Content Deserialization Error", ex));
                    Logger.LogError(ex, "Failed to deserialize message content");
                }
            }

            if (tMessage.MessageType == AckMessageType && tMessage.MessageData is AckMessageContent ackContent)
            {
                _ackManager.ProcessIncomingAck(ackContent.OriginalMessageId, tMessage.MessageSource);
            }

            Logger.LogTrace("Processing message {0}", tMessage.ArrivalSequenceNumber);
            bool handled = false;

            if (_genericHandlers.TryGetValue(tMessage.MessageType, out var handler))
            {
                // Bolt: Optimized using raw ticks to avoid string allocation
                var context = new MessageContext(tMessage.MessageId, tMessage.MessageSource, tMessage.Ticks, tMessage.RequestAck);
                // Optimized dispatch: using the wrapper delegate avoids the reflection overhead of DynamicInvoke
                handler(tMessage.MessageData, context);
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
            if (options?.MessageType != null)
                messageType = options.MessageType;
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
                message.SenderSequenceNumber = Interlocked.Increment(ref _senderSequenceNumber);
                _messageSerializer.SerializeToWriter(writer, message, _securityHandler);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to serialize message {0}", message.MessageId);
                throw;
            }

            if (_socket != null)
                await _socket.SendAsync(writer.WrittenMemory);

            if (!message.RequestAck)
                session.ReportAck(LocalSource);

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
            if (success)
                Logger.LogInformation("Network Diagnostics Passed.");
            else
                Logger.LogWarning("Network Diagnostics Failed.");
            return success;
        }
    }
}
