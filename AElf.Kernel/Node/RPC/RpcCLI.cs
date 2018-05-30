using System;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using NLog;

namespace AElf.Kernel.Node.RPC
{
    public class RpcCLI
    {
        private MainChainNode _node;

        /// <summary>
        /// This method start the server that listens for incoming
        /// connections and sets up the manager.
        /// </summary>
        /// <param name="node"></param>
        public void Start(MainChainNode node)
        {
            _node = node;
            Greeting();
        }
        
        /// <summary>
        /// This initialises the CLI and provides the user
        /// with a menu.
        /// </summary>
        private void Greeting()
        {
            Console.WriteLine("Welcome to AElf\n" +
                              "This is the command-line interface for the RPC API.\n" +
                              "Please select from the following options:\n" +
                              "(1) Get Transaction by TxID\n" +
                              "(2) Insert Transaction\n" +
                              "(3) Broadcast Transaction\n");

            string exec = Console.ReadLine();

            switch (exec)
            {
                case "1":
                    GetTx();
                    break;
                case "2":
                    InsertTx();
                    break;
                case "3":
                    break;
                default:
                    break;
            }
        }

        private async void GetTx()
        {
            Console.Clear();
            Console.WriteLine("Please enter the hash of the transaction you are looking for:\n");
            string hash = Console.ReadLine();
            await _node.GetTransaction(new Hash(ByteString.CopyFrom(hash, Encoding.Unicode)));
            
            throw new NotImplementedException(); // Do we want to return the tx as JSON to the console?
        }

        private async void InsertTx()
        {
            Console.Clear();
            Console.WriteLine("Please enter the transaction you wish to insert:\n");
        }
    }
}