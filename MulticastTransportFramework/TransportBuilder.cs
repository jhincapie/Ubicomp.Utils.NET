#nullable enable
using System;
using System.Collections.Generic;
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
        private readonly List<Action<TransportComponent>> _registrations = new List<Action<TransportComponent>>();

        /// <summary>
        /// Configures the multicast socket options.
        /// </summary>
        public TransportBuilder WithMulticastOptions(MulticastSocketOptions options)
        {
            _options = options;
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

            var component = new TransportComponent(_options);

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

            return component;
        }
    }
}
