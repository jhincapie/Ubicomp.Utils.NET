#nullable enable
using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Provides utility methods for diagnosing network issues, specifically
    /// focused on multicast and firewall configuration.
    /// </summary>
    public static class NetworkDiagnostics
    {
        /// <summary>
        /// Probes the system for firewall status and logs relevant info.
        /// </summary>
        /// <param name="port">The port to check.</param>
        /// <param name="logger">The logger instance.</param>
        public static void LogFirewallStatus(int port, ILogger? logger)
        {
            if (logger == null)
            {
                Console.WriteLine($"--- Firewall Diagnostic for Port {port} ---");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                CheckLinuxFirewall(port, logger);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                CheckWindowsFirewall(port, logger);
            }

            if (logger == null)
            {
                Console.WriteLine("-----------------------------------------");
            }
        }

        private static void CheckLinuxFirewall(int port, ILogger? logger)
        {
            // Check ufw
            if (IsServiceActive("ufw"))
            {
                string status = ExecuteCommand("ufw", "status") ?? "";
                if (status.Contains("Status: active"))
                {
                    string msg = $"Firewall 'ufw' is active. Ensure port {port}/udp is allowed.";
                    if (logger != null)
                        logger.LogInformation(msg);
                    else
                        Console.WriteLine(msg);

                    if (!status.Contains(port.ToString() + "/udp"))
                    {
                        string warn = $"Port {port}/udp not explicitly found in 'ufw status'.";
                        if (logger != null)
                            logger.LogInformation(warn);
                        else
                            Console.WriteLine(warn);
                    }
                }
            }

            // Check firewalld
            if (IsServiceActive("firewalld"))
            {
                string msg = $"Firewall 'firewalld' is active. Ensure port {port}/udp is allowed.";
                if (logger != null)
                    logger.LogInformation(msg);
                else
                    Console.WriteLine(msg);

                string state = ExecuteCommand("firewall-cmd", "--state") ?? "";
                if (state.Trim() == "running")
                {
                    string ports =
                        ExecuteCommand("firewall-cmd", "--list-ports") ?? "";
                    if (!ports.Contains(port.ToString() + "/udp"))
                    {
                        string warn = $"Port {port}/udp not explicitly found in 'firewall-cmd --list-ports'.";
                        if (logger != null)
                            logger.LogInformation(warn);
                        else
                            Console.WriteLine(warn);
                    }
                }
            }
        }

        private static void CheckWindowsFirewall(int port, ILogger? logger)
        {
            string status =
                ExecuteCommand("netsh", "advfirewall show currentprofile") ??
                "";
            if (status.Contains("ON"))
            {
                string msg = $"Windows Firewall is ON. Checking for port {port}/udp...";
                if (logger != null)
                    logger.LogInformation(msg);
                else
                    Console.WriteLine(msg);

                string ruleCheck = ExecuteCommand("netsh", $"advfirewall firewall show rule name=all") ?? "";
                if (ruleCheck.Contains(port.ToString()) && ruleCheck.Contains("UDP"))
                {
                    string info = $"Found firewall rule referencing port {port}/UDP.";
                    if (logger != null)
                        logger.LogInformation(info);
                    else
                        Console.WriteLine(info);
                }
                else
                {
                    string warn = $"No explicit firewall rule found for port {port}/UDP. Multicast might be blocked.";
                    if (logger != null)
                        logger.LogWarning(warn);
                    else
                        Console.WriteLine(warn);
                }
            }
        }
        private static bool IsServiceActive(string serviceName)
        {
            try
            {
                string output =
                    ExecuteCommand("systemctl", $"is-active {serviceName}") ?? "";
                return output.Trim() == "active";
            }
            catch
            {
                // Fallback: check if the command itself exists and returns something
                string? status = serviceName == "ufw" ? ExecuteCommand("ufw", "status") : null;
                return status != null && status.Contains("Status: active");
            }
        }

        private static string? ExecuteCommand(string command, string arguments)
        {
            try
            {
                using var process =
                    new Process
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
            catch (Exception e)
            {
                Console.Error.WriteLine(
                    $"Error executing diagnostic command '{command}': {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Performs a loopback test by sending a multicast message and waiting
        /// for it.
        /// </summary>
        /// <param name="transport">The transport component to test.</param>
        /// <param name="timeoutMs">The timeout in milliseconds.</param>
        /// <returns>True if the message was received, otherwise
        /// false.</returns>
        public static bool PerformLoopbackTest(TransportComponent transport,
                                               int timeoutMs = 2000)
        {
            bool received = false;
            var testId = Guid.NewGuid();

            ITransportListener testListener = new TestListener(
                (msg, raw) =>
                {
                    if (msg.MessageData is LoopbackTestData data &&
                        data.Id == testId)
                    {
                        received = true;
                    }
                });

            const int TestMessageType = 999;
            transport.TransportListeners[TestMessageType] = testListener;
            TransportMessageConverter.KnownTypes[TestMessageType] =
                typeof(LoopbackTestData);

            var source = new EventSource(
                Guid.NewGuid(), Environment.MachineName, "DiagnosticSource");
            var message = new TransportMessage(
                source, TestMessageType, new LoopbackTestData { Id = testId });

            transport.Send(message);

            var start = DateTime.Now;
            while (!received &&
                   (DateTime.Now - start).TotalMilliseconds < timeoutMs)
            {
                Thread.Sleep(100);
            }

            transport.TransportListeners.Remove(TestMessageType);
            return received;
        }

        private class TestListener : ITransportListener
        {
            private readonly Action<TransportMessage, string> _callback;
            public TestListener(Action<TransportMessage, string> callback) =>
                _callback = callback;
            public void MessageReceived(TransportMessage message,
                                        string rawMessage) =>
                _callback(message, rawMessage);
        }

        /// <summary>Placeholder data for loopback diagnostics.</summary>
        public class LoopbackTestData : ITransportMessageContent
        {
            /// <summary>Gets or sets the diagnostic ID.</summary>
            public Guid Id
            {
                get; set;
            }
        }
    }
}
