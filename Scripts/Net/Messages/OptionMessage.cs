using ProtoBuf;
using Sandbox.ModAPI;
using Sisk.SmarterSuit.Data;
using Sisk.Utils.Net.Messages;

// ReSharper disable ExplicitCallerInfoArgument
namespace Sisk.SmarterSuit.Net.Messages {
    [ProtoContract]
    public class OptionMessage : IMessage {
        [ProtoMember(1)]
        public Option Option { get; set; }

        [ProtoMember(2)]
        public byte[] Value { get; set; }

        public byte[] Serialize() {
            return MyAPIGateway.Utilities.SerializeToBinary(this);
        }
    }
}