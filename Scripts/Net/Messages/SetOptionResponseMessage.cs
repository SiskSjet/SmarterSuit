using ProtoBuf;
using Sandbox.ModAPI;
using Sisk.SmarterSuit.Data;
using Sisk.Utils.Net.Messages;

// ReSharper disable ExplicitCallerInfoArgument

namespace Sisk.SmarterSuit.Net.Messages {
    [ProtoContract]
    public class SetOptionResponseMessage : IMessage {
        [ProtoMember(2)] public Option Option;

        [ProtoMember(1)]
        public Result Result { get; set; }

        [ProtoMember(3)]
        public byte[] Value { get; set; }

        public byte[] Serialize() {
            return MyAPIGateway.Utilities.SerializeToBinary(this);
        }
    }
}