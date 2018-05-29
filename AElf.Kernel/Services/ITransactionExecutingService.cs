using System.Threading.Tasks;

namespace AElf.Kernel.Services
{
    public interface ITransactionExecutingService
    {
        Task ExecuteAsync(ITransaction tx);
    }
}