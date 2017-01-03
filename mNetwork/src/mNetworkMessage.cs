using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mNetwork
{
    public class mNetworkMessage
    {
        public int Type { get; set; }
        public byte[] Data { get; set; }
    }
}
