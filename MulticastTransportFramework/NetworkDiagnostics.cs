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
            const int TestMessageType = 998; // Changed to avoid collision if any

            transport.RegisterHandler<LoopbackTestData>(TestMessageType, (data, context) =>
            {
                if (data.Id == testId)
                {
                    received = true;
                }
            });

            var content = new LoopbackTestData { Id = testId };
            transport.Send(content, new SendOptions { MessageType = TestMessageType });

            var start = DateTime.Now;
            while (!received &&
                   (DateTime.Now - start).TotalMilliseconds < timeoutMs)
            {
                Thread.Sleep(100);
            }

            return received;
        }

        /// <summary>Placeholder data for loopback diagnostics.</summary>
        public class LoopbackTestData
        {
            /// <summary>Gets or sets the diagnostic ID.</summary>
            public Guid Id
            {
                get; set;
            }
        }
    }
}
