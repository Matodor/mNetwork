using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using mNetwork.Packets;

namespace mNetwork.TCP
{
    public class mTcpClient
    {
        public Guid Guid { get; }

        public event Action<object> OnReceive; 
        public event Action<mTcpClient, string> OnDisconnect; 
        public event Action<mTcpClient> OnConnect; 
        
        public const int ConnectTimeout = 5;
        
        public bool IsServerClient { get; }

        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly Dictionary<Guid, Action<object>> _onReceiveActions;

        private bool _acceptedByServer;
        private readonly DateTime _expireAcceptTime;

        public static mTcpClient Connect(string host, int port)
        { 
            var tcpClient = new TcpClient();
            mNetworkHelper.Logger.WriteLine($"Try connect to ({host}:{port})");

            var ar = tcpClient.BeginConnect(host, port, ConnectCallback, tcpClient);
            var succes = ar.AsyncWaitHandle.WaitOne(5000);

            if (succes)
            {
                if (tcpClient.Connected)
                {
                    mNetworkHelper.Logger.WriteLine($"Connected successful");
                    var client = new mTcpClient(tcpClient, false);
                    client.Send(new ConnectMsg());
                    return client;
                }
            }
            mNetworkHelper.Logger.WriteLine($"Connect failed");
            tcpClient.Close();
            return null;
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                var client = (TcpClient) ar.AsyncState;
                client.EndConnect(ar);
            }
            catch (SocketException e)
            {
                mNetworkHelper.Logger.WriteLine($"[ConnectCallback] {e.Message}");
            }
            catch
            {
                //
            }
        }

        public mTcpClient(TcpClient client, bool isServerClient)
        {
            IsServerClient = isServerClient;

            _acceptedByServer = false;
            _expireAcceptTime = DateTime.UtcNow + TimeSpan.FromSeconds(ConnectTimeout);
            _client = client;
            _stream = _client.GetStream();
            _onReceiveActions = new Dictionary<Guid, Action<object>>();

            if (isServerClient)
                Subscribe<ConnectMsg>(ConnectMessage);
            else
                Subscribe<ConnectionReadyMsg>(ConnectionReadyMessage);

            Subscribe<DisconnectMsg>(DisconnectMessage);
            Guid = Guid.NewGuid();
        }

        private void ConnectionReadyMessage(ConnectionReadyMsg msg)
        {
            if (_acceptedByServer)
                return;

            mNetworkHelper.Logger.WriteLine("[client] Client ready");
            _acceptedByServer = true;
            OnConnect?.Invoke(this);
        }

        private void DisconnectMessage(DisconnectMsg disconnectMsg)
        {
            Disconnect(disconnectMsg.Reason);
        }

        private void ConnectMessage(ConnectMsg msg)
        {
            if (_acceptedByServer)
                return;

            Send(new ConnectionReadyMsg());
            mNetworkHelper.Logger.WriteLine("[client] Client accepted");
            _acceptedByServer = true;
            OnConnect?.Invoke(this);
        }

        public void Disconnect(string message)
        {
            mNetworkHelper.Logger.WriteLine($"[client] Client disconnect ({message})");
            OnDisconnect?.Invoke(this, message);
        }

        public void Tick()
        {
            if (_client.Connected)
                Read();

            if (IsServerClient)
            {
                if (_acceptedByServer)
                {
                    if (!_client.Connected)
                        Disconnect("Timeout");
                }
                else
                {
                    if (DateTime.UtcNow > _expireAcceptTime)
                        Disconnect("Connect timeout");
                }
            }
        }

        public mTcpClient Subscribe<T>(Action<T> action)
        {
            var type = typeof (T);
            if (_onReceiveActions.ContainsKey(type.GUID))
            {
                _onReceiveActions[type.GUID] += o => { action((T) o); };
            }
            else
            {
                _onReceiveActions.Add(type.GUID, o => { action((T) o); });
            }
            return this;
        }

