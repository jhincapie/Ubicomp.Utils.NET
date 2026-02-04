#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        private int _currentMessageCons = 1;
        private readonly object gate = new object();
        private bool _isStopping = false;
        private readonly PriorityQueue<SocketMessage> _waitingMessages = new PriorityQueue<SocketMessage>();
        private CancellationTokenSource? _gapTimeoutCts;

        /// <summary>Gets or sets the default timeout for acknowledgements.</summary>
        public TimeSpan DefaultAckTimeout { get; set; } = TimeSpan.FromSeconds(5);

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

            var builder = new MulticastSocketBuilder()
                .WithOptions(_socketOptions)
                .OnError(err => Logger.LogError(
                    "Socket Error: {0}. Exception: {1}",
                    err.Message,
                    err.Exception?.Message));

            // If we have a logger, we should try to get the factory to pass it down, 
            // but TransportComponent currently only holds ILogger.
            // Let's assume for now we might want to pass the Logger directly if we can,
            // or better, update TransportComponent to optionally hold the factory.
            // Given the current structure, let's pass the Logger to the socket.
            builder.WithLogger(Logger);

            _socket = builder.Build();

            lock (gate)
            {
                _currentMessageCons = 1;
                _isStopping = false;
                _waitingMessages.Clear();
                _gapTimeoutCts?.Cancel();
                _gapTimeoutCts = null;
                Monitor.PulseAll(gate);
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
            Logger.LogInformation("TransportComponent Initialized.");
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
                if (!_isStopping)
                {
                    Logger.LogError(ex, "Error in TransportComponent receive loop.");
                }
            }
        }

        /// <summary>
        /// Stops the transport component and closes the underlying socket.
        /// </summary>
        public void Stop()
        {
            lock (gate)
            {
                _isStopping = true;
                Monitor.PulseAll(gate);
            }

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

            string json = JsonSerializer.Serialize(message, _jsonOptions);

            if (_socket != null)
            {
                await _socket.SendAsync(json);
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
                try
                {
                    ProcessSingleMessage(msg);
                }
                finally
                {
                    msg.Dispose();
                }
                return;
            }

            lock (gate)
            {
                if (_isStopping)
                    return;

                if (msg.SequenceId < _currentMessageCons)
                {
                    Logger.LogWarning(
                        "Received late message {0} (current is {1}). Ignoring.",
                        msg.SequenceId,
                        _currentMessageCons);
                    msg.Dispose();
                    return;
                }

                if (msg.SequenceId == _currentMessageCons)
                {
                    // If we get the expected message, cancel any pending gap timeout
                    if (_gapTimeoutCts != null)
                    {
                        _gapTimeoutCts.Cancel();
                        _gapTimeoutCts = null;
                    }
                    // Process immediately
                }
                else
                {
                    // Future message
                    _waitingMessages.Enqueue(msg, msg.SequenceId);

                    // If we aren't already waiting for a gap to fill, start waiting
                    if (_gapTimeoutCts == null)
                    {
                        _gapTimeoutCts = new CancellationTokenSource();
                        var token = _gapTimeoutCts.Token;
                        _ = WaitForGapAsync(token);
                    }
                    return;
                }
            }

            // If we are here, we are processing the current message
            ProcessMessageAndSequence(msg);
        }

        private async Task WaitForGapAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(GateKeeperTimeout, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            SocketMessage? nextToProcess = null;
            lock (gate)
            {
                if (_isStopping || token.IsCancellationRequested)
                    return;

                _gapTimeoutCts = null;

                if (_waitingMessages.Count > 0 && _waitingMessages.TryPeek(out var nextMsg, out var priority))
                {
                    // Double check ordering (should be guaranteed by logic)
                    if (priority > _currentMessageCons)
                    {
                        Logger.LogWarning(
                            "Sequence gap detected. Timed out waiting for message {0}. Jumping to {1}.",
                            _currentMessageCons,
                            priority);

                        _currentMessageCons = priority;
                        nextToProcess = _waitingMessages.Dequeue();
                    }
                }
            }

            if (nextToProcess != null)
            {
                ProcessMessageAndSequence(nextToProcess);
            }
        }

        private void ProcessSingleMessage(SocketMessage msg)
        {
            try
            {
                string sMessage = Encoding.UTF8.GetString(msg.Data, 0, msg.Length);
                TransportMessage? tMessage;

                Logger.LogTrace("Importing message {0}", msg.SequenceId);
                tMessage = JsonSerializer.Deserialize<TransportMessage>(sMessage, _jsonOptions);

                if (tMessage != null)
                {
                    ProcessTransportMessage(tMessage, msg.SequenceId);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "Error Processing Received Message {0}: {1}",
                    msg.SequenceId,
                    ex.Message);
            }
        }

        private void ProcessMessageAndSequence(SocketMessage initialMsg)
        {
            SocketMessage? currentMsg = initialMsg;

            while (currentMsg != null)
            {
                try
                {
                    ProcessSingleMessage(currentMsg);
                }
                finally
                {
                    currentMsg.Dispose();
                }
                currentMsg = NudgeGateAndGetNext();
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

        private SocketMessage? NudgeGateAndGetNext()
        {
            lock (gate)
            {
                _currentMessageCons++;

                // Discard any messages that are now "late" (smaller than current)
                // This handles duplicates or old messages that were in the queue
                while (_waitingMessages.Count > 0 && _waitingMessages.TryPeek(out var msg, out var priority))
                {
                    if (priority < _currentMessageCons)
                    {
                        _waitingMessages.Dequeue(); // discard
                        continue;
                    }

                    if (priority == _currentMessageCons)
                    {
                        return _waitingMessages.Dequeue();
                    }

                    break;
                }

                Monitor.PulseAll(gate);
                return null;
            }
        }
    }
}
