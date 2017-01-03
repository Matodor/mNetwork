using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mNetwork
{
    [AttributeUsage(AttributeTargets.Class)]
    public class mNetworkPacketAttribute : Attribute
    {
        public int PacketID { get; }

        public mNetworkPacketAttribute(int packetId)
        {
            PacketID = packetId;
        }
    }
}
