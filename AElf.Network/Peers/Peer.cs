﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Timers;
using AElf.Common;
using AElf.Cryptography.ECDSA;
using AElf.Network.Connection;
using AElf.Network.Data;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using NLog;

namespace AElf.Network.Peers
{
    public class PeerDisconnectedArgs : EventArgs
    {
        public DisconnectReason Reason { get; set; }
        public Peer Peer { get; set; }
    }

    public class AuthFinishedArgs : EventArgs
    {
        public bool IsAuthentified { get; private set; }
        public RejectReason Reason { get; private set; }

        public AuthFinishedArgs(RejectReason reason)
        {
            IsAuthentified = false;
            Reason = reason;
        }
        
        public AuthFinishedArgs()
        {
            IsAuthentified = true;
        }
    }

    public enum RejectReason
    {
        Auth_Timeout,
        Auth_Invalid_handshake_msg,
        Auth_Invalid_Key,
        Auth_Invalid_Sig
    }

    public enum DisconnectReason
    {
        StreamClosed
    }
    
    public class PeerMessageReceivedArgs : EventArgs
    {
        public Peer Peer { get; set; }
        public Message Message { get; set; }
    }
    
    /// <summary>
    /// This class is essentially a wrapper around the connections underlying stream. Its the entry
    /// point for incoming messages and is also used for sending messages to the peer it represents.
    /// This class handles a basic form of authentification as well as ping messages.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Peer : IPeer
    {
        private const string LoggerName = "Peer";
        
        private const double DefaultPingInterval = 1000;
        private const double DefaultAuthTimeout = 2000;
        
        private readonly ILogger _logger;
        private readonly IMessageReader _messageReader;
        private readonly IMessageWriter _messageWriter;
        
        private readonly Timer _pingPongTimer;
        private readonly Timer _authTimer;
        
        /// <summary>
        /// The event that's raised when a message is received from the peer.
        /// </summary>
        public event EventHandler MessageReceived;
        
        /// <summary>
        /// The event that's raised when a peers stream has ended.
        /// </summary>
        public event EventHandler PeerDisconnected;

        /// <summary>
        /// The event that's raised when the authentification phase has finished.
        /// </summary>
        public event EventHandler AuthFinished;

        /// <summary>
        /// Indicates if Dispose has been called (once false, never changes back to true).
        /// </summary>
        public bool IsDisposed { get; private set; }
        
        /// <summary>
        /// Indicates if correct authentification information has been received.
        /// </summary>
        public bool IsAuthentified { get; private set; }

        /// <summary>
        /// This nodes listening port.
        /// </summary>
        private readonly int _port;
        
        /// <summary>
        /// This nodes public key.
        /// </summary>
        private ECKeyPair _nodeKey;
        
        /// <summary>
        /// The underlying network client.
        /// </summary>
        private readonly TcpClient _client;
        
        public int PacketsReceivedCount { get; private set; }
        
        /// <summary>
        /// List of currently pending ping requests.
        /// </summary>
        private readonly List<Ping> _pings = new List<Ping>();
        private object pingLock = new Object();
        
        private TimeSpan pingWaitTime = TimeSpan.FromSeconds(2);

        private int _droppedPings = 0;

        public double AuthTimeout { get; set; } = DefaultAuthTimeout;

        /// <summary>
        /// The data received in the handshake message.
        /// </summary>
        [JsonProperty(PropertyName = "address")]
        public NodeData DistantNodeData
        {
            get { return _lastReceivedHandshake?.NodeInfo; }
        }

        private Handshake _lastReceivedHandshake;
        
        public ECKeyPair DistantNodeKeyPair { get; private set; }
        
        public byte[] DistantNodeAddress
        {
            get
            {
                return DistantNodeKeyPair?.GetAddress().DumpByteArray();
            }
        }

        public byte[] DistantPublicKey
        {
            get { return _lastReceivedHandshake?.PublicKey.ToByteArray(); }
        }
        
        [JsonProperty(PropertyName = "isBp")]
        public bool IsBp { get; internal set; } 

        public string IpAddress
        {
            get
            {
                return DistantNodeData?.IpAddress;
            }
        }

        public ushort Port
        {
            get
            {
                return DistantNodeData?.Port != null ? (ushort) DistantNodeData?.Port : (ushort)0;
            }
        }
        
        public Peer(TcpClient client, IMessageReader reader, IMessageWriter writer, int port, ECKeyPair nodeKey)
        {
            _pingPongTimer = new Timer();
            _authTimer = new Timer();
            
            SetupHeartbeat();
            
            _port = port;
            _nodeKey = nodeKey;
            _logger = LogManager.GetLogger(LoggerName);
            
            _client = client;
            
            _messageReader = reader;
            _messageWriter = writer;
        }

        private void SetupHeartbeat()
        {
            _pingPongTimer.Interval = DefaultPingInterval;
            _pingPongTimer.Elapsed += TimerTimeoutElapsed;
            _pingPongTimer.AutoReset = true;
        }
        
        public bool Start()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(Peer), "This peer as already been disposed.");
            
