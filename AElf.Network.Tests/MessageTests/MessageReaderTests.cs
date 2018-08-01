using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using AElf.Network.DataStream;
using AElf.Network.Message;
using Moq;
using Xunit;

namespace AElf.Network.Tests.MessageTests
{
    public class MessageReaderTests
    {
        [Fact]
        public async Task Read_LengthHigherThanMax_ThrowInvalidProtocol()
        {
            int maxSize = 1024;

            byte[] type = {0};
            byte[] isBuffered = {0};
            byte[] intBytes = BitConverter.GetBytes(maxSize + 1);
            
            Mock<INetworkStream> networkStream = new Mock<INetworkStream>();
            networkStream.SetupSequence(ns => ns.ReadBytesAsync(It.IsAny<int>()))
                .ReturnsAsync(type)
                .ReturnsAsync(isBuffered)
                .ReturnsAsync(intBytes);
            
            MessageReader reader = new MessageReader(networkStream.Object);
            reader.MaxMessageSize = maxSize;
            
            List<EventArgs> receivedEvents = new List<EventArgs>();

            reader.ReadingStopped += (sender, args) => {
                receivedEvents.Add(args);
            };

            await reader.Read();
            
            Assert.Equal(1, receivedEvents.Count);
            ReadingStoppedArgs readingStoppedArgs = Assert.IsType<ReadingStoppedArgs>(receivedEvents[0]);
            ProtocolViolationException protocolError = Assert.IsType<ProtocolViolationException>(readingStoppedArgs.Exception);
            
            Assert.Equal(protocolError.Message, $"Received a message that is larger than the maximum " +
                                     $"accepted size ({maxSize} bytes). Size : {maxSize+1} bytes.");
        }
    }
}