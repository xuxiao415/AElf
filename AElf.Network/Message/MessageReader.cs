using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common.ByteArrayHelpers;
using AElf.Network.Data;
using AElf.Network.DataStream;
using AElf.Network.Exceptions;
using NLog;

namespace AElf.Network.Message
{
    public class MessageReader : IMessageReader
    {   
        private const int IntLength = 4;

        private ILogger _logger;
        
        private readonly INetworkStream _stream;

        public event EventHandler PacketReceived;
        public event EventHandler StreamClosed;

        private readonly List<PartialPacket> _partialPacketBuffer;

        public bool IsConnected { get; private set; }
        
        public MessageReader(INetworkStream stream)
        {
            _partialPacketBuffer = new List<PartialPacket>();
            
            _stream = stream;
        }
        
        public void Start()
        {
            Task.Run(Read).ConfigureAwait(false);
            IsConnected = true;

            _logger = LogManager.GetLogger(nameof(MessageReader));
        }
        
        /// <summary>
        /// Reads the bytes from the stream.
        /// </summary>
        private async Task Read()
        {
            try
            {
                while (true)
                {
                    // Read type 
                    int type = await ReadByte();

                    // Is this a partial reception ?
                    bool isBuffered = await ReadBoolean();

                    Message message;
                    
                    if (isBuffered)
                    {
                        message = await ReadBufferedMessage(type);
                    }
                    else
                    {
                        message = await ReadMessage(type);
                    }
                    
                    if (message != null)
                    {
                        _logger.Trace($"Received message, type : {(MessageType)type}, length : {message.Length} bytes.");
                        FireMessageReceivedEvent(message);
                    }
                }
            }
            catch (PeerDisconnectedException e)
            {
                _logger.Trace(e, "Connection was aborted.\n");
                StreamClosed?.Invoke(this, EventArgs.Empty);
                
                Close();
            }
            catch (Exception e)
            {
                if (!IsConnected && e is IOException)
                {
                    // If the stream fails while the connection is logically closed (call to Close())
                    // we simply return - the StreamClosed event will no be closed.
                    return;
                }

                Close();
                
                _logger.Trace(e, "[Message reader] Connection was aborted.\n");
                
                StreamClosed?.Invoke(this, EventArgs.Empty);
            }
        }

        private void FireMessageReceivedEvent(Message message)
        {
            PacketReceivedEventArgs args = new PacketReceivedEventArgs { Message = message };

            PacketReceived?.Invoke(this, args);
        }

        private async Task<Message> ReadMessage(int type)
        {
            // Read the size of the data
            int length = await ReadInt();
            
            // If it's not a partial packet the next "length" bytes should be 
            // the entire data

            byte[] packetData = await _stream.ReadBytesAsync(length);

            Message message = new Message { Type = type, Length = length, Payload = packetData };

            return message;
        }

        private async Task<Message> ReadBufferedMessage(int type)
        {
            // Read the size of the data
            int length = await ReadInt();
            
            // If it's a partial packet read the packet info
            PartialPacket partialPacket = await ReadPartialPacket(length);

            // todo property control

            if (!partialPacket.IsEnd)
            {
                _partialPacketBuffer.Add(partialPacket);
                _logger.Trace($"Received message part : { (MessageType) type }, length : { length }");
                
                // If only partial reception return no message
                return null;
            }
            
            // This is the last packet
            // Concat all data 

            _partialPacketBuffer.Add(partialPacket);

            byte[] allData =
                ByteArrayHelpers.Combine(_partialPacketBuffer.Select(pp => pp.Data).ToArray());

            _logger.Trace($"Received last message part : { _partialPacketBuffer.Count }, total length : { allData.Length }");

            // Clear the buffer for the next partial to receive 
            _partialPacketBuffer.Clear();

            return new Message { Type = type, Length = allData.Length, Payload = allData };
        }

        private async Task<int> ReadByte()
        {
            byte[] type = await _stream.ReadBytesAsync(1);
            return type[0];
        }

        private async Task<int> ReadInt()
        {
            byte[] intBytes = await _stream.ReadBytesAsync(IntLength);
            return BitConverter.ToInt32(intBytes, 0);
        }

        private async Task<bool> ReadBoolean()
        {
            byte[] isBuffered = await _stream.ReadBytesAsync(1);
            return isBuffered[0] != 0;
        }

        private async Task<PartialPacket> ReadPartialPacket(int dataLength)
        {
            PartialPacket partialPacket = new PartialPacket();

            partialPacket.Position = await ReadInt();
            partialPacket.IsEnd = await ReadBoolean();
            partialPacket.TotalDataSize = await ReadInt();
            
            // Read the data
            byte[] packetData = await _stream.ReadBytesAsync(dataLength);
            partialPacket.Data = packetData;
            
            return partialPacket;
        }
        
        #region Closing and disposing

        public void Close()
        {
            Dispose();
        }
        
        public void Dispose()
        {
            // Change logical connection state
            IsConnected = false;
            
            // This will cause an IOException in the read loop
            // but since IsConnected is switched to false, it 
            // will not fire the disconnection exception.
            _stream?.Close();
        }

        #endregion
    }
}