﻿using System.Collections.Generic;
using Google.Protobuf.Collections;
using AElf.Common;

namespace AElf.Kernel
{
    public interface IBlockBody: IHashProvider
    {
        RepeatedField<Hash> Transactions { get; }

        bool AddTransaction(Hash tx);

        bool AddTransactions(IEnumerable<Hash> hashes);
        
        void Complete(Hash blockHeaderHash);
        
        RepeatedField<SideChainBlockInfo> IndexedInfo { get; }
    }
}