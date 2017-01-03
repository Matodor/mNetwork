using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace mNetwork.TCP
{
    public class mTcpServer
    {
        public event Action<mTcpClient> OnAcceptClient;
        public event Action<mTcpClient> OnClientConnected;

        private Dictionary<Guid, mTcpClient> _clients; 
        private readonly TcpListener _listener;
        private Thread _listenerThread;
        private bool _threadWork;

        public mTcpServer(IPAddress localaddr, int port)
        {
            _listener = new TcpListener(new IPEndPoint(localaddr, port));
            Init();
        }

        public mTcpServer(IPEndPoint endPoint)
        {
            _listener = new TcpListener(endPoint);
            Init();
        } 

        public mTcpServer(int port)
        {
            _listener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
            Init();
        }

        private void Init()
        {
            mNetworkHelper.Logger.WriteLine("Create TCP server");
            mNetworkHelper.Logger.WriteLine("Listen on: " + _listener.LocalEndpoint);

            _threadWork = true;
            _clients = new Dictionary<Guid, mTcpClient>();
            _listener.Start();
            _listenerThread = new Thread(Work);
            _listenerThread.Start();
        }

        private void Work()
        {
            while (_threadWork)
            {
                while (_listener.Pending())
                {
                    _listener.BeginAcceptTcpClient(AcceptTcpClientCallback, null);
                }

                foreach (var pair in _clients.ToArray())
                {
                    pair.Value.Tick();
                }
            }
        }

        private void AcceptTcpClientCallback(IAsyncResult ar)
        {
            var tcpClient = _listener.EndAcceptTcpClient(ar);
            mNetworkHelper.Logger.WriteLine("[clients] Tcpclient accepted: " + tcpClient.Client.RemoteEndPoint);

            var client = new mTcpClient(tcpClient, true);
            client.OnDisconnect += OnClientDisconnect;
            client.OnConnect += OnClientConnect;
            _clients.Add(client.Guid, client);

            OnAcceptClient?.Invoke(client);
        } 

        private void OnClientConnect(mTcpClient client)
        {
            OnClientConnected?.Invoke(client);
        } 

        private void OnClientDisconnect(mTcpClient client, string message)
        {
            _clients.Remove(client.Guid);
        } 
    }
}
