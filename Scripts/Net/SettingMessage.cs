using ProtoBuf;
using Sandbox.ModAPI;
using Sisk.SmarterSuit.Settings;
using Sisk.Utils.Net.Messages;

// ReSharper disable ExplicitCallerInfoArgument

namespace Sisk.SmarterSuit.Net {
    [ProtoContract]
    public class SettingMessage : IMessage {
        [ProtoMember(2)]
        public ModSettings Settings { get; set; }

        [ProtoMember(1)]
        public ulong SteamId { get; set; }

        public byte[] Serialize() {
            return MyAPIGateway.Utilities.SerializeToBinary(this);
        }
    }
}