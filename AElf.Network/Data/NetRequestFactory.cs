using AElf.Network.Data;
using Google.Protobuf;

namespace AElf.Network.Data
{
    public static class NetRequestFactory
    {
        public static Message.Message CreateMissingPeersReq(int peersMissing)
        {
            var reqPeerListData = new ReqPeerListData { NumPeers = peersMissing };
            var payload = reqPeerListData.ToByteString().ToByteArray();

            var request = new Message.Message
            {
                Type = (int) MessageType.RequestPeers,
                Length = payload.Length,
                Payload = payload
            };

            return request;
        }

        public static Message.Message CreateMessage(MessageType messageType, byte[] payload)
        {
            Message.Message packetData = new Message.Message
            {
                Type = (int)messageType,
                Length = payload.Length,
                Payload = payload
            };

            return packetData;
        }
    }
}