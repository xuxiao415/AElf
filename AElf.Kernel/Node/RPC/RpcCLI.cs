using System;

namespace AElf.Kernel.Node.RPC
{
    public class RpcCLI
    {
        static void Main(string[] args)
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
                    break;
                case "2":
                    break;
                case "3":
                    break;
                default:
                    break;
            }
        }
    }
}