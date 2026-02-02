using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Ubicomp.Utils.NET.ContextAwarenessFramework.ContextAdapter;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ubicomp.Utils.NET.ContextAwarenessFramework.ContextService
{

  public enum ContextServicePersistenceType { None, Periodic, OnRequest, Combined };

  public delegate void UpdateMonitorReadingDelegate(object sender, NotifyContextMonitorListenersEventArgs e);
  public delegate void ContextChangedDelegate(object sender, NotifyContextServiceListenersEventArgs e);

  public abstract class ContextService : IContextMonitorListener
  {
    // Exposed logger property, defaults to NullLogger
    public ILogger Logger { get; set; } = NullLogger.Instance;

    public event EventHandler? OnStart;
    public event EventHandler? OnStop;
    public event NotifyContextServiceListeners? OnNotifyContextServiceListeners;

    private ContextServicePersistenceType persistenceType = ContextServicePersistenceType.None;
    private int persistInterval = 60000;

    private bool persistRequested = false;
    private DateTime datePersistRequested = DateTime.MinValue;
    private bool stopped = false;



    protected ContextServicePersistenceType PersistenceType
    {
      get { return persistenceType; }
      set { persistenceType = value; }
    }

    public int PersistInterval
    {
      get { return persistInterval; }
      set { persistInterval = value; }
    }



    protected virtual void LoadEntities()
    { }

    protected virtual void PreparePersist()
    { }

    protected virtual void PersistEntities()
    { }

    protected void NotifyContextServiceListeners(object sender, NotifyContextServiceListenersEventArgs e)
    {
      OnNotifyContextServiceListeners?.Invoke(sender, e);
    }

    /// <summary>
    /// Launches the thread.
    /// </summary>
    public void Start()
    {
      CustomStart();
      OnStart?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void CustomStart()
    { }

    /// <summary>
    /// Stops and kills the thread.
    /// </summary>
    public void Stop()
    {
      stopped = true;
      ExecutePersit();
      CustomStop();
      OnStop?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void CustomStop()
    { }

    internal void Run()
    {
      if (persistenceType == ContextServicePersistenceType.None)
        return;

      while (!stopped)
      {
        try
        {
          if (persistenceType == ContextServicePersistenceType.Periodic ||
            persistenceType == ContextServicePersistenceType.Combined)
            Thread.Sleep(persistInterval);
          if (persistenceType == ContextServicePersistenceType.OnRequest)
            Thread.Sleep(500);
          if (stopped)
            break;

          ExecutePersit();
        }
        catch (ThreadAbortException)
        { }
      }
    }

    private object lockPersist = new object();
    private void ExecutePersit()
    {
      lock (lockPersist)
      {
        PreparePersist();
        if (persistenceType == ContextServicePersistenceType.Periodic)
        {
          PersistEntities();
        }
        else if (persistenceType == ContextServicePersistenceType.OnRequest && persistRequested)
        {
          PersistEntities();
          persistRequested = false;
        }
        else if (persistenceType == ContextServicePersistenceType.Combined && persistRequested)
        {
          PersistEntities();
          persistRequested = false;
        }
      }
    }

    public void RequestPersist()
    {
      persistRequested = true;
      datePersistRequested = DateTime.Now;
    }


    #region IContextMonitorListener Members

    /// <summary>
    /// THe implementation of this method pushes the execution of the monitoring update on the dispatcher
    /// thread that created the service. In this way, as the services are created by the UI thread, we can 
    /// make sure that the observable collections can be updated and that the change will be reflected in the
    /// UI components to which they are bound.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public void UpdateMonitorReading(object sender, NotifyContextMonitorListenersEventArgs e)
    {
      try
      {
        CustomUpdateMonitorReading(sender, e);
      }
      catch (Exception exception)
      {
        Logger.LogError(exception, "An error ocurred processing monitor reading");
      }
    }

    protected virtual void CustomUpdateMonitorReading(object sender, NotifyContextMonitorListenersEventArgs e)
    { }

    #endregion
  }

}
