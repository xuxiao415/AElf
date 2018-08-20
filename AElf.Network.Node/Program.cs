using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using AElf.Network.Config;
using AElf.Network.Data;
using AElf.Network.Node.Core;
using AElf.Network.Peers;
using Google.Protobuf;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace AElf.Network.Node
{
    class Program
    {
        //private static PublisherSocket pubSocket;
        private static Timer t;

        private static NetworkPublisher _publisher;
        
        static void Main(string[] args)
        {
            Console.WriteLine(string.Join(",", args));
            
            // Configure NLog
            SetupLogs(args[0]);

            // Dial the simulation - the connection listener on the other side will 
            // have an event.
            TcpClient client = null;
            try
            {
                client = new TcpClient();
                client.Connect("localhost", int.Parse(args[1]));
            }
            catch (Exception e)
            {
                ;
            }
            
            // Create the publisher to the simulation
             _publisher = new NetworkPublisher(client);

            //AElfNetworkConfig conf = new AElfNetworkConfig();

            // If any bootnodes where given
//            if (args.Length == 3)
//                conf.Bootnodes = new List<NodeData> { NodeData.FromString(args[2]) };
//
//            conf.ListeningPort = int.Parse(args[0]);
            
            //PeerManager pm = new PeerManager(conf, new ConnectionListener(LogManager.GetLogger(nameof(ConnectionListener))), LogManager.GetLogger(nameof(PeerManager)));
            //pm.Start();
            //pm.NetPeerEvent += PmOnNetPeerEvent;

            // Setup a fake timer to send some data
            t = new Timer(e => Send(), null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5));
            
            // Block until user inputs
            Console.ReadKey();
            
            // Close the connection
            _publisher.Close();
        }

        private static void Send()
        {
            var data = new NetPeerEventData
            {
                EventType = NetPeerEventData.Types.EventType.Added,
                NodeData = new NodeData {IpAddress = "0.0.0.0", Port = 0}
            };
            
            
            _publisher.Publish(data.ToByteArray());
            //pubSocket.SendMoreFrame("Peer").SendFrame(data.ToByteArray());
        }

        private static void PmOnNetPeerEvent(object sender, EventArgs e)
        {
            if (e is NetPeerEventArgs args && args.Data != null)
            {
                //pubSocket.SendMoreFrame("Peer").SendFrame(args.Data.ToByteArray());
            }
        }

        private static void SetupLogs(string nodePort)
        {
            var config = new LoggingConfiguration();
            var consoleTarget = new ConsoleTarget("target1")
            {
                Layout = @"[${date:format=HH\:mm\:ss}] [" + nodePort + "] ${message} ${exception}"
            };
            
            config.AddTarget(consoleTarget);
            config.AddRuleForAllLevels(consoleTarget);
            LogManager.Configuration = config;
        }
    }

   
}