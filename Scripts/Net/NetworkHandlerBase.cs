using Sandbox.ModAPI;
using Sisk.SmarterSuit.Data;
using Sisk.Utils.Logging;
using Sisk.Utils.Net;

namespace Sisk.SmarterSuit.Net {

    public abstract class NetworkHandlerBase {

        protected NetworkHandlerBase(ILogger log, Network network) {
            Log = log;
            Network = network;
        }

        /// <summary>
        ///     Logger used for logging.
        /// </summary>
        protected ILogger Log { get; private set; }

        /// <summary>
        ///     Network to handle syncing.
        /// </summary>
        protected Network Network { get; private set; }

        /// <summary>
        ///     Close the network message handler.
        /// </summary>
        public virtual void Close() {
            if (Network != null) {
                Network = null;
            }

            if (Log != null) {
                Log = null;
            }
        }

        /// <summary>
        ///     Sync a given option with given value.
        /// </summary>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="option">Which option should be set.</param>
        /// <param name="value">The value for given option.</param>
        public abstract void SyncOption<TValue>(Option option, TValue value);

        /// <summary>
        ///     Checks if player is Admin.
        /// </summary>
        /// <param name="steamId">The steamId used to get the player.</param>
        /// <returns>Return true if player assigned to steamId is a server admin.</returns>
        protected bool IsServerAdmin(ulong steamId) {
            if (Network == null) {
                return true;
            }

            if (Network.IsDedicated) {
                return MyAPIGateway.Utilities.ConfigDedicated.Administrators.Contains(steamId.ToString());
            }

            return MyAPIGateway.Session.LocalHumanPlayer.SteamUserId == steamId;
        }
    }
}