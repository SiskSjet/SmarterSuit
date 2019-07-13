using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sandbox.ModAPI;
using Sisk.SmarterSuit.Data;
using Sisk.SmarterSuit.Extensions;
using Sisk.SmarterSuit.Localization;
using Sisk.SmarterSuit.Net;
using Sisk.SmarterSuit.Net.Messages;
using Sisk.SmarterSuit.Settings;
using Sisk.Utils.Logging;
using Sisk.Utils.Logging.DefaultHandler;
using Sisk.Utils.Net;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Utils;

namespace Sisk.SmarterSuit {
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class Mod : MySessionComponentBase {
        public const string NAME = "Smarter Suit";
        private const LogEventLevel DEFAULT_LOG_EVENT_LEVEL = LogEventLevel.Info | LogEventLevel.Warning | LogEventLevel.Error;
        private const string LOG_FILE_TEMPLATE = "{0}.log";
        private const ushort NETWORK_ID = 51501;
        private const ulong REMOVE_AUTOMATIC_JETPACK_ACTIVATION_ID = 782845808;
        private const string SETTINGS_FILE = "settings.xml";

        private static readonly string LogFile = string.Format(LOG_FILE_TEMPLATE, NAME);
        private static readonly MyStringHash LowPressure = MyStringHash.Get("LowPressure");
        private static readonly MyDefinitionId OxygenId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Oxygen");
        private ChatHandler _chatHandler;
        private NetworkHandlerBase _networkHandler;
        private SuitComputer _suitComputer;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Mod" /> session component.
        /// </summary>
        public Mod() {
            Static = this;
        }

        /// <summary>
        ///     Mod name to acronym.
        /// </summary>
        public static string Acronym => string.Concat(NAME.Where(char.IsUpper));

        /// <summary>
        ///     Indicates if mod is a dev version.
        /// </summary>
        private bool IsDevVersion => ModContext.ModName.EndsWith("_DEV");

        /// <summary>
        ///     Language used to localize this mod.
        /// </summary>
        public MyLanguagesEnum? Language { get; private set; }

        /// <summary>
        ///     Logger used for logging.
        /// </summary>
        public ILogger Log { get; private set; }

        /// <summary>
        ///     Network to handle syncing.
        /// </summary>
        public Network Network { get; private set; }

        /// <summary>
        ///     Indicates if the 'Remove all automatic jetpack activation' is available.
        /// </summary>
        public bool RemoveAutomaticJetpackActivation { get; set; }

        /// <summary>
        ///     The Mod Settings.
        /// </summary>
        public ModSettings Settings { get; private set; }

        /// <summary>
        ///     The static instance.
        /// </summary>
        public static Mod Static { get; private set; }

        /// <summary>
        ///     Shows a result message in chat window.
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="option"></param>
        /// <param name="value"></param>
        /// <param name="result"></param>
        public static void ShowResultMessage<TValue>(Option option, TValue value, Result result) {
            if (MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Utilities.IsDedicated) {
                return;
            }

            switch (result) {
                case Result.NoPermission:
                    MyAPIGateway.Utilities.ShowMessage(NAME, ModText.Error_SS_NoPermission.GetString());
                    break;
                case Result.Error:
                    MyAPIGateway.Utilities.ShowMessage(NAME, ModText.Error_SS_SetOption.GetString(option, value));
                    break;
                case Result.Success:
                    MyAPIGateway.Utilities.ShowMessage(NAME, ModText.Message_SS_SetOptionSuccess.GetString(option, value));
                    break;
            }
        }

        /// <summary>
        ///     Used to format the <see cref="LogEvent" /> entries.
        /// </summary>
        /// <param name="level">The <see cref="LogEventLevel" /> for current event.</param>
        /// <param name="message">The <see cref="LogEvent" /> message.</param>
        /// <param name="timestamp">The timestamp of the <see cref="LogEvent" />.</param>
        /// <param name="scope">The scope of the <see cref="LogEvent" />.</param>
        /// <param name="method">The called method of this <see cref="LogEvent" />.</param>
        /// <returns></returns>
        private static string LogFormatter(LogEventLevel level, string message, DateTime timestamp, Type scope, string method) {
            return $"[{timestamp:HH:mm:ss:fff}] [{new string(level.ToString().Take(1).ToArray())}] [{scope}->{method}()]: {message}";
        }

        /// <summary>
        ///     Before simulation start.
        ///     Used to register a before damage handler.
        /// </summary>
        public override void BeforeStart() {
            if (Network == null || Network.IsServer) {
                MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(25, OnBeforeDamage);
            }
        }

        /// <inheritdoc />
        public override void HandleInput() {
            if (Settings == null || _suitComputer == null || !Settings.AlwaysAutoHelmet || MyAPIGateway.Gui.ChatEntryVisible || MyAPIGateway.Gui.IsCursorVisible) {
                return;
            }

            var input = MyAPIGateway.Input;
            if (input.IsNewGameControlReleased(MyStringId.Get("HELMET"))) {
                _suitComputer.DelayAutoHelmet();
            }
        }

        /// <summary>
        ///     Load mod settings, create localizations and initialize network handler.
        /// </summary>
        public override void LoadData() {
            InitializeLogging();
            LoadLocalization();
            MyAPIGateway.Gui.GuiControlRemoved += OnGuiControlRemoved;

            if (MyAPIGateway.Multiplayer.MultiplayerActive) {
                InitializeNetwork();

                if (Network != null) {
                    if (Network.IsServer) {
                        LoadSettings();
                        _networkHandler = new ServerHandler(Log, Network);

                        if (Network.IsDedicated) {
                            return;
                        }
                    } else {
                        _networkHandler = new ClientHandler(Log, Network);
                        Network.SendToServer(new SettingsRequestMessage());
                    }
                }
            } else {
                LoadSettings();
            }

            _chatHandler = new ChatHandler(Log, Network, _networkHandler);
            MyAPIGateway.Session.OnSessionReady += OnSessionReady;
        }

        public override void UpdateBeforeSimulation() {
            if (_suitComputer == null) {
                return;
            }

            _suitComputer.Update();
        }

        /// <inheritdoc />
        protected override void UnloadData() {
            Log?.EnterMethod(nameof(UnloadData));

            MyAPIGateway.Session.OnSessionReady -= OnSessionReady;
            MyAPIGateway.Gui.GuiControlRemoved -= OnGuiControlRemoved;

            if (_chatHandler != null) {
                _chatHandler.Close();
                _chatHandler = null;
            }

            if (_suitComputer != null) {
                _suitComputer.Close();
                _suitComputer = null;
            }

            if (Network != null) {
                _networkHandler.Close();
                _networkHandler = null;

                Log?.Info("Cap network connections");
                Network.Close();
                Network = null;
            }

            if (Log != null) {
                Log.Info("Logging stopped");
                Log.Flush();
                Log.Close();
                Log = null;
            }
        }

        public void OnSettingsReceived(ModSettings settings) {
            if (settings != null) {
                Settings = settings;
            }
        }

        /// <summary>
        ///     Set a given option to given value.
        /// </summary>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="option">Which option should be set.</param>
        /// <param name="value">The value for given option.</param>
        public void SetOption<TValue>(Option option, TValue value) {
            switch (option) {
                case Option.AlwaysAutoHelmet:
                    Settings.AlwaysAutoHelmet = (bool) (object) value;
                    break;
                case Option.AdditionalFuelWarning:
                    Settings.AdditionalFuelWarning = (bool) (object) value;
                    break;
                case Option.FuelThreshold:
                    Settings.FuelThreshold = (float) (object) value;
                    break;
                case Option.DisableAutoDampener:
                    Settings.DisableAutoDampener = (DisableAutoDampenerOption) (object) value;
                    break;
                case Option.HaltedSpeedTolerance:
                    Settings.HaltedSpeedTolerance = (float) (object) value;
                    break;
                case Option.DelayAfterManualHelmet:
                    Settings.DelayAfterManualHelmet = (int) (object) value;
                    break;
                default:
                    using (Log.BeginMethod(nameof(SetOption))) {
                        Log.Error($"Unknown option '{nameof(option)}'");
                    }

                    return;
            }

            if (Network == null || Network.IsServer) {
                ShowResultMessage(option, value, Result.Success);
                SaveSettings();
            }
        }

        /// <summary>
        ///     Initialize the logging system.
        /// </summary>
        private void InitializeLogging() {
            Log = Logger.ForScope<Mod>();
            if (MyAPIGateway.Multiplayer.MultiplayerActive) {
                if (MyAPIGateway.Multiplayer.IsServer || IsDevVersion) {
                    Log.Register(new WorldStorageHandler(LogFile, LogFormatter, IsDevVersion ? LogEventLevel.All : DEFAULT_LOG_EVENT_LEVEL, IsDevVersion ? 0 : 500));
                }
            } else {
                Log.Register(new WorldStorageHandler(LogFile, LogFormatter, IsDevVersion ? LogEventLevel.All : DEFAULT_LOG_EVENT_LEVEL, IsDevVersion ? 0 : 500));
            }

            using (Log.BeginMethod(nameof(InitializeLogging))) {
                Log.Info("Logging initialized");
            }
        }

        /// <summary>
        ///     Initialize the network system.
        /// </summary>
        private void InitializeNetwork() {
            using (Log.BeginMethod(nameof(InitializeNetwork))) {
                Log.Info("Initialize Network");
                Network = new Network(NETWORK_ID);
                Log.Info($"IsClient {Network.IsClient}, IsServer: {Network.IsServer}, IsDedicated: {Network.IsDedicated}");
                Log.Info("Network initialized");
            }
        }

        /// <summary>
        ///     Load localizations for this mod.
        /// </summary>
        private void LoadLocalization() {
            var path = Path.Combine(ModContext.ModPathData, "Localization");
            var supportedLanguages = new HashSet<MyLanguagesEnum>();
            MyTexts.LoadSupportedLanguages(path, supportedLanguages);

            var currentLanguage = supportedLanguages.Contains(MyAPIGateway.Session.Config.Language) ? MyAPIGateway.Session.Config.Language : MyLanguagesEnum.English;
            if (Language != null && Language == currentLanguage) {
                return;
            }

            Language = currentLanguage;
            var languageDescription = MyTexts.Languages.Where(x => x.Key == currentLanguage).Select(x => x.Value).FirstOrDefault();
            if (languageDescription != null) {
                var cultureName = string.IsNullOrWhiteSpace(languageDescription.CultureName) ? null : languageDescription.CultureName;
                var subcultureName = string.IsNullOrWhiteSpace(languageDescription.SubcultureName) ? null : languageDescription.SubcultureName;

                MyTexts.LoadTexts(path, cultureName, subcultureName);
            }
        }

        /// <summary>
        ///     Load mod settings.
        /// </summary>
        private void LoadSettings() {
            ModSettings settings = null;
            try {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(SETTINGS_FILE, typeof(Mod))) {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(SETTINGS_FILE, typeof(Mod))) {
                        settings = MyAPIGateway.Utilities.SerializeFromXML<ModSettings>(reader.ReadToEnd());
                    }
                }
            } catch (Exception exception) {
                using (Log.BeginMethod(nameof(LoadSettings))) {
                    Log.Error(exception);
                }
            }

