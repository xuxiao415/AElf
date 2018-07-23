using System.Threading.Tasks;
using AElf.Kernel.Types;

namespace AElf.Kernel.Storages
{
    public interface IDataStore
    {
        Task SetDataAsync(Hash pointerHash, TypeName typeName, byte[] data);
        Task<byte[]> GetDataAsync(Hash pointerHash, TypeName typeName);
    }
}