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

        public async Task SetDataAsync(Hash pointerHash, TypeName typeName, byte[] data)
        {
            var key = pointerHash.GetKeyString(typeName);
            await _keyValueDatabase.SetAsync(key, data);
        }

        public async Task<byte[]> GetDataAsync(Hash pointerHash, TypeName typeName)
        {
            if (pointerHash == null)
            {
                return null;
            }
            var key = pointerHash.GetKeyString(typeName);
            return await _keyValueDatabase.GetAsync(key, typeof(byte[]));
        }
    }
}