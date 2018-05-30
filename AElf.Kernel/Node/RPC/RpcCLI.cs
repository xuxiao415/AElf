using System;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using NLog;

namespace AElf.Kernel.Node.RPC
{
    public class RpcCLI
    {
        private RpcServer _server; 

        /// <summary>
        /// This method start the server that listens for incoming
        /// connections and sets up the manager.
        /// </summary>
        /// <param name="node"></param>
        public void Start(RpcServer server)
        {
            _server = server;
            Greeting();
        }
        
        /// <summary>
        /// This initialises the CLI and provides the user
        /// with a menu.
        /// </summary>
        private static void Greeting()
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

        private static void GetTx()
        {
            Console.Clear();
            Console.WriteLine("Please enter the hash of the transaction you are looking for:\n");
            string hash = Console.ReadLine();
            
            // trigger _server.ProcessGetTx() - Build the JObject by asking for values?
            
            throw new NotImplementedException(); // Do we want to return the tx as JSON to the console?
        }

        private static void InsertTx()
        {
            Console.Clear();
            Console.WriteLine("Please enter the transaction you wish to insert:\n");
            
            throw new NotImplementedException(); // JObject?
        }
    }
}