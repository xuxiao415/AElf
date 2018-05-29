using System.Threading.Tasks;
using AElf.Kernel.Extensions;
using AElf.Kernel.KernelAccount;
using AElf.Kernel.Services;
using Google.Protobuf;
using Xunit;
using Xunit.Frameworks.Autofac;

namespace AElf.Kernel.Tests
{
    [UseAutofacTestFramework]
    public class TransactionExecutingServiceTests
    {

        private readonly ITransactionExecutingService _transactionExecutingService;

        private readonly IChainCreationService _chainCreationService;

        private readonly IChainContextService _chainContextService;

        public TransactionExecutingServiceTests(ITransactionExecutingService transactionExecutingService, 
            IChainCreationService chainCreationService, IChainContextService chainContextService)
        {
            _transactionExecutingService = transactionExecutingService;
            _chainCreationService = chainCreationService;
            _chainContextService = chainContextService;
        }

        [Fact]
        public async Task TestTxExecuting()
        {
            Hash chain = new Hash("MainChain".CalculateHash());

            await _chainCreationService.CreateNewChainAsync(chain,typeof(SmartContractZero));
            
            Transaction tx=new Transaction()
            {
                From = Hash.Zero,
                To = Hash.Zero,
                MethodName = nameof(SmartContractZero.DeploySmartContract),
                IncrementId = 1,
                Params = ByteString.Empty
            };

            var context = _chainContextService.GetChainContext(chain);
            await _transactionExecutingService.ExecuteAsync(tx );
        }
    }
}