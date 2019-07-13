using VRage.ModAPI;

namespace Sisk.SmarterSuit {
    public class ThrusterWorkData : Work.Data {
        public ThrusterWorkData(IMyEntity lastEntity, bool allowSwitchingDampeners = false) {
            LastEntity = lastEntity;
            AllowSwitchingDampeners = allowSwitchingDampeners;
        }

        public bool AllowSwitchingDampeners { get; }
        public IMyEntity LastEntity { get; }
    }
}