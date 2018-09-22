﻿using ProtoBuf;
using Sandbox.ModAPI;
using Sisk.Utils.Net.Messages;

// ReSharper disable ExplicitCallerInfoArgument

namespace Sisk.SmarterSuit.Net {
    [ProtoContract]
    public class RequestSettingsMessage : IMessage {
        [ProtoMember(1)]
        public ulong SteamId { get; set; }

        public byte[] Serialize() {
            return MyAPIGateway.Utilities.SerializeToBinary(this);
        }
    }
}