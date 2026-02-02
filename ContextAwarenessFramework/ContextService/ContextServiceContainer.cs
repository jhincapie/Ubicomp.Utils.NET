#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ubicomp.Utils.NET.ContextAwarenessFramework.ContextService
{

    /// <summary>
    /// Manages a collection of <see cref="ContextService"/> instances and their
    /// lifecycles.
    /// </summary>
    public abstract class ContextServiceContainer
    {
        /// <summary>Occurs when services are being initialized.</summary>
        public static event EventHandler? OnInitialize;
        /// <summary>Occurs when services are being finalized.</summary>
        public static event EventHandler? OnFinalize;

        private static bool _servicesStarted = false;
        private static readonly List<ContextService> _services =
            new List<ContextService>();
        private static readonly Dictionary<ContextService, Thread> _threads =
            new Dictionary<ContextService, Thread>();
        private static readonly Dictionary<Type, List<ContextService>>
            _serviceMap = new Dictionary<Type, List<ContextService>>();
        private static readonly object _syncRoot = new object();

        /// <summary>
        /// Registers a new service and prepares its background thread.
        /// </summary>
        /// <param name="service">The service to add.</param>
        public static void AddContextService(ContextService service)
        {
            Thread serviceThread =
                new Thread(service.Run) { IsBackground = true };

            lock (_syncRoot)
            {
                if (_services.Contains(service))
                    return;

                _services.Add(service);
                _threads.Add(service, serviceThread);

                Type serviceType = service.GetType();
                if (!_serviceMap.ContainsKey(serviceType))
                {
                    _serviceMap[serviceType] = new List<ContextService>();
                }
                _serviceMap[serviceType].Add(service);

                if (_servicesStarted)
                {
                    serviceThread.Start();
                    service.Start();
                }
            }
        }

        /// <summary>
        /// Retrieves registered services of the specified type.
        /// </summary>
        /// <param name="serviceType">The type of the service.</param>
        /// <returns>A list of registered service instances.</returns>
        public static List<ContextService> GetContextService(Type serviceType)
        {
            if (serviceType == null)
                return new List<ContextService>();

            lock (_syncRoot)
            {
                if (_serviceMap.TryGetValue(serviceType,
                                            out var registeredServices))
                {
                    // Return a shallow copy of the list to avoid external
                    // modification issues
                    return new List<ContextService>(registeredServices);
                }
                return new List<ContextService>();
            }
        }

        /// <summary>
        /// Starts all registered services.
        /// </summary>
        public static void StartServices()
        {
            OnInitialize?.Invoke(null, EventArgs.Empty);

            lock (_syncRoot)
            {
                foreach (ContextService service in _services)
                {
                    Thread serviceThread = _threads[service];
                    serviceThread.Start();
                    service.Start();
                }

                _servicesStarted = true;
            }
        }

        /// <summary>
        /// Stops all registered services.
        /// </summary>
        public static void StopServices()
        {
            OnFinalize?.Invoke(null, EventArgs.Empty);

            lock (_syncRoot)
            {
                foreach (ContextService service in _services)
                {
                    service.Stop();
                    Thread serviceThread = _threads[service];
                    // Join the thread to allow it to exit gracefully.
                    // service.Stop() signals the loop to terminate.
                    if (!serviceThread.Join(500))
                    {
                        // If it doesn't exit in time, we let it background or
                        // could log a warning.
                    }
                }

                _servicesStarted = false;
            }
        }
    }

}
