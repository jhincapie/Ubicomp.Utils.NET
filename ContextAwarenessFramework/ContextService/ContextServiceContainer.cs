using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ubicomp.Utils.NET.ContextAwarenessFramework.ContextService
{

  public abstract class ContextServiceContainer
  {

    public static event EventHandler OnInitialize;
    public static event EventHandler OnFinalize;

    private static bool servicesStarted = false;
    private static List<ContextService> services = new List<ContextService>();
    private static Dictionary<ContextService, Thread> threadsHT = new Dictionary<ContextService, Thread>();
    private static Dictionary<Type, List<ContextService>> _serviceMap = new Dictionary<Type, List<ContextService>>();
    private static readonly object _syncRoot = new object();

    public static void AddContextService(ContextService service)
    {
      ThreadStart serviceStart = new ThreadStart(service.Run);
      Thread serviceThread = new Thread(serviceStart);
      serviceThread.IsBackground = true;

      lock (_syncRoot)
      {
        if (!services.Contains(service))
        {
          services.Add(service);
          threadsHT.Add(service, serviceThread);

          Type serviceType = service.GetType();
          if (!_serviceMap.ContainsKey(serviceType))
          {
            _serviceMap[serviceType] = new List<ContextService>();
          }
          _serviceMap[serviceType].Add(service);
        }

        if (servicesStarted)
        {
          serviceThread.Start();
          service.Start();
        }
      }
    }

    public static List<ContextService> GetContextService(Type contextServiceType)
    {
      if (contextServiceType == null)
        return new List<ContextService>();

      lock (_syncRoot)
      {
        if (_serviceMap.ContainsKey(contextServiceType))
        {
          // Return a shallow copy of the list to avoid external modification issues
          return new List<ContextService>(_serviceMap[contextServiceType]);
        }
        return new List<ContextService>();
      }
    }

    public static void StartServices()
    {
      if (OnInitialize != null)
        OnInitialize(null, EventArgs.Empty);

      lock (_syncRoot)
      {
        foreach (ContextService service in services)
        {
          Thread serviceThread = threadsHT[service];
          serviceThread.Start();
          service.Start();
        }

        servicesStarted = true;
      }
    }

    public static void StopServices()
    {
      if (OnFinalize != null)
        OnFinalize(null, EventArgs.Empty);

      lock (_syncRoot)
      {
        foreach (ContextService service in services)
        {
          service.Stop();
          Thread serviceThread = threadsHT[service];
          serviceThread.Abort();
        }

        servicesStarted = false;
      }
    }

  }

}
