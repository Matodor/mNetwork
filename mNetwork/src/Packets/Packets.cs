using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mNetwork.Packets
{
    [mNetworkPacket(1)]
    public class ConnectionReadyMsg
    {
        public string Message = "";
    }

    [mNetworkPacket(2)]
    public class ConnectMsg
    {
        public string Message = "Connect to server";
    }

    [mNetworkPacket(3)]
    public class DisconnectMsg
    {
        public string Reason = "none";
    }
}
 