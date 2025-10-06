using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Sandbox.ModAPI;
using Sisk.SmarterSuit.Data;
using Sisk.SmarterSuit.Extensions;
using Sisk.SmarterSuit.Localization;
using Sisk.Utils.CommandHandler;
using Sisk.Utils.Logging;

// ReSharper disable InlineOutVariableDeclaration

namespace Sisk.SmarterSuit {

    public class ChatHandler {

        private readonly Dictionary<string, Option> _alias = new Dictionary<string, Option>(StringComparer.CurrentCultureIgnoreCase) {
            { Acronym(nameof(Option.AlwaysAutoHelmet)), Option.AlwaysAutoHelmet },
            { Acronym(nameof(Option.AdditionalFuelWarning)), Option.AdditionalFuelWarning },
            { Acronym(nameof(Option.FuelThreshold)), Option.FuelThreshold },
            { Acronym(nameof(Option.DisableAutoDampener)), Option.DisableAutoDampener },
            { Acronym(nameof(Option.HaltedSpeedTolerance)), Option.HaltedSpeedTolerance },
            { Acronym(nameof(Option.DelayAfterManualHelmet)), Option.DelayAfterManualHelmet },
            { Acronym(nameof(Option.AlignToGravity)), Option.AlignToGravity },
            { Acronym(nameof(Option.AlignToGravityDelay)), Option.AlignToGravityDelay },
            { Acronym(nameof(Option.RememberBroadcast)), Option.AlignToGravityDelay },
            { Acronym(nameof(Option.SwitchHelmetLight)), Option.SwitchHelmetLight },
            { Acronym(nameof(Option.TurnLightsBackOn)), Option.TurnLightsBackOn },
        };

        private readonly CommandHandler _commandHandler;

        private readonly Dictionary<Option, Type> _options = new Dictionary<Option, Type> {
            { Option.AlwaysAutoHelmet, typeof(bool) },
            { Option.AdditionalFuelWarning, typeof(bool) },
            { Option.FuelThreshold, typeof(float) },
            { Option.DisableAutoDampener, typeof(byte) },
            { Option.HaltedSpeedTolerance, typeof(float) },
            { Option.DelayAfterManualHelmet, typeof(int) },
            { Option.AlignToGravity, typeof(bool) },
            { Option.AlignToGravityDelay, typeof(int) },
            { Option.RememberBroadcast, typeof(bool) },
            { Option.SwitchHelmetLight, typeof(bool) },
            { Option.TurnLightsBackOn, typeof(bool) },
        };

        public ChatHandler(ILogger log) {
            Log = log;

            _commandHandler = new CommandHandler(Mod.NAME) { Prefix = $"/{Mod.Acronym}" };
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
        ///     Close the network message handler.
        /// </summary>
        public virtual void Close() {
            MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;

            if (Log != null) {
                Log = null;
            }
        }

        private static string Acronym(string name) {
            return string.Concat(name.Where(char.IsUpper));
        }

        /// <summary>
        ///     Called on Disable option command.
        /// </summary>
        /// <param name="arguments">The arguments that should contain the option name.</param>
        private void OnDisableOptionCommand(string arguments) {
            Option result;
            if (TryGetOption(arguments, out result)) {
                if (_options[result] == typeof(bool)) {
                    SetOption(result, false);
                } else {
                    MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.Error_SS_OnlyBooleanAllowed.GetString());
                }
            } else {
                MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.Error_SS_UnknownOption.GetString(arguments));
            }
        }

        /// <summary>
        ///     Called on Enable command received.
        /// </summary>
        /// <param name="arguments">The arguments that should contain the option name.</param>
        private void OnEnableOptionCommand(string arguments) {
            Option result;
            if (TryGetOption(arguments, out result)) {
                if (_options[result] == typeof(bool)) {
                    SetOption(result, true);
                } else {
                    MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.Error_SS_OnlyBooleanAllowed.GetString());
                }
            } else {
                MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.Error_SS_UnknownOption.GetString(arguments));
            }
        }

        /// <summary>
        ///     Called on List command received.
        /// </summary>
        /// <param name="arguments">Arguments are ignored in this handler.</param>
        private void OnListOptionsCommand(string arguments) {
            var sb = new StringBuilder("Option | Alias | Type").AppendLine();
            foreach (var pair in _options) {
                var option = pair.Key;
                var type = pair.Value;
                sb.Append(option).Append(" | ");
                sb.AppendFormat("({0})", string.Join(",", _alias.Where(x => x.Value == option).Select(x => x.Key))).Append(" | ");
                sb.Append($"<{type.Name}>").AppendLine();
            }

            MyAPIGateway.Utilities.ShowMessage(Mod.NAME, sb.ToString());
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
                MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.Error_SS_Argument.GetString(arguments));
                return;
            }

            var optionString = array[0];
            var valueString = array[1];
            Option result;
            if (TryGetOption(optionString, out result)) {
                var type = _options[result];
                if (type == typeof(bool)) {
                    bool value;
                    if (bool.TryParse(valueString, out value)) {
                        SetOption(result, value);
                    } else {
                        MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.Error_SS_Convert.GetString(valueString, type.Name));
                    }
                } else if (type == typeof(float)) {
                    float value;
                    if (float.TryParse(valueString, NumberStyles.Number, CultureInfo.InvariantCulture, out value)) {
                        SetOption(result, value);
                    } else {
                        MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.Error_SS_Convert.GetString(valueString, type.Name));
                    }
                } else if (type == typeof(byte)) {
                    byte value;
                    if (byte.TryParse(valueString, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) {
                        SetOption(result, value);
                    } else {
                        MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.Error_SS_Convert.GetString(valueString, type.Name));
                    }
                } else if (type == typeof(int)) {
                    int value;
                    if (int.TryParse(valueString, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) {
                        SetOption(result, value);
                    } else {
                        MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.Error_SS_Convert.GetString(valueString, type.Name));
                    }
                }
            } else {
                MyAPIGateway.Utilities.ShowMessage(Mod.NAME, ModText.Error_SS_UnknownOption.GetString(optionString));
            }
        }

        /// <summary>
        ///     Set a given option to given value.
        /// </summary>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="option">Which option should be set.</param>
        /// <param name="value">The value for given option.</param>
        private void SetOption<TValue>(Option option, TValue value) {
            Mod.Static.SetOption(option, value);
        }

        /// <summary>
        ///     Try to resolve given string to an option.
        /// </summary>
        /// <param name="arguments">The string that gets checked.</param>
        /// <param name="result">The Option returned.</param>
        /// <returns>Returns true if an option is resolved.</returns>
        private bool TryGetOption(string arguments, out Option result) {
            if (_alias.ContainsKey(arguments)) {
                result = _alias[arguments];
                return true;
            }

            if (Enum.TryParse(arguments, true, out result)) {
                return true;
            }

            return false;
        }
    }
}