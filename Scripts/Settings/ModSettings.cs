using System.ComponentModel;
using System.Xml.Serialization;
using ProtoBuf;
using Sisk.SmarterSuit.Data;

// ReSharper disable ExplicitCallerInfoArgument

namespace Sisk.SmarterSuit.Settings {
    [ProtoContract]
    public class ModSettings {
        public const int VERSION = 1;
        private const bool ADDITIONAL_FUEL_WARNING = false;
        private const bool AUTO_HELMET_EVERYWHERE = false;
        private const DisableAutoDamenerOption DISABLE_AUTO_DAMPENER = DisableAutoDamenerOption.Disable;
        private const float FUEL_THRESHOLD = 0.25f;

        [ProtoMember(3)]
        [DefaultValue(ADDITIONAL_FUEL_WARNING)]
        [XmlElement(Order = 3)]
        public bool AdditionalFuelWarning { get; set; } = ADDITIONAL_FUEL_WARNING;

        [ProtoMember(2)]
        [DefaultValue(AUTO_HELMET_EVERYWHERE)]
        [XmlElement(Order = 2)]
        public bool AlwaysAutoHelmet { get; set; } = AUTO_HELMET_EVERYWHERE;

        [ProtoMember(5)]
        [DefaultValue(DISABLE_AUTO_DAMPENER)]
        [XmlElement(Order = 5)]
        public DisableAutoDamenerOption DisableAutoDampener { get; set; } = DISABLE_AUTO_DAMPENER;

        [ProtoMember(4)]
        [DefaultValue(FUEL_THRESHOLD)]
        [XmlElement(Order = 4)]
        public float FuelThreshold { get; set; } = FUEL_THRESHOLD;

        [ProtoMember(1)]
        [XmlElement(Order = 1)]
        public int Version { get; set; } = VERSION;
    }
}