using System.Collections.Generic;
using Jakaria;
using ProtoBuf;
using Sandbox.ModAPI;
using Sisk.Utils.Net.Messages;

namespace Sisk.SmarterSuit.Net.Messages {
    [ProtoContract]
    public class WaterModDataSyncMessage : IMessage {
        [ProtoMember(1)]
        public List<Water> Waters { get; set; }

        public byte[] Serialize() {
            return MyAPIGateway.Utilities.SerializeToBinary(this);
        }
    }
}