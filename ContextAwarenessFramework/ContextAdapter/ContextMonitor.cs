using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ubicomp.Utils.NET.ContextAwarenessFramework.ContextAdapter
{

  public enum ContextAdapterUpdateType { Continous, Interval, OnRequest };

  public abstract class ContextMonitor
  {

    public event EventHandler OnStart;
    public event EventHandler OnStop;
    public event NotifyContextMonitorListeners OnNotifyContextServices;

    protected ContextAdapterUpdateType updateType = ContextAdapterUpdateType.Continous;
    protected int updateInterval = 3000;

    private bool stopped = false;

    public ContextAdapterUpdateType UpdateType
    {
      get { return updateType; }
      set { updateType = value; }
    }

    public int UpdateInterval
    {
      get { return updateInterval; }
      set { updateInterval = value; }
    }

    /// <summary>
    /// Launches the thread.
    /// </summary>
    public void Start() 
    {
      stopped = false;
      CustomStart();
      if (OnStart != null)
        OnStart(this, null);
    }

    protected virtual void CustomStart()
    { }

    /// <summary>
    /// Stops and kills the thread.
    /// </summary>
    public void Stop() 
    {
      stopped = true;
      CustomStop();
      if (OnStop != null)
        OnStop(this, null);
    }

    protected virtual void CustomStop()
    { }

    /// <summary>
    /// Here we control the kind of reading (continuos, interval).
    /// </summary>
    internal void Run()
    {
      if (updateType == ContextAdapterUpdateType.OnRequest)
        return;

      while (!stopped)
      {
        if (updateType == ContextAdapterUpdateType.Interval)
          Thread.Sleep(updateInterval);
        if (stopped)
          break;
        CustomRun();
      }
    }

    protected virtual void CustomRun()
    { }

    protected void NotifyContextServices(object sender, NotifyContextMonitorListenersEventArgs e)
    {
      if (OnNotifyContextServices != null)
        OnNotifyContextServices(sender, e);
    }

  }

}
