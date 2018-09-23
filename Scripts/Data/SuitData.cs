using VRageMath;

// ReSharper disable ExplicitCallerInfoArgument

namespace Sisk.SmarterSuit.Data {
    /// <summary>
    ///     A simple data structure which holds information which suit system should be enabled/disabled.
    /// </summary>
    public struct SuitData {
        /// <summary>
        ///     Creates a new instance.
        /// </summary>
        /// <param name="dampeners">Indicates whether dampeners should be enabled/disabled.</param>
        /// <param name="thruster">Indicates whether thruster should be enabled/disabled.</param>
        /// <param name="helmet">Indicates whether helmet should be opened/closed.</param>
        /// <param name="linearVelocity">Indicates whether linearVelocity should be set.</param>
        /// <param name="angularVelocity">Indicates whether angularVelocity should be set.</param>
        public SuitData(bool? dampeners, bool? thruster, bool? helmet, Vector3? linearVelocity, Vector3? angularVelocity) {
            Dampeners = dampeners;
            Thruster = thruster;
            Helmet = helmet;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
        }

        /// <summary>
        ///     Indicates whether dampeners should be enabled/disabled.
        /// </summary>
        public bool? Dampeners { get; }

        /// <summary>
        ///     Indicates whether thruster should be enabled/disabled.
        /// </summary>
        public bool? Thruster { get; }

        /// <summary>
        ///     Indicates whether helmet should be opened/closed.
        /// </summary>
        public bool? Helmet { get; }

        /// <summary>
        ///     Indicates whether linearVelocity should be set.
        /// </summary>
        public Vector3? LinearVelocity { get; }

        /// <summary>
        ///     Indicates whether angularVelocity should be set.
        /// </summary>
        public Vector3? AngularVelocity { get; }
    }
}