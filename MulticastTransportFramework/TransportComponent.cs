using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Jayrock.Json.Conversion;
using Jayrock.Json;
using System.IO;
using System.Threading;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{

  public class TransportComponent : ITransportListener
  {

    log4net.ILog logger = log4net.LogManager.GetLogger(typeof(TransportComponent));

    public const int TransportComponentID = 0;
    public static TransportComponent Instance = new TransportComponent();

    private static Object importLock = new Object();
    private static Object exportLock = new Object();

    private MulticastSocket socket;
    private IPAddress address;
    private int port;
    private int udpTTL;

    public Dictionary<Int32, ITransportListener> TransportListeners = new Dictionary<int, ITransportListener>();

    public ImportContext JsonImportContext;
    public ExportContext JsonExportContext;

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
      JsonExportContext = JsonConvert.CreateExportContext();
      JsonExportContext.Register(new TransportMessageExporter());

      JsonImportContext = JsonConvert.CreateImportContext();
      JsonImportContext.Register(new TransportMessageImporter());

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

      logger.Info("Multicast Sockect Started to Listen for Traffic.");
      logger.Info("TransportComponent Initialized.");
    }

    public String Send(TransportMessage message)
    {
      StringBuilder buffer = new StringBuilder();
      JsonWriter writer = new JsonTextWriter(new StringWriter(buffer));

      lock (exportLock)
        JsonExportContext.Export(message, writer);

      String json = buffer.ToString();

      socket.Send(json);
      return json;
    }

    void socket_OnNotifyMulticastSocketListener(object sender, NotifyMulticastSocketListenerEventArgs e)
    {
      if (e.Type != MulticastSocketMessageType.MessageReceived)
      {
        if (e.Type == MulticastSocketMessageType.SendException)
        {
          logger.Error(String.Format("Error Sending Message: {0}", e.NewObject));
        }
        else if (e.Type == MulticastSocketMessageType.ReceiveException)
        {
          logger.Error(String.Format("Error Receiving Message: {0}", e.NewObject));
        }
        return;
      }

      bool enteredGate = false;
      try
      {
        String sMessage = GetMessageAsString((byte[])e.NewObject);
        JsonReader reader = new JsonTextReader(new StringReader(sMessage));

        TransportMessage tMessage = null;
        lock (importLock)
        {
          Console.WriteLine("Importing message {0}", e.Consecutive);
          //This is a method called from different threads -- This way we make only one import at a time.
          tMessage = JsonImportContext.Import<TransportMessage>(reader);
        }

        GateKeeperMethod(e.Consecutive);
        enteredGate = true;

        try
        {
          Console.WriteLine("Processing message {0}", e.Consecutive);
          ITransportListener listener = TransportListeners[tMessage.MessageType];
          listener.MessageReceived(tMessage, sMessage);
        }
        catch (Exception ex)
        {
          logger.Error(String.Format("Error Processing Received Message: {0}", ex.Message));
        }

      }
      catch (Exception ex)
      {
        logger.Error(String.Format("Error Processing Received Message: {0}", ex.Message));
      }

      //It's only called if the thread actually entered the gate. If it didn't, it would wake up another thread that should
      // still be waiting
      if (enteredGate)
        NudgeGate();
    }

    //This method is being executed from a threadpool thread
    private static Object gate = new Object();
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

    public void MessageReceived(TransportMessage message, String rawMessage)
    {
      logger.Info("Received Message for Transport Component - Not Implemented Feature.");
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
