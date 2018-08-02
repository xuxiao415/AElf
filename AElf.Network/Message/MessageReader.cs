using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AElf.Common.ByteArrayHelpers;
using AElf.Network.Data;
using AElf.Network.DataStream;
using AElf.Network.Exceptions;
using NLog;

[assembly:InternalsVisibleTo("AElf.Network.Tests.MessageTests")]
namespace AElf.Network.Message
{
    public class ReadingStoppedArgs : EventArgs
    {
        public Exception Exception { get; set; }
    }
    
    public class MessageReader : IMessageReader
    {
        public const int DefaultMaxMessageSize = 1024 * 1024; // 1 MiB
        
        public const int IntLength = 4;
        public const int BooleanLength = 1;

        private ILogger _logger;
        
        private readonly INetworkStream _stream;

        public event EventHandler PacketReceived;
        public event EventHandler ReadingStopped;

        private readonly List<PartialMessage> _partialMessageBuffer;
        private int _partialMessageCurrentSize;
        private int? _currentMessageTotalSize;
        private int? _lastIndexReceived;
        
        public int MaxMessageSize { get; set; } = DefaultMaxMessageSize;

        public bool ReadingFinished { get; private set; }
        
        public MessageReader(INetworkStream stream)
        {
            _partialMessageBuffer = new List<PartialMessage>();
            _stream = stream;
        }
        
        public void Start()
        {
            Task.Run(Read).ConfigureAwait(false);
            _logger = LogManager.GetLogger(nameof(MessageReader));
        }
        
        /// <summary>
        /// Reads the bytes from the stream.
        /// </summary>
        internal async Task Read()
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
            catch (StreamStoppedException e)
            {
                ReadingStopped?.Invoke(this, new ReadingStoppedArgs { Exception = e});
                Close();
            }
            catch (Exception e)
            {
                if (!ReadingFinished && e is IOException)
                {
                    // If the stream fails while the connection is logically closed (call to Close())
                    // we simply return - the StreamClosed event will no be closed.
                    return;
                }

                Close();
                
                ReadingStopped?.Invoke(this, new ReadingStoppedArgs { Exception = e});
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

            if (length > MaxMessageSize)
                ThrowInvalidMessageSizeException(length);
            
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
            
            if (length > MaxMessageSize)
                ThrowInvalidMessageSizeException(length);
            
            // If it's a partial packet read the packet info
            PartialMessage partialMessage = await ReadPartialPacket(length);

            if (partialMessage.IsStart) // Start is index 0
            {
                if (_partialMessageBuffer.Count > 0 || _lastIndexReceived.HasValue || _partialMessageCurrentSize != 0)
                    throw new ProtocolViolationException("Received last partial packet with invalid state.");

                _currentMessageTotalSize = partialMessage.TotalDataSize;
            }
            
            _partialMessageCurrentSize += length;

            if (!_lastIndexReceived.HasValue)
            {
                _lastIndexReceived = partialMessage.Position;
            }
            else
            {
                
            }

            if (!_currentMessageTotalSize.HasValue)
            {
                _currentMessageTotalSize = partialMessage.TotalDataSize;
                if (partialMessage.TotalDataSize > MaxMessageSize)
                    throw new ProtocolViolationException($"Received a message that is larger than the maximum " +
                                                         $"accepted size ({MaxMessageSize} bytes). Size : {partialMessage.TotalDataSize} bytes.");
            }
                
            // todo property control
            // todo Contiguous position in the list - keep track of last index

            if (!partialMessage.IsEnd)
            {
                _partialMessageBuffer.Add(partialMessage);
                _logger.Trace($"Received message part : {(MessageType) type}, position : {partialMessage.Position}, length : {length}");
                
                ValidatePartialMessageReception();
                
                // If only partial reception return no message
                return null;
            }
            
            // This is the last packet: concat all data 

            _partialMessageBuffer.Add(partialMessage);

            byte[] allData = ByteArrayHelpers.Combine(_partialMessageBuffer.Select(pp => pp.Data).ToArray());

            _logger.Trace($"Received last message part : { _partialMessageBuffer.Count }, total length : { allData.Length }");
                
            ResetBufferedMessageState();

            return new Message { Type = type, Length = allData.Length, Payload = allData };
        }

        private void ValidatePartialMessageReception()
        {
            if (_partialMessageBuffer.Count <= 0 || _partialMessageCurrentSize == 0 || !_lastIndexReceived.HasValue)
                throw new ProtocolViolationException("Received last partial packet with invalid state.");
        }

        private void ResetBufferedMessageState()
        {
            _partialMessageBuffer?.Clear();
            _partialMessageCurrentSize = 0;
            _lastIndexReceived = null;
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
            byte[] isBuffered = await _stream.ReadBytesAsync(BooleanLength);
            return isBuffered[0] != 0;
        }

        private async Task<PartialMessage> ReadPartialPacket(int dataLength)
        {
            PartialMessage partialMessage = new PartialMessage();

            partialMessage.Position = await ReadInt();
            partialMessage.IsEnd = await ReadBoolean();
            partialMessage.TotalDataSize = await ReadInt();
            
            // Read the data
            byte[] packetData = await _stream.ReadBytesAsync(dataLength);
            partialMessage.Data = packetData;
            
            return partialMessage;
        }

        private void ThrowInvalidMessageSizeException(int size, bool isPartial = false)
        {
            string msg = $"Received a message that is larger than the maximum accepted size ({MaxMessageSize} bytes). Size : {size} bytes.";

            if (isPartial)
                msg += "(partial)";
                    
            throw new ProtocolViolationException(msg);
        }
        
        #region Closing and disposing

        public void Close()
        {
            ResetBufferedMessageState();
            Dispose();
        }
        
        public void Dispose()
        {
            // Change logical connection state
            ReadingFinished = true;
            
            // This will cause an IOException in the read loop
            // but since IsConnected is switched to false, it 
            // will not fire the disconnection exception.
            _stream?.Close();
        }

        #endregion
    }
}