using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Linq;

namespace Ubicomp.Utils.NET.Sockets
{

  /// <summary>
  /// Taken from http://www.osix.net/modules/article/?id=409
  /// </summary>
  public class MulticastSocket
  {

    public event NotifyMulticastSocketListener? OnNotifyMulticastSocketListener;

    //Socket creation, regular UDP socket 
    private Socket udpSocket;
    private Int32 mConsecutive;

    private EndPoint localEndPoint = null!;
    private IPEndPoint localIPEndPoint = null!;

    private string targetIP;
    private int targetPort;
    private int udpTTL;
    private string? localIP;
    private List<IPAddress> joinedAddresses = new List<IPAddress>();

    public IEnumerable<IPAddress> JoinedAddresses => joinedAddresses;

    //socket initialization 
    public MulticastSocket(string tIP, int tPort, int TTL, string? lIP = null)
    {
      udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
      mConsecutive = 0;

      targetIP = tIP;
      targetPort = tPort;
      udpTTL = TTL;
      localIP = lIP;

      SetupSocket();
    }

    private void SetupSocket()
    {
      if (udpSocket.IsBound)
        throw new ApplicationException("The socket is already bound and receving.");

      // Always bind to Any for receiving multicast packets on Linux/Unix
      localIPEndPoint = new IPEndPoint(IPAddress.Any, targetPort);
      localEndPoint = (EndPoint)localIPEndPoint;

      //init Socket properties:
      try
      {
        udpSocket.SetSocketOption(SocketOptionLevel.Udp, SocketOptionName.NoDelay, 1);
      }
      catch (SocketException)
      {
        // NoDelay is not supported for UDP on some platforms (e.g. Linux)
      }

      //allow for loopback testing 
      udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
      udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, 1);

      //extremly important to bind the Socket before joining multicast groups 
      udpSocket.Bind(localIPEndPoint);

      //set multicast flags, sending flags - TimeToLive (TTL) 
      // 0 - LAN 
      // 1 - Single Router Hop 
      // 2 - Two Router Hops... 
      udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, udpTTL);

      //join multicast group 
      IPAddress mcastAddr = IPAddress.Parse(targetIP);
      IPAddress? localAddr = localIP != null ? IPAddress.Parse(localIP) : null;
      
      joinedAddresses.Clear();
      if (localAddr != null)
      {
        // If a specific local IP is provided, join only on that interface
        udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(mcastAddr, localAddr));
        joinedAddresses.Add(localAddr);
        
        // Also set the multicast interface for sending
        try
        {
          udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, localAddr.GetAddressBytes());
        }
        catch (SocketException) { }
      }
      else
      {
        // Try joining on all interfaces that are up and support multicast
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
          if (ni.OperationalStatus == OperationalStatus.Up && ni.SupportsMulticast)
          {
            var ipProps = ni.GetIPProperties();
            foreach (var addr in ipProps.UnicastAddresses)
            {
              if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
              {
                try
                {
                  udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(mcastAddr, addr.Address));
                  joinedAddresses.Add(addr.Address);
                }
                catch (SocketException)
                {
                  // Some interfaces might fail to join, ignore them
                }
              }
            }
          }
        }

        // Also try the default join if no interfaces were found or joined
        try
        {
          udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(mcastAddr, IPAddress.Any));
          if (!joinedAddresses.Any(a => a.Equals(IPAddress.Any)))
            joinedAddresses.Add(IPAddress.Any);
        }
        catch (SocketException) { /* Already joined or not supported */ }

        // Set the multicast interface for sending to Any (0.0.0.0)
        try
        {
          udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, IPAddress.Any.GetAddressBytes());
        }
        catch (SocketException) { }
      }

      NotifyMulticastSocketListener(MulticastSocketMessageType.SocketStarted, null);
    }

    public void StartReceiving()
    {
      if (OnNotifyMulticastSocketListener == null)
        throw new ApplicationException("No socket listener has been specified at OnNotifyMulticastSocketListener.");

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
      // Retrieve the state object and the client socket from the async state object. 
      StateObject? state = null;
      try
      {
        state = (StateObject?)ar.AsyncState;
        if (state == null) return;
        
        Socket client = state.WorkSocket;

        // Read data from the remote device. 
        int bytesRead = client.EndReceiveFrom(ar, ref localEndPoint);

        // Makes a copy of the buffer so it can be cleant up and reused while the listeners are notified in parallel threads.
        byte[] bufferCopy = new byte[bytesRead];
        Array.Copy(state.Buffer, 0, bufferCopy, 0, bytesRead);

        // Listeners are notified in a different thread
        NotifyMulticastSocketListener(MulticastSocketMessageType.MessageReceived, bufferCopy, ++mConsecutive);

        //keep listening 
        Recieve(state);
      }
      catch (Exception e)
      {
        NotifyMulticastSocketListener(MulticastSocketMessageType.ReceiveException, e);
        if (state != null)
          Recieve(state);
        else
          StartReceiving();
      }
    }

    //client send function 
    public void Send(string sendData)
    {
      byte[] bytesToSend = Encoding.UTF8.GetBytes(sendData);

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
        Socket? client = (Socket?)ar.AsyncState;
        if (client == null) return;

        // Complete sending the data to the remote device. 
        int bytesSent = client.EndSendTo(ar);

        // Notifies sending completed
        NotifyMulticastSocketListener(MulticastSocketMessageType.MessageSent, bytesSent);
      }
      catch (Exception e)
      {
        NotifyMulticastSocketListener(MulticastSocketMessageType.SendException, e);
      }
    }

    private void NotifyMulticastSocketListener(MulticastSocketMessageType messageType, object? obj)
    {
      ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadedNotifyMulticastSocketListener), new NotifyMulticastSocketListenerEventArgs(messageType, obj));
    }

    private void NotifyMulticastSocketListener(MulticastSocketMessageType messageType, object? obj, int consecutive)
    {
      ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadedNotifyMulticastSocketListener), new NotifyMulticastSocketListenerEventArgs(messageType, obj, consecutive));
    }

    private void ThreadedNotifyMulticastSocketListener(object? argsObj)
    {
      try
      {
        if (OnNotifyMulticastSocketListener != null && argsObj is NotifyMulticastSocketListenerEventArgs args)
          OnNotifyMulticastSocketListener(this, args);
      }
      catch (Exception e)
      {
        Console.Error.WriteLine(e);
      }
    }

    internal class StateObject
    {
      public const int BufferSize = 1024;

      private byte[] sBuffer;
      private Socket workSocket = null!;

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
      }

      internal StateObject(int size, Socket sock)
      {
        sBuffer = new byte[size];
        workSocket = sock;
      }
    }

  }
}