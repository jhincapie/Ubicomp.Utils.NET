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

    private static Object importLock = new Object();
    private static Object exportLock = new Object();

    private MulticastSocket socket;
    private IPAddress address;
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

      socket = new MulticastSocket(address.ToString(), port, udpTTL);
      socket.OnNotifyMulticastSocketListener += new NotifyMulticastSocketListener(socket_OnNotifyMulticastSocketListener);

      currentMessageCons = 1;
      socket.StartReceiving();

      Logger.LogInformation("Multicast Sockect Started to Listen for Traffic.");
      Logger.LogInformation("TransportComponent Initialized.");
    }

    public String Send(TransportMessage message)
    {
      String json;
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
        String sMessage = GetMessageAsString((byte[])e.NewObject);

        TransportMessage tMessage = null;
        lock (importLock)
        {
          // Using Logger instead of Console.WriteLine where appropriate, 
          // or keeping Console.WriteLine for debug if not converted to Debug/Trace logs.
          // For now, I'll convert these debug prints to Trace logs.
          Logger.LogTrace("Importing message {0}", e.Consecutive);
          tMessage = JsonConvert.DeserializeObject<TransportMessage>(sMessage, jsonSettings);
        }

        GateKeeperMethod(e.Consecutive);
        enteredGate = true;

        try
        {
          Logger.LogTrace("Processing message {0}", e.Consecutive);
          ITransportListener listener = TransportListeners[tMessage.MessageType];
          listener.MessageReceived(tMessage, sMessage);
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
    private static Object gate = new Object();
    private void GateKeeperMethod(int consecutive)
    {
      lock (gate)
      {
        while (currentMessageCons != consecutive)
        {
          Monitor.Wait(gate);
        }
      }
    }

    private void NudgeGate()
    {
      lock (gate)
      {
        currentMessageCons++;
        Monitor.PulseAll(gate);
      }
    }

    #region ITransportListener Members

    public void MessageReceived(TransportMessage message, String rawMessage)
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