using VRageMath;

namespace Sisk.SmarterSuit {
    public struct SuitData {
        public SuitData(bool? dampeners, bool? thruster, bool? helmet, Vector3? linearVelocity, Vector3? angularVelocity) {
            Dampeners = dampeners;
            Thruster = thruster;
            Helmet = helmet;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
        }

        public bool? Dampeners { get; }
        public bool? Thruster { get; }
        public bool? Helmet { get; }
        public Vector3? LinearVelocity { get; }
        public Vector3? AngularVelocity { get; }
    }
}