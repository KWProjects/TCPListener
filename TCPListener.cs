using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;

namespace LingoServer
{
    class TCPListener
    {
        struct Client
        {
            public int Id;
            public Socket Socket;
        }

        public int total = 0;

        IPEndPoint _localEndPoint;
        Socket _listener;
        Thread _listen;
        Dictionary<int, Client> _clients = new Dictionary<int, Client>();
        int _backlog;
        bool _running = false;
        private Object _clientsLock = new Object();

        public TCPListener(IPAddress ipAddress, int port, int backlog)
        {
            _localEndPoint = new IPEndPoint(ipAddress, port);
            _backlog = backlog;
        }

        public void Start()
        {
            if (Initialize())
            {
                _running = true;
                _listen = new Thread(new ThreadStart(Listen));
                _listen.Start();
            }
        }

        public bool IsRunning()
        {
            return _running;
        }
        
        public void Stop()
        {
            _running = false;
            CleanUp();
        }

        private bool Initialize()
        {
            try
            {
                _listener = new Socket(_localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _listener.Bind(_localEndPoint);
                _listener.Listen(_backlog);
                return true;
            }
            catch (SocketException e)
            {
                Console.WriteLine("Failed to initialize listener socket \n" + e.Message);
                return false;
            }
        }

        private void CleanUp()
        {
            _listener.Close();
            _listener.Dispose();
            lock (_clientsLock)
            {
                foreach (KeyValuePair<int, Client> client in _clients)
                {
                    client.Value.Socket.Close();
                    client.Value.Socket.Dispose();
                }

                _clients.Clear();
            }
        }

        private void Listen()
        {
            try
            {
                while (_running)
                {
                    Socket clientSocket = _listener.Accept();

                    if (clientSocket.Connected)
                    {
                        Client client;
                        client.Socket = clientSocket;
                        
                        lock (_clientsLock)
                        {
                            client.Id = !_clients.Any() ? 1 : _clients.Keys.Max() + 1;
                            _clients.Add(client.Id, client);
                        }

                        ClientConnectionEventArgs args = new ClientConnectionEventArgs();
                        args.ClientId = client.Id;
                        args.RemoteEndPoint = client.Socket.RemoteEndPoint;
                        OnClientConnected(args);

                        Thread receive = new Thread(() => Receive(client));
                        receive.Start();
                    }
                }
            }
            catch (SocketException e)
            {
                if(_running)
                {
                    Console.WriteLine("Error while listening, stopping server... \n" + e.Message);
                }
               
            }

            finally
            {
                CleanUp();
                _running = false;
            }
        }

        private void Receive(Client client)
        {
            try
            {
                while (client.Socket.Connected && _running)
                {
                    byte[] messageReceived = new byte[1024];
                    int data = client.Socket.Receive(messageReceived);
                    //if (data != 0)
                    //{
                        MessageReceivedEventArgs args = new MessageReceivedEventArgs();
                    //args.Message = Encoding.ASCII.GetString(messageReceived, 0, data);
                    args.Message = messageReceived;
                    args.ClientId = client.Id;
                    OnMessageReceived(args);
                    total++;
                    Thread.Sleep(100);
                    //}
                    //else
                    //{
                        //Disconnect(client.Id);
                    //}
                }
            }
            catch (SocketException e)
            {
                if (_running)
                {
                    Console.WriteLine(e.Message);
                }
            }

            finally
            {
                
                lock (_clientsLock)
                {
                    _clients.Remove(client.Id);
                    client.Socket.Close();
                    //client.Socket.Dispose();
                }

                if (_running)
                {
                    ClientConnectionEventArgs args = new ClientConnectionEventArgs();
                    args.ClientId = client.Id;
                    OnClientDisconnected(args);
                }
            }
        }
        //Send string
        public void Send(int clientId, string message)
        {
            Client client;
            
            lock (_clientsLock)
            {
                if (_clients.TryGetValue(clientId, out client))
                {
                    try
                    {
                        client.Socket.Send(Encoding.UTF8.GetBytes(message));
                    }
                    catch (SocketException e)
                    {

                    }
                    
                }
            }
        }

        //Send bytes
        public void Send(int clientId, Byte[] message)
        {
            Client client;

            lock (_clientsLock)
            {
                if (_clients.TryGetValue(clientId, out client))
                {
                    try
                    {
                        client.Socket.Send(message);
                    }
                    catch (SocketException e)
                    {

                    }
                }
            }
        }
        public void SendAll(Byte[] message)
        {
            lock (_clientsLock)
            {
                foreach (Client client in _clients.Values)
                {
                    try
                    {
                        client.Socket.Send(message);
                    }
                    catch(SocketException e)
                    {
                        
                    }
                }
            }
        }



        public void Disconnect(int clientId)
        {
            Client client;
            lock (_clientsLock)
            {
                if (_clients.TryGetValue(clientId, out client))
                {
                    client.Socket.Close();
                }
            }
        }

        public event EventHandler<ClientConnectionEventArgs> ClientConnected;
        public event EventHandler<ClientConnectionEventArgs> ClientDisconnected;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        protected virtual void OnClientConnected(ClientConnectionEventArgs e)
        {
            ClientConnected?.Invoke(this, e);
        }

        protected virtual void OnClientDisconnected(ClientConnectionEventArgs e)
        {
            ClientDisconnected?.Invoke(this, e);
        }
        protected virtual void OnMessageReceived(MessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }
    }

    public class ClientConnectionEventArgs : EventArgs
    {
        public int ClientId { get; set; }
        public EndPoint RemoteEndPoint { get; set; }
    }
    public class MessageReceivedEventArgs : EventArgs
    {
        public int ClientId { get; set; }
        public Byte[] Message { get; set; }
    }
}






