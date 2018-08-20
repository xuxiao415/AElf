using System;
using System.Collections.Generic;
using AElf.Network.Data;

namespace AElf.Network.Peers
{
    public interface IPeerManager : IDisposable
    {
        event EventHandler PeerAdded;
        
        void Start();
        List<NodeData> GetPeers();
    }
}