        public void Send(object obj)
        {
            if (obj == null)
                return;
            
            mNetworkHelper.Logger.WriteLine("[tcpClient] Send: " + obj);
            if (_stream.CanWrite)
            {
                var data = mNetworkHelper.Serialize(obj);
                if (data.Data != null && data.Data.Length > 0)
                {
                    var header = mNetworkHelper.CreateHeader(new mHeader
                    {
                        MessageLength = data.Data.Length,
                        MessageType = data.Type 
                    });
                    _stream.Write(header, 0, header.Length);
                    _stream.Write(data.Data, 0, data.Data.Length);
                }
            }
            else
            {
                mNetworkHelper.Logger.WriteLine("CANT WRITEEEEEEEEE");
            }
        }

        /*public void Send(object obj)
        {
            if (_stream.CanWrite)
            {
                var data = mNetworkHelper.Serialize(obj);
                var header = mNetworkHelper.CreateHeader(new mHeader
                {
                    MessageLength = data.Data.Length, MessageType = 0
                });
                _stream.BeginWrite(header, 0, header.Length, WriteHeader, data);
            }
        }

        private void WriteHeader(IAsyncResult ar)
        {
            var data = (mNetworkMessage) ar.AsyncState;
            _stream.EndWrite(ar);
            _stream.BeginWrite(data.Data, 0, data.Data.Length, WriteMessaage, data);
        }

        private void WriteMessaage(IAsyncResult ar)
        {
            _stream.EndWrite(ar);
        }*/

        private void Read()
        {
            while (_stream != null && _stream.CanRead && _stream.DataAvailable)
            {
                var headerBuffer = new byte[mNetworkHelper.HeaderSize];
                if (_stream.Read(headerBuffer, 0, headerBuffer.Length) == mNetworkHelper.HeaderSize)
                {
                    mHeader header;
                    if (mNetworkHelper.ParseHeader(headerBuffer, out header))
                    {
                        mNetworkHelper.Logger.WriteLine($"[client] ParseHeader: {header.MessageLength}:{header.MessageType}");

                        if (header.MessageLength > 1400)
                        {
                            _stream.Flush();
                            return;
                        }

                        var messageBuffer = new byte[header.MessageLength];
                        if (_stream.Read(messageBuffer, 0, messageBuffer.Length) == messageBuffer.Length)
                        {
                            object message = mNetworkHelper.Deserialize(new mNetworkMessage() { Type = header.MessageType, Data = messageBuffer });
                            mNetworkHelper.Logger.WriteLine($"[client] obj: {message.ToString()}");
                            OnReceive?.Invoke(message);
                            var key = message.GetType().GUID;
                            if (_onReceiveActions.ContainsKey(key))
                                _onReceiveActions[key](message);
                        }
                    }
                }
            }
        }

        /*private void ReadHeader(IAsyncResult ar)
        {
            var headerBuffer = (byte[])ar.AsyncState;
            if (_stream != null && _stream.CanRead && _stream.EndRead(ar) == headerBuffer.Length)
            {
                mHeader header;
                if (mNetworkHelper.ParseHeader(headerBuffer, out header))
                {
                    mNetworkHelper.Logger.WriteLine($"[client] ParseHeader: {header.MessageLength}");
                    var messageBuffer = new byte[header.MessageLength];
                    _stream.BeginRead(messageBuffer, 0, messageBuffer.Length, ReadMessage, messageBuffer);
                }
            }
        }*/

        /*private void ReadMessage(IAsyncResult ar)
        {
            var messageBuffer = (byte[])ar.AsyncState;
            int l = _stream.EndRead(ar);

            mNetworkHelper.Logger.WriteLine($"[client] ReadMessage: {l}:{messageBuffer.Length}");
            if (_stream != null && _stream.CanRead && l == messageBuffer.Length)
            {
                object message = mNetworkHelper.Deserialize(new mNetworkMessage() {Data = messageBuffer});
                mNetworkHelper.Logger.WriteLine($"[client] obj: {message.ToString()}");
                OnReceive?.Invoke(message);
                var key = message.GetType().GUID;
                if (_onReceiveActions.ContainsKey(key))
                    _onReceiveActions[key](message);
            }
        }*/
    }
}
