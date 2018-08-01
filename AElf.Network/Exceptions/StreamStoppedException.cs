using System;

namespace AElf.Network.Exceptions
{
    public class StreamStoppedException : Exception
    {
        public StreamStoppedException() : base() { }
        public StreamStoppedException(string msg) : base(msg) { }
    }
}