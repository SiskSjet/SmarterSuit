using ProtoBuf;
using Sandbox.ModAPI;
using Sisk.Utils.Net.Messages;

// ReSharper disable ExplicitCallerInfoArgument

namespace Sisk.SmarterSuit.Net.Messages {
    [ProtoContract]
    public class SettingsRequestMessage : IMessage {
        public byte[] Serialize() {
            return MyAPIGateway.Utilities.SerializeToBinary(this);
        }
    }
}