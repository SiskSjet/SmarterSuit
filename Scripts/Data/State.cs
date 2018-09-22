namespace Sisk.SmarterSuit.Data {
    /// <summary>
    ///     States to determine which actions should executed.
    /// </summary>
    public enum State {
        /// <summary>
        ///     Nothing to do.
        /// </summary>
        None,

        /// <summary>
        ///     After the player has exited a cockpit.
        /// </summary>
        ExitCockpit,

        /// <summary>
        ///     After the player has respawned.
        /// </summary>
        Respawn,

        /// <summary>
        ///     Extra check for oxygen if Pressurization is enabled, because of false oxygen values for a couple of ticks.
        /// </summary>
        CheckOxygenAfterDelay
    }
}