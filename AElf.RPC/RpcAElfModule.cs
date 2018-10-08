using System;
using AElf.Common.Module;
using AElf.Configuration.Config.RPC;
using Autofac;

namespace AElf.RPC
{
    public class RpcAElfModule:IAElfModule
    {
        public void Init(ContainerBuilder builder)
        {
            Console.WriteLine("rpc begin init");
            builder.RegisterModule(new RpcAutofacModule());
        }

        public void Run(ILifetimeScope scope)
        {
            Console.WriteLine("rpc begin init run");
            var rpc = scope.Resolve<IRpcServer>();
            var result = rpc.Init(scope, RpcConfig.Instance.Host, RpcConfig.Instance.Port);
            Console.WriteLine("rpc init result:" + result);
        }
    }
}