using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace CAF.Util.Net
{

  /// <summary>
  /// Taken from http://www.osix.net/modules/article/?id=409
  /// </summary>
  public class MulticastSocket
  {

    public event NotifyMulticastSocketListener OnNotifyMulticastSocketListener;

    //Socket creation, regular UDP socket 
    private Socket udpSocket;

    private EndPoint localEndPoint;
    private IPEndPoint localIPEndPoint;

    private string targetIP;
    private int targetPort;

    //socket initialization 
    public MulticastSocket(string tIP, int tPort)
    {
      udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

      targetIP = tIP;
      targetPort = tPort;
    }

    public void StartReceiving()
    {
      if (udpSocket.IsBound)
        throw new ApplicationException("The socket is already bound and receving.");
      if (OnNotifyMulticastSocketListener == null)
        throw new ApplicationException("No socket listener has been specified at OnNotifyMulticastSocketListener.");

      //recieve data from any source 
      localIPEndPoint = new IPEndPoint(IPAddress.Any, targetPort);
      localEndPoint = (EndPoint)localIPEndPoint;

      //init Socket properties:
      udpSocket.SetSocketOption(SocketOptionLevel.Udp, SocketOptionName.NoDelay, 1);

      //allow for loopback testing 
      udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);

      //extremly important to bind the Socket before joining multicast groups 
      udpSocket.Bind(localIPEndPoint);

      //set multicast flags, sending flags - TimeToLive (TTL) 
      // 0 - LAN 
      // 1 - Single Router Hop 
      // 2 - Two Router Hops... 
      udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 0);

      //join multicast group 
      udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(IPAddress.Parse(targetIP)));

      // Create the state object. 
      StateObject state = new StateObject();
      state.WorkSocket = udpSocket;

      //get in waiting mode for data - always (this doesn't halt code execution) 
      Recieve(state);
    }

    //initial receive function
    private void Recieve(StateObject state)
    {
      // Begin receiving the data from the remote device. 
      Socket client = state.WorkSocket;
      client.BeginReceiveFrom(state.Buffer, 0, StateObject.BufferSize, 0, ref localEndPoint, new AsyncCallback(ReceiveCallback), state);
    }

    //executes the asynchronous receive - executed everytime data is received on the port 
    private void ReceiveCallback(IAsyncResult ar)
    {
      try
      {
        // Retrieve the state object and the client socket from the async state object. 
        StateObject state = (StateObject)ar.AsyncState;
        Socket client = state.WorkSocket;

        // Read data from the remote device. 
        int bytesRead = client.EndReceiveFrom(ar, ref localEndPoint);

        try
        {
          if (OnNotifyMulticastSocketListener != null)
            OnNotifyMulticastSocketListener(this, new NotifyMulticastSocketListenerEventArgs(MulticastSocketMessageType.MessageReceived, state.Buffer));
        }
        catch (Exception e) { }

        //keep listening 
        for (int i = 0; i < bytesRead; i++)
          state.Buffer[i] = (byte)'\0';
        Recieve(state);
      }
      catch (Exception e)
      {
        if (OnNotifyMulticastSocketListener != null)
          OnNotifyMulticastSocketListener(this, new NotifyMulticastSocketListenerEventArgs(MulticastSocketMessageType.ReceiveException, e));
      }
    }

    //client send function 
    public void Send(string sendData)
    {
      byte[] bytesToSend = Encoding.ASCII.GetBytes(sendData);

      //set the target IP 
      IPEndPoint remoteIPEndPoint = new IPEndPoint(IPAddress.Parse(targetIP), targetPort);
      EndPoint remoteEndPoint = (EndPoint)remoteIPEndPoint;

      //do asynchronous send 
      udpSocket.BeginSendTo(bytesToSend, 0, bytesToSend.Length, SocketFlags.None, remoteEndPoint, new AsyncCallback(SendCallback), udpSocket);
    }

    //executes the asynchronous send 
    private void SendCallback(IAsyncResult ar)
    {
      try
      {
        // Retrieve the socket from the state object. 
        Socket client = (Socket)ar.AsyncState;

        // Complete sending the data to the remote device. 
        int bytesSent = client.EndSendTo(ar);
      }
      catch (Exception e)
      {
        Console.WriteLine(e.ToString());
      }
    }

    internal class StateObject
    {
      public const int BufferSize = 1024;

      private byte[] sBuffer;
      private Socket workSocket;

      internal byte[] Buffer
      {
        get { return sBuffer; }
        set { sBuffer = value; }
      }

      internal Socket WorkSocket
      {
        get { return workSocket; }
        set { workSocket = value; }
      }

      internal StateObject()
      {
        sBuffer = new byte[BufferSize];
        workSocket = null;
      }

      internal StateObject(int size, Socket sock)
      {
        sBuffer = new byte[size];
        workSocket = sock;
      }
    }

  }
}
