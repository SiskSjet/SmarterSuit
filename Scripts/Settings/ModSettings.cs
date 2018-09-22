using System.ComponentModel;
using System.Xml.Serialization;
using ProtoBuf;

// ReSharper disable ExplicitCallerInfoArgument

namespace Sisk.SmarterSuit.Settings {
    [ProtoContract]
    public class ModSettings {
        public const int VERSION = 1;
        public const bool AUTO_HELMET_EVERYWHERE = false;

        [ProtoMember(1)]
        [XmlElement(Order = 1)]
        public int Version { get; set; } = VERSION;

        [ProtoMember(2)]
        [DefaultValue(false)]
        [XmlElement(Order = 2)]
        public bool AlwaysAutoHelmet { get; set; } = AUTO_HELMET_EVERYWHERE;
    }
}