            if (IsAuthentified)
                throw new InvalidOperationException("Cannot start an already authentified peer.");
            
            if (_messageReader == null || _messageWriter == null || _client == null)
                throw new InvalidOperationException("Could not initialize, null components.");
            
            try
            {
                _messageReader.PacketReceived += ClientOnPacketReceived;
                _messageReader.StreamClosed += MessageReaderOnStreamClosed;
            
                _messageReader.Start();
                _messageWriter.Start();

                StartAuthentification();
            }
            catch (Exception e)
            {
                _logger?.Trace(e, "Error while initializing the connection");
                Dispose();
                return false;
            }

            return true;
        }

        private void MessageReaderOnStreamClosed(object sender, EventArgs eventArgs)
        {
            Dispose();
            
            _logger?.Trace($"Peer connection has been terminated : {DistantNodeData}");
            
            PeerDisconnected?.Invoke(this, new PeerDisconnectedArgs { Peer = this, Reason = DisconnectReason.StreamClosed } );
        }

        private void ClientOnPacketReceived(object sender, EventArgs eventArgs)
        {
            if (IsDisposed)
                return;
            
            try
            {
                if (!(eventArgs is PacketReceivedEventArgs a) || a.Message == null)
                    return;

                if (a.Message.Type == (int) MessageType.Auth)
                {
                    HandleAuthResponse(a.Message);
                    return;
                }
                
                if (!IsAuthentified)
                {
                    _logger?.Trace("Received message while not authentified : " + a.Message);
                    return;
                }

                if (a.Message.Type == (int) MessageType.Ping)
                {
                    HandlePingMessage(a.Message);
                    return;
                }

                if (a.Message.Type == (int) MessageType.Pong)
                {
                    HandlePongMessage(a.Message);
                    return;
                }
                
                PacketsReceivedCount++;
            
                FireMessageReceived(a.Message);
            }
            catch (Exception e)
            {
                _logger?.Trace(e);
            }
        }

        #region Heartbeat
        
        private void TimerTimeoutElapsed(object sender, ElapsedEventArgs e)
        {
            if (IsDisposed)
                return;

            lock (pingLock)
            {
                DateTime lowerThreshold = DateTime.Now - pingWaitTime;
                var pings = _pings.Where(p => p.Time.ToDateTime() < lowerThreshold).ToList();

                if (pings.Count > 0)
                {
                    _droppedPings += pings.Count;
                    _logger?.Trace($"{DistantNodeData} - Current failed count {_droppedPings}.");
                    
                    var peerStr = _pings.Select(c => c.Id).Aggregate((a, b) => a.ToString() + ", " + b);
                    
                    _logger?.Trace($"{DistantNodeData} - {pings.Count} pings where dropped [ {peerStr} ].");
                    
                    foreach (var p in pings)
                        _pings.Remove(p);
                }
            }
            
            // Create a new ping
            try
            {
                Guid id = Guid.NewGuid();
                Ping ping = new Ping { Id = id.ToString(), Time = Timestamp.FromDateTime(DateTime.UtcNow)};
            
                byte[] payload = ping.ToByteArray();
            
                var pingMsg = new Message { Type = (int)MessageType.Ping, Length = payload.Length, Payload = payload };

                lock (pingLock)
                {
                    _pings.Add(ping);
                }

                Task.Run(() => EnqueueOutgoing(pingMsg));
            }
            catch (Exception exception)
            {
                _logger?.Trace(exception, "Error while sending ping message.");
            }
        }

        private void HandlePingMessage(Message pingMsg)
        {
            try
            {
                Ping ping = Ping.Parser.ParseFrom(pingMsg.Payload);
                Pong pong = new Pong { Id = ping.Id, Time = Timestamp.FromDateTime(DateTime.UtcNow) };
                        
                byte[] payload = pong.ToByteArray();
            
                var pongMsg = new Message
                {
                    Type = (int)MessageType.Pong,
                    Length = payload.Length,
                    Payload = payload
                };

                EnqueueOutgoing(pongMsg);
            }
            catch (Exception e)
            {
                _logger?.Trace(e, $"Failed to process ping message from {DistantNodeData}.");
            }
        }

        private void HandlePongMessage(Message pongMsg)
        {
            try
            {
                Pong pong = Pong.Parser.ParseFrom(pongMsg.Payload);
                
                lock (pingLock)
                {
                    Ping ping = _pings.FirstOrDefault(p => p.Id == pong.Id);
                            
                    if (ping != null)
                        _pings.Remove(ping);
                    else
                        _logger?.Trace($"Could not match pong reply {pong.Id}.");
                }
            }
            catch (Exception e)
            {
                _logger?.Trace(e, $"Failed to handle pong message from {DistantNodeData}.");
            }
        }

        #endregion Heartbeat

        #region Authentification
        
        /// <summary>
        /// This method sends authentification information to the distant peer and
        /// start the authentification timer.
        /// </summary>
        /// <returns></returns>
        private void StartAuthentification()
        {

            var nodeInfo = new NodeData {Port = _port};
            
            ECSigner signer = new ECSigner();
            ECSignature sig = signer.Sign(_nodeKey, nodeInfo.ToByteArray());
                
            var nd = new Handshake
            {
                NodeInfo = nodeInfo,
                PublicKey = ByteString.CopyFrom(_nodeKey.GetEncodedPublicKey()),
                R = ByteString.CopyFrom(sig.R),
                S = ByteString.CopyFrom(sig.S),
            };
            
            byte[] packet = nd.ToByteArray();
            
            _logger?.Trace($"Sending authentification : {{ port: {nd.NodeInfo.Port}, addr: {nd.PublicKey.ToByteArray().ToHex()} }}");
            
            _messageWriter.EnqueueMessage(new Message { Type = (int)MessageType.Auth, HasId = false, Length = packet.Length, Payload = packet});
            
            StartAuthTimer();
        }
        
        private void StartAuthTimer()
        {
            _authTimer.Interval = AuthTimeout;
            _authTimer.Elapsed += AuthTimerElapsed;
            _authTimer.AutoReset = false;
            _authTimer.Start();
        }

        private void AuthTimerElapsed(object sender, ElapsedEventArgs e)
        {
            _authTimer.Stop(); // dispose

            if (IsAuthentified)
                return; 
            
            _logger?.Trace("Authentification timed out.");
            
            Dispose();
            
            AuthFinished?.Invoke(this, new AuthFinishedArgs(RejectReason.Auth_Timeout));
        }

        /// <summary>
        /// Handles authentification information.
        /// </summary>
        /// <param name="aMessage"></param>
        private void HandleAuthResponse(Message aMessage)
        {
            try
            {
                _authTimer.Stop();
                
                Handshake handshk = Handshake.Parser.ParseFrom(aMessage.Payload);
                
                AuthentifyWith(handshk);
                
                // Update with the real IP address
                IPEndPoint remoteEndPoint = (IPEndPoint)_client.Client.RemoteEndPoint;
                handshk.NodeInfo.IpAddress = remoteEndPoint.Address.ToString();
                
                _pingPongTimer.Start();
            }
            catch (Exception e)
            {
                _logger?.Trace(e, "Error processing authentification information.");
                Dispose();
            }
            
            AuthFinished?.Invoke(this, new AuthFinishedArgs());
        }

        /// <summary>
        /// Mainly for testing purposes, it's used for authentifying a node. Note that
        /// is doesn't launch the correponding event.
        /// </summary>
        /// <param name="handshakeMsg"></param>
        internal void AuthentifyWith(Handshake handshakeMsg)
        {
            if (handshakeMsg == null)
            {
                FireInvalidAuth(RejectReason.Auth_Invalid_handshake_msg);
                return;
            }
            
            _lastReceivedHandshake = handshakeMsg;
            
            try
            {
                DistantNodeKeyPair = ECKeyPair.FromPublicKey(handshakeMsg.PublicKey.ToByteArray());
                
                if (DistantNodeKeyPair == null)
                {
                    FireInvalidAuth(RejectReason.Auth_Invalid_Key);
                    return;
                }
                
                // verify sig
                ECVerifier verifier = new ECVerifier(DistantNodeKeyPair);
                bool sigValid = verifier.Verify(
                    new ECSignature(handshakeMsg.R.ToByteArray(), handshakeMsg.S.ToByteArray()),
                    handshakeMsg.NodeInfo.ToByteArray());

                if (!sigValid)
                {
                    FireInvalidAuth(RejectReason.Auth_Invalid_Sig);
                    return;
                }

            }
            catch (Exception)
            {
                FireInvalidAuth(RejectReason.Auth_Invalid_Key);
                return;
            }
            
            IsAuthentified = true;
        }

        private void FireInvalidAuth(RejectReason reason)
        {
            AuthFinished?.Invoke(this, new AuthFinishedArgs(reason));
        }

        #endregion Authentification

        private void FireMessageReceived(Message p)
        {
            MessageReceived?.Invoke(this, new PeerMessageReceivedArgs { Peer = this, Message = p });
        }

        /// <summary>
        /// Sends the provided message to the peer.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public void EnqueueOutgoing(Message msg)
        {
            try
            {
                if (!IsAuthentified)
                {
                    _logger?.Trace($"Can't write : not identified {DistantNodeData}.");
                }
                
                if (_messageWriter == null)
                {
                    _logger?.Trace($"Peer {DistantNodeData?.IpAddress} : {DistantNodeData?.Port} - Null stream while sending");
                    return;
                }
                
                _messageWriter.EnqueueMessage(msg);
            }
            catch (Exception e)
            {
                _logger?.Trace(e, $"Exception while sending data.");
            }
        }
        
        public override string ToString()
        {
            return DistantNodeData?.IpAddress + ":" + DistantNodeData?.Port;
        }

        /// <summary>
        /// Equality of two peers is based on the equality of the underlying
        /// distant node data it represents.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null))
                return false;

            if (ReferenceEquals(obj, this))
                return true;
            
            Peer p = obj as Peer;

            if (p?.DistantNodeData == null || DistantNodeData == null)
                return false;

            return p.DistantNodeData.Equals(DistantNodeData);
        }

        #region Closing and disposing
        
        public void Dispose()
        {
            if (IsDisposed)
                return;
            
            _pingPongTimer?.Stop();
            _authTimer?.Stop();
            
            if (_messageReader != null)
            {
                _messageReader.PacketReceived -= ClientOnPacketReceived;
                _messageReader.StreamClosed -= MessageReaderOnStreamClosed;
            }
            
            _messageReader?.Close();
            _messageWriter?.Close();
            
            _client?.Close();

            IsDisposed = true;
        }
        
        #endregion
    }
}