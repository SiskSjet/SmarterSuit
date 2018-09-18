namespace Sisk.SmarterSuit {
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
        ///     Extra step after a respawn if Pressurization is enabled
        /// </summary>
        CheckOxygenAfterRespawn
    }
}