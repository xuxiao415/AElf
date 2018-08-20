using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Google.Protobuf;
using NetMQ;
using NetMQ.Sockets;

namespace AElf.Network.Sim.PeerManagement
{
    public class TestNode
    {
        public SubscriberSocket _socket;
        
        private Process _process;

        private bool _hasStopped = false;

        public void Start(string args, string port)
        {
            try
            {
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                        Arguments = @"run --no-build -p ../../../../AElf.Network.Node/AElf.Network.Node.csproj " + args,
                        UseShellExecute = true,
                    }
                };
                
                _process.Start();
            }
            catch (Exception e)
            {
                ;
            }
        }

        public void Stop()
        {
            _process.Close();
            _socket.Dispose();
            _hasStopped = true;
        }
    }
}