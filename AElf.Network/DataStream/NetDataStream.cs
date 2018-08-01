using System.Net.Sockets;
using System.Threading.Tasks;
using AElf.Network.Exceptions;

namespace AElf.Network.DataStream
{
    public class NetDataStream : INetworkStream
    {
        private readonly NetworkStream _stream;
        
        public NetDataStream(NetworkStream stream)
        {
            _stream = stream;
        }
        
        /// <summary>
        /// Reads bytes from the stream.
        /// </summary>
        /// <param name="amount">The amount of bytes we want to read.</param>
        /// <returns>The read bytes.</returns>
        public async Task<byte[]> ReadBytesAsync(int amount)
        {
            if (amount <= 0)
                return new byte[0];
            
            byte[] requestedBytes = new byte[amount];
            
            int receivedIndex = 0;
            while (receivedIndex < amount)
            {
                int readAmount = await _stream.ReadAsync(requestedBytes, receivedIndex, amount - receivedIndex);
                
                if (readAmount == 0)
                    throw new StreamStoppedException("The end of the stream has been detected.");
                
                receivedIndex += readAmount;
            }
            
            return requestedBytes;
        }
        
        #region Closing and disposing

        public void Close()
        {
            Dispose();
        }
        
        public void Dispose()
        {
            // This will cause an IOException in the read loop
            // but since IsConnected is switched to false, it 
            // will not fire the disconnection exception.
            _stream?.Close();
        }

        #endregion
    }
}