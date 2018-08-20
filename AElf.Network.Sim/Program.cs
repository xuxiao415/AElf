using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using AElf.Network.Config;
using AElf.Network.Connection;
using AElf.Network.Data;
using AElf.Network.Peers;
using AElf.Network.Sim.Core;
using AElf.Network.Sim.PeerManagement;
using Google.Protobuf;
using NetMQ;
using NetMQ.Sockets;
using NLog;
using NLog.Config;
using NLog.Targets;
using Org.BouncyCastle.Crypto.Tls;

namespace AElf.Network.Sim
{
    public class Simulation
    {
        private readonly ILogger _logger;
        
        private readonly IPeerEventProvider _peerEventProvider;
        private readonly Dictionary<NodeData, List<NodeData>> _connectionGraph;
        
        public Simulation(IPeerEventProvider peerEventProvider, ILogger logger)
        {
            _connectionGraph = new Dictionary<NodeData, List<NodeData>>();
            _peerEventProvider = peerEventProvider;
            _logger = logger;
            
            peerEventProvider.PeerEventReceived += PeerEventProviderOnPeerEventReceived;
        }

        private void PeerEventProviderOnPeerEventReceived(object sender, EventArgs e)
        {
            if (e is PeerEventArgs peerEventArgs)
            {
                if (peerEventArgs.Type == EventType.Added)
                {
                    OnNodeAddedPeer(peerEventArgs.Source, peerEventArgs.Other);
                }
                else
                {
                    OnNodeRemovedPeer(peerEventArgs.Source, peerEventArgs.Other);
                }
            }
        }

        /// <summary>
        /// Adds a completly new node, used at the original setup time.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public bool AddNode(NodeData node)
        {
            if (node == null)
            {
                _logger?.Trace($"Null argument, connot add.");

                return false;
            }
            
            if (_connectionGraph.ContainsKey(node))
            {
                _logger?.Trace($"Event node already contains {node}.");
                return false;
            }
            
            _connectionGraph.Add(node, new List<NodeData>());

            return true;
        }

        public bool RemoveNode(NodeData node)
        {
            return _connectionGraph.Remove(node);
        }

        public bool OnNodeAddedPeer(NodeData source, NodeData addedPeer)
        {
            if (source == null || addedPeer == null)
            {
                _logger?.Trace("Null arguement.");
                return false;
            }
            
            if (!_connectionGraph.ContainsKey(source))
            {
                _logger?.Trace($"Source is not part of the current node list {source}.");
                return false;
            }

            var sourcePair = _connectionGraph[source];

            if (sourcePair.Contains(addedPeer))
            {
                _logger?.Trace($"Node to add already in collection");
                return false;
            }

            sourcePair.Add(addedPeer);

            return true;
        }
        
        public bool OnNodeRemovedPeer(NodeData source, NodeData removedPeer)
        {
            if (source == null || removedPeer == null)
            {
                _logger?.Trace("Null arguement.");
                return false;
            }
            
            if (!_connectionGraph.ContainsKey(source))
            {
                _logger?.Trace($"Source is not part of the current node list {source}.");
                return false;
            }

            var sourcePair = _connectionGraph[source];

            if (!sourcePair.Contains(removedPeer))
            {
                _logger?.Trace($"Node to add not in collection");
                return false;
            }

            sourcePair.Remove(removedPeer);

            return true;
        }
    }

    public interface IPeerEventProvider
    {
        event EventHandler PeerEventReceived;
    }

    public enum EventType
    {
        Added,
        Removed
    };

    public class PeerEventArgs : EventArgs
    {
        public EventType Type { get; set; }
        public NodeData Source { get; set; }
        public NodeData Other { get; set; }
    }

    public class ProtobufPeerEventProvider : IPeerEventProvider
    {
        public event EventHandler PeerEventReceived;
        
        private TcpClient client;

        public ProtobufPeerEventProvider()
        {
            
        }

        public void ReadProtobufByte(byte[] bytes)
        {
            NetPeerEventData args = NetPeerEventData.Parser.ParseFrom(bytes);
            PeerEventReceived?.Invoke(this, new PeerEventArgs
            {
                Source = args.Source,
                Other = args.Node,
                Type = (EventType)args.EventType
            });
        }
    }

    class Program
    {
        public static List<TestNode> nodes = new List<TestNode>();
        public static SubscriberSocket _socket;
        private static bool _hasStopped;

        static void Main(string[] args)
        {
            SetupLogs("12345");

            var proto = new ProtobufPeerEventProvider();
            proto.PeerEventReceived += ProtoOnPeerEventReceived;
            
            NetworkSubscriber subscriberSocket = new NetworkSubscriber(proto, LogManager.GetLogger(nameof(NetworkSubscriber)));
            subscriberSocket.Start();
            
//            TestNode tn = new TestNode();
//            tn.Start("1234 12345", "12345");
//            
//            TestNode tn2 = new TestNode();
//            tn2.Start("1235 12345", "12345");

            for (int i = 0; i < 5; i++)
            {
                TestNode tn = new TestNode();
                tn.Start("1234 12345", "12345");
                nodes.Add(tn);
            }
            
            Console.ReadKey();

            foreach (var node in nodes)
            {
                node.Stop();
            }
            
            _hasStopped = true;
        }

