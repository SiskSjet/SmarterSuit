using Sandbox.ModAPI;
using Sisk.SmarterSuit.Data;
using Sisk.SmarterSuit.Net.Messages;
using Sisk.Utils.Logging;
using Sisk.Utils.Net;

namespace Sisk.SmarterSuit.Net {
    public class ClientHandler : NetworkHandlerBase {
        public ClientHandler(ILogger log, Network network) : base(log.ForScope<ClientHandler>(), network) {
            Network.Register<SettingsResponseMessage>(OnSettingsResponseMessage);
            Network.Register<SetOptionResponseMessage>(OnSetOptionResponseMessage);
            Network.Register<SetOptionSyncMessage>(OnSetOptionsSyncMessage);
        }

        /// <inheritdoc />
        public override void Close() {
            Network.Unregister<SettingsResponseMessage>(OnSettingsResponseMessage);
            Network.Unregister<SetOptionResponseMessage>(OnSetOptionResponseMessage);
            Network.Unregister<SetOptionSyncMessage>(OnSetOptionsSyncMessage);
            base.Close();
        }

        /// <inheritdoc />
        public override void SyncOption<TValue>(Option option, TValue value) {
            var steamUserId = MyAPIGateway.Session.LocalHumanPlayer.SteamUserId;
            var message = new SetOptionMessage { SteamId = steamUserId, Option = option, Value = MyAPIGateway.Utilities.SerializeToBinary(value) };
            Network.SendToServer(message);
        }

        /// <summary>
        ///     Set option response handler.
        /// </summary>
        /// <param name="sender">The sender who send the option message.</param>
        /// <param name="message">The option message received.</param>
        private void OnSetOptionResponseMessage(ulong sender, SetOptionResponseMessage message) {
            object value = null;
            switch (message.Option) {
                case Option.AlwaysAutoHelmet:
                case Option.AdditionalFuelWarning:
                    value = MyAPIGateway.Utilities.SerializeFromBinary<bool>(message.Value);
                    break;
                case Option.FuelThreshold:
                    value = MyAPIGateway.Utilities.SerializeFromBinary<float>(message.Value);
                    break;
            }

            Mod.ShowResultMessage(message.Option, value, message.Result);
        }

        /// <summary>
        ///     Set option sync message.
        /// </summary>
        /// <param name="sender">The sender who send the option message.</param>
        /// <param name="message">The option message received.</param>
        private void OnSetOptionsSyncMessage(ulong sender, SetOptionSyncMessage message) {
            switch (message.Option) {
                case Option.AlwaysAutoHelmet:
                case Option.AdditionalFuelWarning:
                    Mod.Static.SetOption(message.Option, MyAPIGateway.Utilities.SerializeFromBinary<bool>(message.Value));
                    break;
                case Option.FuelThreshold:
                    Mod.Static.SetOption(message.Option, MyAPIGateway.Utilities.SerializeFromBinary<float>(message.Value));
                    break;
            }

            using (Log.BeginMethod(nameof(OnSetOptionsSyncMessage))) {
                Log.Debug($"Sync: {message.Option} option");
                Log.Debug($"AlwaysAutoHelmet: {Mod.Static.Settings.AlwaysAutoHelmet}");
                Log.Debug($"AdditionalFuelWarning: {Mod.Static.Settings.AdditionalFuelWarning}");
                Log.Debug($"FuelThreshold: {Mod.Static.Settings.FuelThreshold}");
            }
        }

        /// <summary>
        ///     Settings received message handler.
        /// </summary>
        /// <param name="sender">The sender of the message.</param>
        /// <param name="message">The message.</param>
        private void OnSettingsResponseMessage(ulong sender, SettingsResponseMessage message) {
            if (message.Settings != null) {
                Mod.Static.OnSettingsReceived(message.Settings);
            }
        }
    }
}