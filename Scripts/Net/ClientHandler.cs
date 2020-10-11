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
            Network.Register<SetOptionSyncMessage>(OnSetOptionSyncMessage);

            if (Mod.Static.WaterModAvailable) {
                Network.Register<WaterModDataSyncMessage>(OnWaterModDataReceived);
            }
        }

        /// <inheritdoc />
        public override void Close() {
            Network.Unregister<SettingsResponseMessage>(OnSettingsResponseMessage);
            Network.Unregister<SetOptionResponseMessage>(OnSetOptionResponseMessage);
            Network.Unregister<SetOptionSyncMessage>(OnSetOptionSyncMessage);

            if (Mod.Static.WaterModAvailable) {
                Network.Unregister<WaterModDataSyncMessage>(OnWaterModDataReceived);
            }

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
                case Option.AlignToGravity:
                    value = MyAPIGateway.Utilities.SerializeFromBinary<bool>(message.Value);
                    break;
                case Option.FuelThreshold:
                case Option.HaltedSpeedTolerance:
                    value = MyAPIGateway.Utilities.SerializeFromBinary<float>(message.Value);
                    break;
                case Option.AlignToGravityDelay:
                case Option.DelayAfterManualHelmet:
                    value = MyAPIGateway.Utilities.SerializeFromBinary<int>(message.Value);
                    break;
                case Option.DisableAutoDampener:
                    value = MyAPIGateway.Utilities.SerializeFromBinary<DisableAutoDampenerOption>(message.Value);
                    break;
                default:
                    using (Log.BeginMethod(nameof(OnSetOptionResponseMessage))) {
                        Log.Error($"Unknown option '{nameof(message.Option)}'");
                    }

                    return;
            }

            Mod.ShowResultMessage(message.Option, value, message.Result);
        }

        /// <summary>
        ///     Set option sync message.
        /// </summary>
        /// <param name="sender">The sender who send the option message.</param>
        /// <param name="message">The option message received.</param>
        private void OnSetOptionSyncMessage(ulong sender, SetOptionSyncMessage message) {
            switch (message.Option) {
                case Option.AlwaysAutoHelmet:
                case Option.AdditionalFuelWarning:
                    Mod.Static.SetOption(message.Option, MyAPIGateway.Utilities.SerializeFromBinary<bool>(message.Value));
                    break;
                case Option.FuelThreshold:
                case Option.HaltedSpeedTolerance:
                    Mod.Static.SetOption(message.Option, MyAPIGateway.Utilities.SerializeFromBinary<float>(message.Value));
                    break;
                case Option.DelayAfterManualHelmet:
                    Mod.Static.SetOption(message.Option, MyAPIGateway.Utilities.SerializeFromBinary<int>(message.Value));
                    break;
                case Option.DisableAutoDampener:
                    Mod.Static.SetOption(message.Option, MyAPIGateway.Utilities.SerializeFromBinary<DisableAutoDampenerOption>(message.Value));
                    break;
                default:
                    using (Log.BeginMethod(nameof(OnSetOptionSyncMessage))) {
                        Log.Error($"Unknown option '{nameof(message.Option)}'");
                    }

                    return;
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

        /// <summary>
        ///     Water mod data received message handler.
        /// </summary>
        /// <param name="sender">The sender of the message.</param>
        /// <param name="message">The message.</param>
        private void OnWaterModDataReceived(ulong sender, WaterModDataSyncMessage message) {
            Mod.Static.WaterModAPI.Waters = message.Waters;
        }
    }
}