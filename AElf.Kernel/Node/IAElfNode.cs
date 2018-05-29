using System.Threading.Tasks;

namespace AElf.Kernel.Node
{
    public interface IAElfNode
    {
        void Start(bool startRpc);
        Task<ITransaction> GetTransaction(Hash txId);
        Task<IHash> InsertTransaction(Transaction tx);
        Task BroadcastTransaction(Transaction tx);
    }
}