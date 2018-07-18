using System.Threading.Tasks;
using AElf.Database;
using AElf.Kernel.Types;

namespace AElf.Kernel.Storages
{
    public class DataStore : IDataStore
    {
        private readonly IKeyValueDatabase _keyValueDatabase;

        public DataStore(IKeyValueDatabase keyValueDatabase)
        {
            _keyValueDatabase = keyValueDatabase;
        }

        public async Task SetDataAsync(Hash pointerHash, byte[] data)
        {
            var key = pointerHash.GetKeyString(TypeName.Bytes);
            await _keyValueDatabase.SetAsync(key, data);
        }

        public async Task<byte[]> GetDataAsync(Hash pointerHash)
        {
            if (pointerHash == null)
            {
                return null;
            }
            var key = pointerHash.GetKeyString(TypeName.Bytes);
            return await _keyValueDatabase.GetAsync(key, typeof(byte[]));
        }
    }
}