            if (settings != null) {
                if (settings.Version < ModSettings.VERSION) {
                    // todo: merge old and new settings in future versions.
                }
            } else {
                settings = new ModSettings();
            }

            Settings = settings;
        }

        /// <summary>
        ///     The before damage handler to avoid 'LowPressure' damage when AutoHelmet is on.
        /// </summary>
        /// <param name="target">The target which received the damage.</param>
        /// <param name="info">The damage info.</param>
        private void OnBeforeDamage(object target, ref MyDamageInformation info) {
            if (info.Type == LowPressure && Static.Settings.AlwaysAutoHelmet) {
                // todo: when settings can be per player I have to check if AutoHelmet is enabled for this player.
                var character = target as IMyCharacter;
                if (character != null) {
                    if (!character.EnabledHelmet) {
                        character.SwitchHelmet();
                    }

                    if (character.GetSuitGasFillLevel(OxygenId) > 0) {
                        info.Amount = 0;
                    }
                }
            }
        }

        /// <summary>
        ///     Event triggered on gui control removed.
        ///     Used to detect if Option screen is closed and then to reload localization.
        /// </summary>
        /// <param name="obj"></param>
        private void OnGuiControlRemoved(object obj) {
            if (obj.ToString().EndsWith("ScreenOptionsSpace")) {
                LoadLocalization();
            }
        }

        /// <summary>
        ///     Executed if Session is ready.
        /// </summary>
        private void OnSessionReady() {
            MyAPIGateway.Session.OnSessionReady -= OnSessionReady;
            RemoveAutomaticJetpackActivation = MyAPIGateway.Session.Mods.Any(x => x.PublishedFileId == REMOVE_AUTOMATIC_JETPACK_ACTIVATION_ID);

            _suitComputer = SuitComputer.Create();
            if (_suitComputer != null) {
                SetUpdateOrder(MyUpdateOrder.BeforeSimulation);
            }
        }

        /// <summary>
        ///     Save settings.
        /// </summary>
        private void SaveSettings() {
            try {
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(SETTINGS_FILE, typeof(Mod))) {
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(Settings));
                }
            } catch (Exception exception) {
                using (Log.BeginMethod(nameof(SaveSettings))) {
                    Log.Error(exception);
                }
            }
        }
    }
}