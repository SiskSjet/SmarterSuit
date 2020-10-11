using ProtoBuf;
using Sandbox.ModAPI;
using Sisk.Utils.Net.Messages;

namespace Sisk.SmarterSuit.Net.Messages {
    [ProtoContract]
    public class WaterModDataRequestMessage : IMessage {
        public byte[] Serialize() {
            return MyAPIGateway.Utilities.SerializeToBinary(this);
        }
    }
}