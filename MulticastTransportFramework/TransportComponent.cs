using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Newtonsoft.Json;
using System.IO;
using System.Threading;
using Ubicomp.Utils.NET.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{

  public class TransportComponent : ITransportListener
  {
    // Use NullLogger by default to avoid null checks
    public ILogger Logger { get; set; } = NullLogger.Instance;

    public const int TransportComponentID = 0;
    public static TransportComponent Instance = new TransportComponent();

    private static object importLock = new object();
    private static object exportLock = new object();

    private MulticastSocket socket = null!;
    private IPAddress address = null!;
    private IPAddress? localAddress;
    private int port;
    private int udpTTL;

    public Dictionary<Int32, ITransportListener> TransportListeners = new Dictionary<int, ITransportListener>();

    private JsonSerializerSettings jsonSettings;

    private static Int32 currentMessageCons = 0;

    public IPAddress MulticastGroupAddress
    {
      get { return address; }
      set { address = value; }
    }

    public IPAddress? LocalIPAddress
    {
      get { return localAddress; }
      set { localAddress = value; }
    }

    public int Port
    {
      get { return port; }
      set { port = value; }
    }

    public int UDPTTL
    {
      get { return udpTTL; }
      set { udpTTL = value; }
    }

    private TransportComponent()
    {
      jsonSettings = new JsonSerializerSettings();
      jsonSettings.Converters.Add(new TransportMessageConverter());

      TransportListeners.Add(TransportComponent.TransportComponentID, this);
    }

    public void Init()
    {
      if (address == null)
        throw new ApplicationException("Multicast group address not specified.");
      if (port == 0)
        throw new ApplicationException("Multicast group port not specified.");

      socket = new MulticastSocket(address.ToString(), port, udpTTL, localAddress?.ToString());
      socket.OnNotifyMulticastSocketListener += new NotifyMulticastSocketListener(socket_OnNotifyMulticastSocketListener);

      currentMessageCons = 1;
      socket.StartReceiving();

      Logger.LogInformation("Multicast Sockect Started to Listen for Traffic.");
      Logger.LogInformation("TransportComponent Initialized.");
    }

    public bool VerifyNetworking()
    {
        Logger.LogInformation("Performing Network Diagnostics...");
        NetworkDiagnostics.LogFirewallStatus(port, Logger);
        
        bool success = NetworkDiagnostics.PerformLoopbackTest(this);
        if (success)
        {
            Logger.LogInformation("Network Diagnostics Passed: Multicast Loopback Successful.");
        }
        else
        {
            Logger.LogWarning("Network Diagnostics Failed: Multicast Loopback NOT received. Check firewall settings and interface configuration.");
        }
        return success;
    }

    public string Send(TransportMessage message)
    {
      string json;
      lock (exportLock)
        json = JsonConvert.SerializeObject(message, jsonSettings);

      socket.Send(json);
      return json;
    }

    void socket_OnNotifyMulticastSocketListener(object sender, NotifyMulticastSocketListenerEventArgs e)
    {
      if (e.Type != MulticastSocketMessageType.MessageReceived)
      {
        if (e.Type == MulticastSocketMessageType.SendException)
        {
          Logger.LogError("Error Sending Message: {0}", e.NewObject);
        }
        else if (e.Type == MulticastSocketMessageType.ReceiveException)
        {
          Logger.LogError("Error Receiving Message: {0}", e.NewObject);
        }
        return;
      }

      bool enteredGate = false;
      try
      {
        if (e.NewObject == null) return;
        string sMessage = GetMessageAsString((byte[])e.NewObject);

        TransportMessage? tMessage = null;
        lock (importLock)
        {
          Logger.LogTrace("Importing message {0}", e.Consecutive);
          tMessage = JsonConvert.DeserializeObject<TransportMessage>(sMessage, jsonSettings);
        }

        if (tMessage == null) return;

        GateKeeperMethod(e.Consecutive);
        enteredGate = true;

        try
        {
          Logger.LogTrace("Processing message {0}", e.Consecutive);
          ITransportListener? listener;
          if (TransportListeners.TryGetValue(tMessage.MessageType, out listener))
          {
            listener.MessageReceived(tMessage, sMessage);
          }
          else
          {
            Logger.LogWarning("No listener registered for message type {0}", tMessage.MessageType);
          }
        }
        catch (Exception ex)
        {
          Logger.LogError("Error Processing Received Message: {0}", ex.Message);
        }

      }
      catch (Exception ex)
      {
        Logger.LogError("Error Processing Received Message: {0}", ex.Message);
      }

      //It's only called if the thread actually entered the gate. If it didn't, it would wake up another thread that should
      // still be waiting
      if (enteredGate)
        NudgeGate();
    }

    //This method is being executed from a threadpool thread
    private static object gate = new object();
    private static EventWaitHandle handle = new EventWaitHandle(true, EventResetMode.ManualReset);
    private void GateKeeperMethod(int consecutive)
    {
      while (true)
      {
        bool isTurn = false;
        lock (gate)
        {
          if (currentMessageCons == consecutive)
            isTurn = true;
        }

        //goes out of this method and proceeds to process the received message
        if (isTurn)
          break;

        handle.WaitOne();
      }
    }

    private void NudgeGate()
    {
      lock (gate)
      {
        currentMessageCons++;
        handle.Set();
      }
    }

    #region ITransportListener Members

    public void MessageReceived(TransportMessage message, string rawMessage)
    {
      Logger.LogInformation("Received Message for Transport Component - Not Implemented Feature.");
    }

    #endregion

    private static string GetMessageAsString(byte[] receivedMsgB)
    {
      int length = Array.IndexOf<byte>(receivedMsgB, (byte)'\0');
      if (length == -1)
        length = receivedMsgB.Length;
      System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
      return enc.GetString(receivedMsgB, 0, length);
    }

  }

}
