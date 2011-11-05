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

    public static void AddContextService(ContextService service)
    {
      ThreadStart serviceStart = new ThreadStart(service.Run);
      Thread serviceThread = new Thread(serviceStart);
      serviceThread.IsBackground = true;

      if (!services.Contains(service))
      {
        services.Add(service);
        threadsHT.Add(service, serviceThread);
      }

      if (servicesStarted)
      {
        serviceThread.Start();
        service.Start();
      }
    }

    public static ContextService GetContextService(Type contextServiceType)
    {
      foreach (ContextService service in services)
      {
        if (service.GetType() == contextServiceType)
          return service;
      }
      return null;
    }

    public static void StartServices()
    {
      if (OnInitialize != null)
        OnInitialize(null, EventArgs.Empty);

      foreach (ContextService service in services)
      {
        Thread serviceThread = threadsHT[service];
        serviceThread.Start();
        service.Start();
      }

      servicesStarted = true;
    }

    public static void StopServices()
    {
      if (OnFinalize != null)
        OnFinalize(null, EventArgs.Empty);

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
