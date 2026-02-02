using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    public static class NetworkDiagnostics
    {
        public static void LogFirewallStatus(int port, ILogger logger)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                CheckLinuxFirewall(port, logger);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                CheckWindowsFirewall(port, logger);
            }
        }

        private static void CheckLinuxFirewall(int port, ILogger logger)
        {
            // Check ufw
            if (IsServiceActive("ufw"))
            {
                string status = ExecuteCommand("ufw", "status") ?? "";
                if (status.Contains("Status: active"))
                {
                    logger.LogInformation("Firewall 'ufw' is active. Ensure port {0}/udp is allowed.", port);
                    if (!status.Contains(port.ToString() + "/udp"))
                    {
                        logger.LogInformation("Port {0}/udp not explicitly found in 'ufw status'.", port);
                    }
                }
            }

            // Check firewalld
            if (IsServiceActive("firewalld"))
            {
                logger.LogInformation("Firewall 'firewalld' is active. Ensure port {0}/udp is allowed.", port);
                string state = ExecuteCommand("firewall-cmd", "--state") ?? "";
                if (state.Trim() == "running")
                {
                    string ports = ExecuteCommand("firewall-cmd", "--list-ports") ?? "";
                    if (!ports.Contains(port.ToString() + "/udp"))
                    {
                        logger.LogInformation("Port {0}/udp not explicitly found in 'firewall-cmd --list-ports'.", port);
                    }
                }
            }
        }

        private static void CheckWindowsFirewall(int port, ILogger logger)
        {
            string status = ExecuteCommand("netsh", "advfirewall show currentprofile") ?? "";
            if (status.Contains("ON"))
            {
                logger.LogInformation("Windows Firewall is ON. Ensure port {0}/udp is allowed.", port);
            }
        }

        private static bool IsServiceActive(string serviceName)
        {
            string output = ExecuteCommand("systemctl", $"is-active {serviceName}") ?? "";
            return output.Trim() == "active";
        }

        private static string? ExecuteCommand(string command, string arguments)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Performs a loopback test by sending a multicast message and waiting for it.
        /// </summary>
        public static bool PerformLoopbackTest(TransportComponent transport, int timeoutMs = 2000)
        {
            bool received = false;
            var testId = Guid.NewGuid();
            
            ITransportListener testListener = new TestListener((msg, raw) => {
                if (msg.MessageData is LoopbackTestData data && data.Id == testId)
                {
                    received = true;
                }
            });

            const int TestMessageType = 999;
            transport.TransportListeners[TestMessageType] = testListener;
            TransportMessageConverter.KnownTypes[TestMessageType] = typeof(LoopbackTestData);

            var source = new EventSource(Guid.NewGuid(), Environment.MachineName, "DiagnosticSource");
            var message = new TransportMessage(source, TestMessageType, new LoopbackTestData { Id = testId });
            
            transport.Send(message);

            var start = DateTime.Now;
            while (!received && (DateTime.Now - start).TotalMilliseconds < timeoutMs)
            {
                Thread.Sleep(100);
            }

            transport.TransportListeners.Remove(TestMessageType);
            return received;
        }

        private class TestListener : ITransportListener
        {
            private readonly Action<TransportMessage, string> _callback;
            public TestListener(Action<TransportMessage, string> callback) => _callback = callback;
            public void MessageReceived(TransportMessage message, string rawMessage) => _callback(message, rawMessage);
        }

        public class LoopbackTestData : ITransportMessageContent
        {
            public Guid Id { get; set; }
        }
    }
}