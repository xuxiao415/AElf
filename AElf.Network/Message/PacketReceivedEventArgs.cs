using System;

namespace AElf.Network.Message
{
    public class PacketReceivedEventArgs : EventArgs
    {
        public Message Message { get; set; }
    }
}