using System;
using System.Threading.Tasks;

namespace AElf.Network.DataStream
{
    public interface INetworkStream : IDisposable
    {
        Task<byte[]> ReadBytesAsync(int amount);
        void Close();
    }
}