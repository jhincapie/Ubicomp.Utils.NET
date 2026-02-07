using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.Sockets;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Ubicomp.Utils.NET.MulticastTransportFramework.Components
{
    internal class GateKeeper : IDisposable
    {
        private Channel<GateCmd>? _gateInput;
        private Task? _gateLoopTask;
        private ILogger _logger;
        private ChannelWriter<SocketMessage>? _outputWriter;

        public ILogger Logger { get => _logger; set => _logger = value ?? NullLogger.Instance; }

        public TimeSpan GateKeeperTimeout { get; set; } = TimeSpan.FromMilliseconds(500);
        public int MaxQueueSize { get; set; } = 10000;

        private abstract class GateCmd { }
        private class InputMsgCmd : GateCmd
        {
            public SocketMessage Msg = null!;
        }
        private class TimeoutCmd : GateCmd
        {
            public int SeqId;
        }

        public GateKeeper(ILogger logger)
        {
            _logger = logger;
        }

        public void Start(ChannelWriter<SocketMessage> outputWriter)
        {
            Stop();
            _outputWriter = outputWriter ?? throw new ArgumentNullException(nameof(outputWriter));

            var channelOptions = new BoundedChannelOptions(MaxQueueSize)
            {
                FullMode = BoundedChannelFullMode.DropWrite
            };
            _gateInput = Channel.CreateBounded<GateCmd>(channelOptions);

            _gateLoopTask = Task.Run(GateKeeperLoop);
        }

        public void Stop()
        {
            _gateInput?.Writer.TryComplete();
            try { _gateLoopTask?.Wait(1000); } catch {}
            _gateLoopTask = null;
            _gateInput = null;
        }

        public bool TryPush(SocketMessage msg)
        {
            if (_gateInput == null) return false;
            return _gateInput.Writer.TryWrite(new InputMsgCmd { Msg = msg });
        }

        public void Dispose()
        {
            Stop();
        }

        private async Task GateKeeperLoop()
        {
            var pq = new PriorityQueue<SocketMessage>();
            int currentSeq = 1;
            CancellationTokenSource? gapCts = null;

            if (_gateInput == null || _outputWriter == null)
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
                                _logger.LogWarning("Received late message {0} (current is {1}). Ignoring.", msg.ArrivalSequenceId, currentSeq);
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
                                if (!_outputWriter.TryWrite(msg))
                                {
                                    _logger.LogWarning("Processing channel full. Dropping message {0}.", msg.ArrivalSequenceId);
                                    msg.Dispose();
                                }
                                else
                                {
                                    currentSeq++;
                                    // Check queue for next messages
                                    CheckQueue(pq, ref currentSeq, _outputWriter);
                                }
                            }
                            else
                            {
                                // Future message - Check Queue Size Limit
                                if (pq.Count >= MaxQueueSize)
                                {
                                    _logger.LogWarning("PriorityQueue full ({0}). Dropping future message {1} to prevent DoS.", pq.Count, msg.ArrivalSequenceId);
                                    msg.Dispose();
                                    continue;
                                }

                                pq.Enqueue(msg, msg.ArrivalSequenceId);
                                if (gapCts == null)
                                {
                                    gapCts = new CancellationTokenSource();
                                    var token = gapCts.Token;
                                    var captureSeq = currentSeq;

                                    // Capture writer locally to avoid closure issues if _gateInput changes (though it shouldn't while running)
                                    var writer = _gateInput.Writer;

                                    _ = Task.Delay(GateKeeperTimeout, token).ContinueWith(t =>
                                    {
                                        if (!t.IsCanceled)
                                        {
                                            writer.TryWrite(new TimeoutCmd { SeqId = captureSeq });
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
                                    _logger.LogWarning("Sequence gap detected. Timed out waiting for message {0}. Jumping to {1}.", currentSeq, priority);
                                    currentSeq = priority;
                                    gapCts = null;
                                    CheckQueue(pq, ref currentSeq, _outputWriter);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in GateKeeperLoop");
            }
        }

        private void CheckQueue(PriorityQueue<SocketMessage> pq, ref int currentSeq, ChannelWriter<SocketMessage> output)
        {
            while (pq.Count > 0 && pq.TryPeek(out var nextMsg, out var priority))
            {
                if (priority == currentSeq)
                {
                    pq.Dequeue();
                    if (!output.TryWrite(nextMsg))
                    {
                        _logger.LogWarning("Processing channel full. Dropping queued message {0}.", nextMsg.ArrivalSequenceId);
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
    }
}
