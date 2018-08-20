using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using AElf.Network.Connection;
using AElf.Network.Exceptions;
using NLog;

namespace AElf.Network.Sim.Core
{
    public class NetworkSubscriber
    {
        private const int IntLength = 4;

        private readonly ProtobufPeerEventProvider _provider;
        private readonly TcpClient _publisher;
        
        private readonly ILogger _logger;

        private List<TcpClient> _clients = new List<TcpClient>(); 

        private bool _disposed;
        
        public NetworkSubscriber(ProtobufPeerEventProvider provider, ILogger logger)
        {
            _provider = provider;
            _logger = logger;
        }

        public void Start()
        {
            ConnectionListener cl = new ConnectionListener(_logger);
            cl.IncomingConnection += ClOnIncomingConnection;

            Task.Run(() => cl.StartListening(12345));
        }
        
        private void ClOnIncomingConnection(object sender, EventArgs e)
        {
            if (sender != null && e is IncomingConnectionArgs args)
            {
                _clients.Add(args.Client);
                Subscribe(args.Client);
            }
        }

        public void Subscribe(TcpClient client)
        {
            Task.Run( async () => 
            {
                while (!_disposed)
                {
                    int size = await ReadInt(client.GetStream());
                    byte[] bytes = await ReadBytesAsync(client.GetStream(), size);
                    _provider.ReadProtobufByte(bytes);
                }
            });
        }
        
        /// <summary>
        /// Reads bytes from the stream.
        /// </summary>
        /// <param name="amount">The amount of bytes we want to read.</param>
        /// <returns>The read bytes.</returns>
        protected async Task<byte[]> ReadBytesAsync(NetworkStream stream, int amount)
        {
            if (amount == 0)
            {
                _logger.Trace("Read amount is 0");
                return new byte[0];
            }
            
            byte[] requestedBytes = new byte[amount];
            
            int receivedIndex = 0;
            while (receivedIndex < amount)
            {
                int readAmount = await stream.ReadAsync(requestedBytes, receivedIndex, amount - receivedIndex);
                
                if (readAmount == 0)
                    throw new PeerDisconnectedException();
                
                receivedIndex += readAmount;
            }
            
            return requestedBytes;
        }
        
        private async Task<int> ReadInt(NetworkStream stream)
        {
            byte[] intBytes = await ReadBytesAsync(stream, IntLength);
            return BitConverter.ToInt32(intBytes, 0);
        }

        public void Close()
        {
            _publisher?.Dispose();
            _disposed = true;
        }
    }

    
}