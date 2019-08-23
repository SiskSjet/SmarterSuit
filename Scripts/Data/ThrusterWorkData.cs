using VRage.ModAPI;

namespace Sisk.SmarterSuit.Data {
    public class ThrusterWorkData : Work.Data {
        public ThrusterWorkData(IMyEntity lastEntity, bool allowSwitchingDampeners = false, bool leavedLadder = false) {
            LastEntity = lastEntity;
            AllowSwitchingDampeners = allowSwitchingDampeners;
            LeavedLadder = leavedLadder;
        }

        public bool AllowSwitchingDampeners { get; }
        public IMyEntity LastEntity { get; }
        public bool LeavedLadder { get; }
    }
}