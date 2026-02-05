#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Fluent builder for creating and configuring a <see cref="TransportComponent"/>.
    /// </summary>
    public class TransportBuilder
    {
        private MulticastSocketOptions? _options;
        private ILoggerFactory? _loggerFactory;
        private EventSource? _localSource;
        private bool _autoSendAcks = false;
        private bool? _enforceOrdering;
        private string? _securityKey;
        private bool _encryptionEnabled = false;
        private IMulticastSocket? _socket;
        private readonly List<Action<TransportComponent>> _registrations = new List<Action<TransportComponent>>();

        /// <summary>
        /// Configures whether to enable AES-GCM encryption.
        /// Requires <see cref="WithSecurityKey"/> to be set.
        /// </summary>
        public TransportBuilder WithEncryption(bool enabled = true)
        {
            _encryptionEnabled = enabled;
            return this;
        }

        /// <summary>
        /// Configures the multicast socket options.
        /// </summary>
        public TransportBuilder WithMulticastOptions(MulticastSocketOptions options)
        {
            _options = options;
            return this;
        }

        /// <summary>
        /// Configures the specific socket instance to use (optional, for testing).
        /// </summary>
        public TransportBuilder WithSocket(IMulticastSocket socket)
        {
            _socket = socket;
            return this;
        }

        /// <summary>
        /// Configures whether to enforce strict message ordering.
        /// </summary>
        public TransportBuilder WithEnforceOrdering(bool enforce)
        {
            _enforceOrdering = enforce;
            return this;
        }

        /// <summary>
        /// Configures the logger factory for the component.
        /// </summary>
        public TransportBuilder WithLogging(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            return this;
        }

        /// <summary>
        /// Configures the local source identification.
        /// </summary>
        public TransportBuilder WithLocalSource(string resourceName, Guid? resourceId = null)
        {
            _localSource = new EventSource(resourceId ?? Guid.NewGuid(), resourceName);
            return this;
        }

        /// <summary>
        /// Configures whether to automatically send acknowledgements.
        /// </summary>
        public TransportBuilder WithAutoSendAcks(bool autoSend = true)
        {
            _autoSendAcks = autoSend;
            return this;
        }

        /// <summary>
        /// Configures the shared secret key for HMAC integrity.
        /// If not set, the component defaults to simple SHA256 integrity checks (no authentication).
        /// </summary>
        public TransportBuilder WithSecurityKey(string key)
        {
            _securityKey = key;
            return this;
        }

        /// <summary>
        /// Registers a handler for a specific message type.
        /// </summary>
        public TransportBuilder RegisterHandler<T>(string id, Action<T, MessageContext> handler) where T : class
        {
            _registrations.Add(component => component.RegisterHandler(id, handler));
            return this;
        }

        /// <summary>
        /// Registers a handler for a message type using the <see cref="MessageTypeAttribute"/>.
        /// </summary>
        public TransportBuilder RegisterHandler<T>(Action<T, MessageContext> handler) where T : class
        {
            _registrations.Add(component => component.RegisterHandler(handler));
            return this;
        }

        /// <summary>
        /// Builds and initializes the <see cref="TransportComponent"/>.
        /// </summary>
        public TransportComponent Build()
        {
            if (_options == null)
                throw new InvalidOperationException("Multicast options must be configured.");

            var component = new TransportComponent(_options, _socket);

            component.SecurityKey = _securityKey;
            component.EncryptionEnabled = _encryptionEnabled;

            if (_enforceOrdering.HasValue)
            {
                component.EnforceOrdering = _enforceOrdering.Value;
            }

            if (_loggerFactory != null)
            {
                component.Logger = _loggerFactory.CreateLogger<TransportComponent>();
            }

            if (_localSource != null)
            {
                component.LocalSource = _localSource;
            }

            component.AutoSendAcks = _autoSendAcks;

            foreach (var registration in _registrations)
            {
                registration(component);
            }

            AutoRegisterDiscoveredMessages(component);

            return component;
        }

        private void AutoRegisterDiscoveredMessages(TransportComponent component)
        {
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // Look for the specific generated type
                    var type = assembly.GetType("Ubicomp.Utils.NET.Generators.AutoDiscovery.TransportExtensions");
                    if (type != null)
                    {
                        var method = type.GetMethod("RegisterDiscoveredMessages", BindingFlags.Public | BindingFlags.Static);
                        if (method != null)
                        {
                            method.Invoke(null, new object[] { component });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                component.Logger?.LogWarning(ex, "Failed to auto-register discovered messages.");
            }
        }
    }
}
