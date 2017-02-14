using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using mNetwork.TCP;

namespace ExampleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            mTcpClient client = mTcpClient.Connect("127.0.0.1", 8303);
            
            client.OnConnect += c =>
            {
                Console.WriteLine("[client] Connected");
            };

            while (true)
            {
                client.Tick();
                Thread.Sleep(10);
            }
        }
    }
}
