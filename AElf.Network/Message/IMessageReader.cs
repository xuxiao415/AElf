using System;

namespace AElf.Network.Message
{
    public interface IMessageReader : IDisposable
    {
        event EventHandler PacketReceived;
        event EventHandler StreamClosed;
        
        void Start();
        void Close();
    }
}