        private static void ProtoOnPeerEventReceived(object sender, EventArgs e)
        {
            var l = LogManager.GetLogger("Logger");
            l.Trace("Hello");
            
        }


//            var config = new LoggingConfiguration();
//            var consoleTarget = new ConsoleTarget("target1")
//            {
//                Layout = @"${date:format=HH\:mm\:ss} ${level} ${message} ${exception}"
//            };
//            
//            config.AddTarget(consoleTarget);
//            config.AddRuleForAllLevels(consoleTarget);
//            LogManager.Configuration = config;
//            
//            int port = int.Parse(args[0]);
//            
//            AElfNetworkConfig netConfig = new AElfNetworkConfig();
//            netConfig.ListeningPort = port;
//            
//            if(args.Length > 1)
//                netConfig.Bootnodes = new List<NodeData> { new NodeData { IpAddress = "127.0.0.1", Port = int.Parse(args[1])}};
//            
//            ConnectionListener listener = new ConnectionListener(LogManager.GetLogger(nameof(ConnectionListener)));
//            PeerManager pm = new PeerManager(netConfig, listener, LogManager.GetLogger(nameof(PeerManager)));
//            
//            NetworkManager manager = new NetworkManager(netConfig, pm, LogManager.GetLogger(nameof(NetworkManager)));
//
//            Task.Run(() => manager.Start());
//            
//            if (netConfig.ListeningPort == 1234)
//            {
//                EventWatcher evtW = new EventWatcher(pm);
//                evtW.StartWatching();
//                
//                Task.Run(() =>
//                {
//                    using (var server = new ResponseSocket("@tcp://localhost:5556")) // connect
//                    {
//                        while (true)
//                        {
//                            // Receive the message from the server socket
//                            Console.WriteLine("Awaiting request...");
//                            string m1 = server.ReceiveFrameString();
//
//                            if (m1 == "exit")
//                                break;
//
//                            Console.WriteLine("From Client: {0}", m1);
//
//                            if (m1 == "get peers")
//                            {
//                                var list = pm.GetPeers();
//                                var peerStr = list == null || !list.Any()
//                                    ? "{ empty }"
//                                    : list.Select(p => p.ToString()).Aggregate((a, b) => a.ToString() + ", " + b);
//                                server.SendFrame(peerStr);
//                            }
//                        }
//                    }
//                });
//            }
            
//            NetworkSubscriber subscriberSocket = new NetworkSubscriber();
//            
//            TestNode tn = new TestNode();
//            tn.Start("1234 12345", "12345");

//            Task.Delay(2000);
//            
//            TestNode tn2 = new TestNode();
//            tn2.Start("1235 12345 127.0.0.1:1234", "12346");
            
//            using (_socket = new SubscriberSocket())
//            {
//                //_socket.Options.ReceiveHighWatermark = 1000;
//                _socket.Connect($"tcp://localhost:12345");
//                _socket.Subscribe("Peer");
//                    
//                Console.WriteLine("Subscriber socket connecting...");
//
//                Task.Run(() =>
//                {
//                    while (!_hasStopped)
//                    {
//                        string messageTopicReceived = _socket.ReceiveFrameString();
//                        byte[] messageReceived = _socket.ReceiveFrameBytes();
//                            
//                        Console.WriteLine("Received " + JsonFormatter.Default.Format(NetPeerEventData.Parser.ParseFrom(messageReceived)));
//                    }
//                });
//            }

//            Console.ReadKey();
//            tn.Stop();
//            _hasStopped = true;
            //tn2.Stop();
        
        
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
    

        public class EventWatcher
        {
            private IPeerManager _peerManager;
            private EventPublisher _evtPublisher;

            public EventWatcher(IPeerManager peerManager)
            {
                _peerManager = peerManager;
                _evtPublisher = new EventPublisher();
            }

            public void StartWatching()
            {
                
                _peerManager.PeerAdded += PeerManagerOnPeerAdded;
                    
            }

            private void PeerManagerOnPeerAdded(object sender, EventArgs eventArgs)
            {
                if (eventArgs is PeerAddedEventArgs p)
                {
                    _evtPublisher.AddString(p.Peer.ToString());
                }
            }
        }

        public class EventPublisher
        {
            private PublisherSocket pubSocket;
            private BlockingCollection<string> _eventArgs = new BlockingCollection<string>();     
               
            public EventPublisher()
            {
                pubSocket = new PublisherSocket();
                pubSocket.Bind("tcp://localhost:12345");
                
                Task.Run(() => PublishLoop());
            }

            private void PublishLoop()
            {
                while (true)
                {
                    string msg = _eventArgs.Take();
                    pubSocket.SendMoreFrame("peer.added").SendFrame(msg);
                }
            }

            public void AddString(string str)
            {
                _eventArgs.Add(str);
            }
        }
    }
}