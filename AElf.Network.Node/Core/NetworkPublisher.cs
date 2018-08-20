using System;
using System.Net.Sockets;

namespace AElf.Network.Node.Core
{
    public class NetworkPublisher
    {
        private const int IntLength = 4;

        private TcpClient _subscriber;
            
        public NetworkPublisher(TcpClient client)
        {
            _subscriber = client;
        }
        
        public void Publish(byte[] message)
        {
            int size = message.Length;
            
            _subscriber.GetStream().Write(BitConverter.GetBytes(size), 0, IntLength);
            _subscriber.GetStream().Write(message, 0, size);
        }

        public void Close()
        {
            _subscriber?.Close();
        }
    }
}