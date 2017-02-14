using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using mNetwork.TCP;

namespace ExampleServer
{
    class Program
    {
        static void Main(string[] args)
        {
            mTcpServer server = new mTcpServer(8303);
            server.Start();
            server.OnAcceptClient += client =>
            {
                Console.WriteLine("[client] Accepted{0}", client.LocalEndPoint());
            };

            while (true)
            {
                Thread.Sleep(10);
            }
        }
    }
}
