using System;

namespace AElf.Network.Message
{
    public interface IMessageWriter : IDisposable
    {
        void Start();
        void EnqueueMessage(Message p);

        void Close();
    }
}