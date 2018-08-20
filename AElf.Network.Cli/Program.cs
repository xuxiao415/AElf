using System;
using NetMQ;
using NetMQ.Sockets;

namespace AElf.Network.Cli
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var subSocket = new SubscriberSocket())
            {
                subSocket.Connect("tcp://localhost:12345");
                subSocket.Subscribe("peer");
                
                while (true)
                {
                    string messageTopicReceived = subSocket.ReceiveFrameString();
                    string messageReceived = subSocket.ReceiveFrameString();
                    Console.WriteLine(messageReceived);
                }
            }
            
//            using (var client = new RequestSocket(">tcp://localhost:5556"))
//            {
//                while (true)
//                {
//                    Console.WriteLine("Readin :");
//                    string s = Console.ReadLine();
//                    client.SendFrame(s);
//
//                    if (s == "exit")
//                        break;
//                
//                    string m1 = client.ReceiveFrameString();
//                    Console.WriteLine("From Client: {0}", m1);
//                }
//
//                Console.WriteLine("Finished loop...");
//            }
            
        }
    }
}