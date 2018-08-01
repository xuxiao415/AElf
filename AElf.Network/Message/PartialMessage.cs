namespace AElf.Network.Message
{
    public class PartialMessage
    {
        public int Type { get; set; }
        public int Position { get; set; }
        public bool IsEnd { get; set; }
        public int TotalDataSize { get; set; }
        
        public byte[] Data { get; set; }
    }
}