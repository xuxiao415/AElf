using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using AElf.Network.DataStream;
using AElf.Network.Exceptions;
using AElf.Network.Message;
using Moq;
using Xunit;

namespace AElf.Network.Tests.MessageTests
{
    public class MessageReaderTests
    {
        [Fact]
        public async Task Read_DistantClosed_StopReading()
        {
            Mock<INetworkStream> networkStream = new Mock<INetworkStream>();
            
            networkStream.Setup(ns => ns.ReadBytesAsync(It.IsAny<int>()))
                .Throws(new StreamStoppedException("The end of the stream has been detected."));
            
            MessageReader reader = new MessageReader(networkStream.Object);
            
            List<EventArgs> receivedEvents = new List<EventArgs>();

            reader.ReadingStopped += (sender, args) => {
                receivedEvents.Add(args);
            };

            await reader.Read();
            
            StreamStoppedException ex = AssertReadingStoppedException<StreamStoppedException>(receivedEvents);
            
            Assert.Equal(ex.Message, $"The end of the stream has been detected.");
        }

        public TExType AssertReadingStoppedException<TExType>(List<EventArgs> receivedEvents) where TExType : Exception 
        {
            Assert.Equal(1, receivedEvents.Count);
            ReadingStoppedArgs readingStoppedArgs = Assert.IsType<ReadingStoppedArgs>(receivedEvents[0]);
            
            TExType protocolError = Assert.IsType<TExType>(readingStoppedArgs.Exception);
            return protocolError;
        }
        
        [Fact]
        public async Task Read_LengthHigherThanMax_StopReading()
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

            ProtocolViolationException ex = AssertReadingStoppedException<ProtocolViolationException>(receivedEvents);
            Assert.Equal(ex.Message, $"Received a message that is larger than the maximum " +
                                                $"accepted size ({maxSize} bytes). Size : {maxSize+1} bytes.");
        }

        [Fact]
        public async Task Read_Buffered_TotalMessageSizeToHigh_StopReading()
        {
            //todo
        }
        
        [Fact]
        public async Task Read_Buffered_NonContiguousIndexes_StopReading()
        {
            //todo
        }
    }
}