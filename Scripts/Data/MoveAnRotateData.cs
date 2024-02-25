using VRageMath;

namespace Sisk.SmarterSuit.Data {

    public class MoveAnRotateData : Work.Data {

        public MoveAnRotateData(Vector3 moveIndicator, Vector2 rotationIndicator, float rollIndicator) {
            MoveIndicator = moveIndicator;
            RotationIndicator = rotationIndicator;
            RollIndicator = rollIndicator;
        }

        public Vector3 MoveIndicator { get; }
        public float RollIndicator { get; }
        public Vector2 RotationIndicator { get; }
    }
}