using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sandbox.ModAPI;
using Sisk.SmarterSuit.Data;
using Sisk.SmarterSuit.Extensions;
using Sisk.SmarterSuit.Localization;
using Sisk.SmarterSuit.Net;
using Sisk.Utils.Logging;
using Sisk.Utils.Net;

// ReSharper disable InlineOutVariableDeclaration

namespace Sisk.SmarterSuit {
    public class ChatHandler {
        private readonly CommandHandler _commandHandler;

        private readonly Dictionary<Option, Type> _optionsDictionary = new Dictionary<Option, Type> {
            { Option.AlwaysAutoHelmet, typeof(bool) },
            { Option.AdditionalFuelWarning, typeof(bool) },
            { Option.FuelThreshold, typeof(float) }
        };

        public ChatHandler(ILogger log, Network network, NetworkHandlerBase networkHandler) {
            Log = log;
            Network = network;
            NetworkHandler = networkHandler;

            _commandHandler = new CommandHandler { Prefix = $"/{Mod.Acronym}" };
            _commandHandler.Register(new Command { Name = "Enable", Description = ModText.Description_SS_Enable.GetString(), Execute = OnEnableOptionCommand });
            _commandHandler.Register(new Command { Name = "Disable", Description = ModText.Description_SS_Disable.GetString(), Execute = OnDisableOptionCommand });
            _commandHandler.Register(new Command { Name = "Set", Description = ModText.Description_SS_Set.GetString(), Execute = OnSetOptionCommand });
            _commandHandler.Register(new Command { Name = "List", Description = ModText.Description_SS_List.GetString(), Execute = OnListOptionsCommand });
            _commandHandler.Register(new Command { Name = "Help", Description = ModText.Description_SS_Help.GetString(), Execute = _commandHandler.ShowHelp });

            MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
        }

        /// <summary>
        ///     Logger used for logging.
        /// </summary>
        private ILogger Log { get; set; }

        /// <summary>
        ///     Network to handle syncing.
        /// </summary>
        private Network Network { get; set; }

        private NetworkHandlerBase NetworkHandler { get; }

        /// <summary>
        ///     Close the network message handler.
        /// </summary>
        public virtual void Close() {
            MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;
            if (Network != null) {
                Network = null;
            }

            if (Log != null) {
                Log = null;
            }
        }

        /// <summary>
        ///     Called on Disable option command.
        /// </summary>
        /// <param name="arguments">The arguments that should contain the option name.</param>
        private void OnDisableOptionCommand(string arguments) {
            Option result;
            if (Enum.TryParse(arguments, true, out result)) {
                if (_optionsDictionary[result] == typeof(bool)) {
                    SetOption(result, false);
                } else {
                    MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.SS_OnlyBooleanAllowedError.GetString());
                }
            } else {
                MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.SS_UnknownOptionError.GetString(arguments));
            }
        }

        /// <summary>
        ///     Called on Enable command received.
        /// </summary>
        /// <param name="arguments">The arguments that should contain the option name.</param>
        private void OnEnableOptionCommand(string arguments) {
            Option result;
            if (Enum.TryParse(arguments, true, out result)) {
                if (_optionsDictionary[result] == typeof(bool)) {
                    SetOption(result, true);
                } else {
                    MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.SS_OnlyBooleanAllowedError.GetString());
                }
            } else {
                MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.SS_UnknownOptionError.GetString(arguments));
            }
        }

        /// <summary>
        ///     Called on List command received.
        /// </summary>
        /// <param name="arguments">Arguments are ignored in this handler.</param>
        private void OnListOptionsCommand(string arguments) {
            MyAPIGateway.Utilities.ShowMessage(Mod.NAME, string.Join(", ", _optionsDictionary.Select(x => $"{x.Key} <{x.Value.Name}>")));
        }

        /// <summary>
        ///     Message event handler.
        /// </summary>
        /// <param name="messageText">The received message text.</param>
        /// <param name="sendToOthers">Indicates if message should be send to others.</param>
        private void OnMessageEntered(string messageText, ref bool sendToOthers) {
            if (_commandHandler.TryHandle(messageText.Trim())) {
                sendToOthers = false;
            }
        }

        /// <summary>
        ///     Set an option to given value
        /// </summary>
        /// <param name="arguments">The arguments that should contain the value.</param>
        private void OnSetOptionCommand(string arguments) {
            var array = arguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (array.Length < 2) {
                MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.SS_ArgumentError.GetString(arguments));
                return;
            }

            var optionString = array[0];
            var valueString = array[1];
            Option result;
            if (Enum.TryParse(optionString, true, out result)) {
                var type = _optionsDictionary[result];
                if (type == typeof(bool)) {
                    bool value;
                    if (bool.TryParse(valueString, out value)) {
                        SetOption(result, value);
                    } else {
                        MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.SS_ConvertError.GetString(valueString, type.Name));
                    }
                } else if (type == typeof(float)) {
                    float value;
                    if (float.TryParse(valueString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)) {
                        SetOption(result, value);
                    } else {
                        MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.SS_ConvertError.GetString(valueString, type.Name));
                    }
                }
            } else {
                MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.SS_UnknownOptionError.GetString(optionString));
            }
        }

        /// <summary>
        ///     Set a given option to given value.
        /// </summary>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="option">Which option should be set.</param>
        /// <param name="value">The value for given option.</param>
        private void SetOption<TValue>(Option option, TValue value) {
            if (Network == null) {
                Mod.Static.SetOption(option, value);
            } else {
                NetworkHandler.SyncOption(option, value);
            }
        }
    }
}