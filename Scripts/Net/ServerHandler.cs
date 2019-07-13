using System;
using Sandbox.ModAPI;
using Sisk.SmarterSuit.Data;
using Sisk.SmarterSuit.Net.Messages;
using Sisk.Utils.Logging;
using Sisk.Utils.Net;

namespace Sisk.SmarterSuit.Net {
    public class ServerHandler : NetworkHandlerBase {
        public ServerHandler(ILogger log, Network network) : base(log.ForScope<ClientHandler>(), network) {
            Network.Register<SettingsRequestMessage>(OnSettingsRequestMessage);
            Network.Register<SetOptionMessage>(OnSetOptionMessage);
        }

        public override void Close() {
            Network.Unregister<SettingsRequestMessage>(OnSettingsRequestMessage);
            Network.Unregister<SetOptionMessage>(OnSetOptionMessage);
            base.Close();
        }

        public override void SyncOption<TValue>(Option option, TValue value) {
            Mod.Static.SetOption(option, value);
            var message = new SetOptionSyncMessage { Option = option, Value = MyAPIGateway.Utilities.SerializeToBinary(value) };
            Network.Sync(message);
        }

        /// <summary>
        ///     Set option message handler.
        /// </summary>
        /// <param name="sender">The sender who send the option message.</param>
        /// <param name="message">The option message received.</param>
        private void OnSetOptionMessage(ulong sender, SetOptionMessage message) {
            if (message?.Option == null) {
                return;
            }

            if (!IsServerAdmin(message.SteamId)) {
                var response = new SetOptionResponseMessage { Result = Result.NoPermission, Option = message.Option, Value = message.Value };
                Network.Send(response, sender);
                return;
            }

            try {
                switch (message.Option) {
                    case Option.AlwaysAutoHelmet:
                    case Option.AdditionalFuelWarning:
                        SyncOption(message.Option, MyAPIGateway.Utilities.SerializeFromBinary<bool>(message.Value));
                        break;
                    case Option.FuelThreshold:
                    case Option.HaltedSpeedTolerance:
                        SyncOption(message.Option, MyAPIGateway.Utilities.SerializeFromBinary<float>(message.Value));
                        break;
                    case Option.DelayAfterManualHelmet:
                        SyncOption(Option.DelayAfterManualHelmet, MyAPIGateway.Utilities.SerializeFromBinary<int>(message.Value));
                        break;
                    case Option.DisableAutoDampener:
                        SyncOption(message.Option, MyAPIGateway.Utilities.SerializeFromBinary<DisableAutoDampenerOption>(message.Value));
                        break;
                    default:
                        using (Log.BeginMethod(nameof(OnSetOptionMessage))) {
                            Log.Error($"Unknown option '{nameof(message.Option)}'");
                        }

                        return;
                }

                var response = new SetOptionResponseMessage { Result = Result.Success, Option = message.Option, Value = message.Value };
                Network.Send(response, sender);
            } catch (Exception exception) {
                using (Log.BeginMethod(nameof(OnSetOptionMessage))) {
                    Log.Error(exception);

                    var response = new SetOptionResponseMessage { Result = Result.Error, Option = message.Option, Value = message.Value };
                    Network.Send(response, sender);
                }
            }
        }

        /// <summary>
        ///     Request Settings message handler.
        /// </summary>
        /// <param name="sender">The sender who requested settings.</param>
        /// <param name="message">The message from the requester.</param>
        private void OnSettingsRequestMessage(ulong sender, SettingsRequestMessage message) {
            if (Mod.Static.Settings == null) {
                return;
            }

            try {
                var response = new SettingsResponseMessage {
                    Settings = Mod.Static.Settings,
                    SteamId = sender
                };

                Network.Send(response, sender);
            } catch (Exception exception) {
                using (Log.BeginMethod(nameof(OnSettingsRequestMessage))) {
                    Log.Error(exception);
                }
            }
        }
    }
}
