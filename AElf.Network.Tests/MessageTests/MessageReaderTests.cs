using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using AElf.Common.ByteArrayHelpers;
using AElf.Network.DataStream;
using AElf.Network.Exceptions;
using AElf.Network.Message;
using Moq;
using Xunit;

namespace AElf.Network.Tests.MessageTests
{
    public class MessageReaderTests
    {
        #region Helpers

        public class MessageByteFields
        {
            public byte[] Type { get; set; }
            public byte[] IsBuffered { get; set; }
            public byte[] Size { get; set; }
            public byte[] Data { get; set; }
            
            public byte[] Position { get; set; }
            public byte[] IsEnd { get; set; }
            public byte[] TotalLength { get; set; }
            
        }

        public MessageByteFields CreateMessage(int type, int size, 
            bool isBuffered = false, int position = 0, bool isEnd = false, int totalLength = 0)
        {
            var msg = new MessageByteFields
            {
                Type = new[] { (byte)type },
                IsBuffered = new[] { (byte)(isBuffered ? 1 : 0) },
                Size = BitConverter.GetBytes(size),
                Data = ByteArrayHelpers.RandomFill(size)
            };

            if (isBuffered)
            {
                msg.Position = BitConverter.GetBytes(position);
                msg.IsEnd = new[] {(byte) (isEnd ? 1 : 0)};
                msg.TotalLength = BitConverter.GetBytes(totalLength);
            }

            return msg;
        }
        
        #endregion
        
        #region Smoke Test

        [Fact]
        public async Task Read_BasicSmokeTest_Message()
        {
            Mock<INetworkStream> networkStream = new Mock<INetworkStream>();

        }
        
        [Fact]
        public async Task Read_BasicSmokeTest_BufferedMessage()
        {
            
        }

        #endregion Smoke Test

        #region Reading stopped tests
        
        public TExType AssertReadingStoppedException<TExType>(List<EventArgs> receivedEvents) where TExType : Exception 
        {
            Assert.Equal(1, receivedEvents.Count);
            ReadingStoppedArgs readingStoppedArgs = Assert.IsType<ReadingStoppedArgs>(receivedEvents[0]);
            
            TExType protocolError = Assert.IsType<TExType>(readingStoppedArgs.Exception);
            return protocolError;
        }

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
            Assert.True(reader.ReadingFinished);
        }
        
        [Fact]
        public async Task Read_LengthHigherThanMax_StopReading()
        {
            int maxSize = 1024;

            var msg = CreateMessage(0, maxSize+1);
            
            Mock<INetworkStream> networkStream = new Mock<INetworkStream>();
            networkStream.SetupSequence(ns => ns.ReadBytesAsync(It.IsAny<int>()))
                .ReturnsAsync(msg.Type)
                .ReturnsAsync(msg.IsBuffered)
                .ReturnsAsync(msg.Size);
            
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

        #endregion Reading stopped tests
    }
}