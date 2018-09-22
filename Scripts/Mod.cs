using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sandbox.Game;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.Game.Localization;
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
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

// ReSharper disable InlineOutVariableDeclaration

namespace Sisk.SmarterSuit {
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class Mod : MySessionComponentBase {
        public const string NAME = "Smarter Suit";

        // important: change to info | warning | error or none before publishing this mod.
        private const LogEventLevel DEFAULT_LOG_EVENT_LEVEL = LogEventLevel.All;

        private const string LOG_FILE_TEMPLATE = "{0}.log";
        private const ushort NETWORK_ID = 51501;
        private const ulong REMOVE_AUTOMATIC_JETPACK_ACTIVATION_ID = 782845808;
        private const string SETTINGS_FILE = "settings.xml";
        private const float SPEED_TOLERANCE = 0.01f;

        private const int TICKS_UNTIL_FUEL_CHECK = 30;
        private const int TICKS_UNTIL_OXYGEN_CHECK = 100;
        private static readonly string LogFile = string.Format(LOG_FILE_TEMPLATE, NAME);
        private readonly CommandHandler _commandHandler = new CommandHandler();

        private readonly Dictionary<Option, Type> _optionsDictionary = new Dictionary<Option, Type> {
            { Option.AlwaysAutoHelmet, typeof(bool) },
            { Option.AdditionalFuelWarning, typeof(bool) },
            { Option.FuelThreshold, typeof(float) }
        };

        private SuitData _dataFromLastCockpit;
        private int _fuelCheckTicks;
        private IMyIdentity _identity;
        private bool _isFuelUnderThresholdBefore;
        private int _ticks;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Mod" /> session component.
        /// </summary>
        public Mod() {
            Static = this;
        }

        /// <summary>
        ///     The static instance.
        /// </summary>
        public static Mod Static { get; private set; }

        /// <summary>
        ///     Mod name to acronym.
        /// </summary>
        public static string Acronym => string.Concat(NAME.Where(char.IsUpper));

        /// <summary>
        ///     Indicates if local player has permission to change settings.
        /// </summary>
        private bool HasPermission => !MyAPIGateway.Multiplayer.MultiplayerActive || MyAPIGateway.Session.LocalHumanPlayer.PromoteLevel == MyPromoteLevel.Admin;

        /// <summary>
        ///     Indicates if mod is a dev version.
        /// </summary>
        private bool IsDevVersion => ModContext.ModName.EndsWith("_DEV");

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
        private bool RemoveAutomaticJetpackActivation { get; set; }

        /// <summary>
        ///     The Mod Settings.
        /// </summary>
        public ModSettings Settings { get; private set; }

        /// <summary>
        ///     The state that indicates the actions executed after simulation.
        /// </summary>
        private State State { get; set; }

        /// <summary>
        ///     Checks if enough oxygen around the given character.
        /// </summary>
        /// <param name="character">The character used to check if a helmet is needed.</param>
        /// <returns>Return true if enough oxygen is available.</returns>
        private static bool CheckHelmetNeeded(IMyCharacter character) {
            float oxygen;
            if (!MyAPIGateway.Session.SessionSettings.EnableOxygenPressurization) {
                var position = character.GetPosition();
                oxygen = MyAPIGateway.Session.OxygenProviderSystem.GetOxygenInPoint(position);
            } else {
                oxygen = character.EnvironmentOxygenLevel;
            }

            var helmet = oxygen < 0.5;
            return helmet;
        }

        /// <summary>
        ///     Gets the medical room that is closest to the given entity.
        /// </summary>
        /// <param name="entity">The entity used to find the closest medical room.</param>
        /// <returns>Return the closest medical room or <see langword="null" />.</returns>
        private static IMyMedicalRoom GetMedialRoom(IMyEntity entity) {
            var sphere = entity.PositionComp.WorldVolume;
            var entities = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref sphere).OfType<IMyCubeGrid>().ToList();

            var medicalRooms = new List<IMyMedicalRoom>();
            var blocks = new List<IMyMedicalRoom>();
            foreach (var cubeGrid in entities) {
                blocks.Clear();
                MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(cubeGrid).GetBlocksOfType(blocks);
                medicalRooms.AddRange(blocks);
            }

            if (medicalRooms.Any()) {
                var medicalRoom = medicalRooms.OrderBy(x => Vector3.Distance(x.GetPosition(), entity.GetPosition())).FirstOrDefault();
                if (medicalRoom != null) {
                    return medicalRoom;
                }
            }

            return null;
        }

        /// <summary>
        ///     Check if fuel for given character is under given threshold.
        /// </summary>
        /// <param name="threshold">The threshold.</param>
        /// <returns>Return true if fuel is under given threshold.</returns>
        private static bool IsFuelUnderThreshold(float threshold) {
            if (MyAPIGateway.Session.CreativeMode) {
                return false;
            }

            var character = MyAPIGateway.Session.LocalHumanPlayer.Character;
            if (character == null) {
                return false;
            }

            var jetpackComponent = character.Components.Get<MyCharacterJetpackComponent>();
            var oxygenComponent = character.Components.Get<MyCharacterOxygenComponent>();
            if (jetpackComponent == null || oxygenComponent == null) {
                return false;
            }

            return oxygenComponent.GetGasFillLevel(MyCharacterOxygenComponent.HydrogenId) < threshold;
        }

        /// <summary>
        ///     Checks if the ground is close by.
        /// </summary>
        /// <param name="character">The character used to check distance.</param>
        /// <param name="gravity">The gravity direction used to determine in which direction we check.</param>
        /// <returns>Return true if there is ground in 5m distance.</returns>
        private static bool IsGroundInRange(IMyCharacter character, Vector3 gravity) {
            if (gravity.Length() > 0) {
                var position = character.GetPosition();
                var from = position;
                gravity.Normalize();
                var to = position + gravity * 5;
                var results = new List<IHitInfo>();
                MyAPIGateway.Physics.CastRay(from, to, results);
                return results.Any();
            }

            return false;
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
        ///     Sets suit functions.
        /// </summary>
        /// <param name="character">The character which should enable/disable the systems.</param>
        /// <param name="data">A data structure to check which systems should be enabled/disabled</param>
        private static void SetSuitFunctions(IMyCharacter character, SuitData data) {
            if (character == null) {
                return;
            }

            var jetpackComponent = character.Components.Get<MyCharacterJetpackComponent>();
            if (jetpackComponent != null) {
                if (data.Dampeners != null && character.EnabledDamping != data.Dampeners.Value) {
                    jetpackComponent.SwitchDamping();
                }

                if (data.Thruster != null && character.EnabledThrusts != data.Thruster.Value) {
                    jetpackComponent.SwitchThrusts();

                    if (data.LinearVelocity.HasValue && data.AngularVelocity.HasValue) {
                        character.Physics.SetSpeeds(data.LinearVelocity.Value, data.AngularVelocity.Value);
                    } else if (data.LinearVelocity.HasValue) {
                        character.Physics.SetSpeeds(data.LinearVelocity.Value, Vector3.Zero);
                    } else if (data.AngularVelocity.HasValue) {
                        character.Physics.SetSpeeds(Vector3.Zero, data.AngularVelocity.Value);
                    }
                }
            }

            if (data.Helmet != null && character.EnabledHelmet != data.Helmet.Value) {
                character.SwitchHelmet();
            }
        }

        /// <summary>
        ///     Show a 'fuel low' warning.
        /// </summary>
        private static void ShowFuelLowWarningNotification() {
            var character = MyAPIGateway.Session.LocalHumanPlayer.Character;
            if (character == null) {
                return;
            }

            MyVisualScriptLogicProvider.RemoveSoundEmitter("HUD");
            MyVisualScriptLogicProvider.CreateSoundEmitterAtPosition("HUD", character.GetPosition());
            MyVisualScriptLogicProvider.PlaySound("HUD", "ArcHudVocFuelLow", true);

            MyAPIGateway.Utilities.ShowNotification(MySpaceTexts.NotificationFuelLow.GetString(), 2500, "Red");
        }

        /// <summary>
        ///     Load mod settings, create localizations and initialize network handler.
        /// </summary>
        public override void LoadData() {
            InitializeLogging();
            LoadTranslation();
            if (MyAPIGateway.Multiplayer.MultiplayerActive) {
                InitializeNetwork();

                if (Network != null) {
                    if (Network.IsServer) {
                        LoadSettings();
                        Network.Register<RequestSettingsMessage>(OnRequestSettingsMessage);
                        if (Network.IsDedicated) {
                            return;
                        }
                    }

                    if (Network.IsClient) {
                        Network.Register<SettingMessage>(OnSettingReceived);
                    }

                    Network.Register<OptionMessage>(OnOptionMessageReceived);
                }
            } else {
                LoadSettings();
            }

            CreateCommands();
            MyAPIGateway.Session.OnSessionReady += OnSessionReady;
            MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
            if (Settings != null) {
                SetUpdateOrder(MyUpdateOrder.AfterSimulation);
            }
        }

        /// <inheritdoc />
        public override void UpdateAfterSimulation() {
            if (State == State.None) {
                if (Settings.AlwaysAutoHelmet && MyAPIGateway.Session.SessionSettings.EnableOxygen) {
                    _ticks++;
                    if (_ticks >= TICKS_UNTIL_OXYGEN_CHECK - 1) {
                        State = State.CheckOxygenAfterDelay;
                    }
                }

                if (Settings.AdditionalFuelWarning && MyAPIGateway.Session.ControlledObject != null && MyAPIGateway.Session.ControlledObject is IMyCharacter) {
                    _fuelCheckTicks++;

                    if (_fuelCheckTicks >= TICKS_UNTIL_FUEL_CHECK) {
                        _fuelCheckTicks = 0;

                        var isFuelUnderThreshold = IsFuelUnderThreshold(Settings.FuelThreshold);
                        if (isFuelUnderThreshold && !_isFuelUnderThresholdBefore) {
                            ShowFuelLowWarningNotification();
                        }

                        _isFuelUnderThresholdBefore = isFuelUnderThreshold;
                    }
                }

                return;
            }

            var character = MyAPIGateway.Session.Player.Character;
            bool? helmet = null;
            bool? thruster = null;
            bool? dampeners = null;
            Vector3? linearVelocity = null;
            Vector3? angularVelocity = null;

            switch (State) {
                case State.CheckOxygenAfterDelay: {
                    _ticks++;
                    if (_ticks < TICKS_UNTIL_OXYGEN_CHECK) {
                        return;
                    }

                    _ticks = 0;

                    character = MyAPIGateway.Session.Player.Character;
                    helmet = character.EnvironmentOxygenLevel < 0.5;

                    SetSuitFunctions(character, new SuitData(null, null, helmet, null, null));
                    State = State.None;
                    return;
                }
                case State.ExitCockpit: {
                    dampeners = _dataFromLastCockpit.Dampeners;
                    thruster = _dataFromLastCockpit.Thruster;
                    linearVelocity = _dataFromLastCockpit.LinearVelocity;
                    angularVelocity = _dataFromLastCockpit.AngularVelocity;
                    break;
                }
                case State.Respawn: {
                    var entity = MyAPIGateway.Session.ControlledObject;
                    var atMedicalRoom = character == entity;
                    if (!atMedicalRoom) {
                        return;
                    }

                    var medicalRoom = GetMedialRoom(character);
                    if (medicalRoom == null) {
                        return;
                    }

                    var cubeGrid = medicalRoom.CubeGrid;
                    linearVelocity = Vector3.Zero;
                    angularVelocity = Vector3.Zero;
                    var gravity = character.Physics.Gravity;

                    var physics = cubeGrid.Physics;
                    if (physics != null) {
                        linearVelocity = physics.LinearVelocity;
                        angularVelocity = physics.AngularVelocity;
                    }

                    var isGravityDetected = gravity.Length() > 0;
                    var isGroundInRange = IsGroundInRange(character, gravity);
                    var isNotMoving = Math.Abs(linearVelocity.Value.Length()) < SPEED_TOLERANCE && Math.Abs(angularVelocity.Value.Length()) < SPEED_TOLERANCE;

                    if (isGravityDetected) {
                        if (isGroundInRange) {
                            thruster = RemoveAutomaticJetpackActivation ? (bool?) null : false;
                            dampeners = isNotMoving;
                        } else {
                            thruster = RemoveAutomaticJetpackActivation ? (bool?) null : true;
                            dampeners = isNotMoving;
                        }
                    } else {
                        thruster = RemoveAutomaticJetpackActivation ? (bool?) null : true;
                        dampeners = isNotMoving;
                    }

                    break;
                }
            }

            if (!MyAPIGateway.Session.SessionSettings.EnableOxygenPressurization) {
                helmet = CheckHelmetNeeded(character);
                State = State.None;
            } else {
                helmet = CheckHelmetNeeded(character) ? true : (bool?) null;
                State = State.CheckOxygenAfterDelay;
            }

            var data = new SuitData(dampeners, thruster, helmet, linearVelocity, angularVelocity);
            SetSuitFunctions(character, data);
        }

        /// <inheritdoc />
        protected override void UnloadData() {
            Log?.EnterMethod(nameof(UnloadData));

            MyAPIGateway.Session.OnSessionReady -= OnSessionReady;
            MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;

            var player = MyAPIGateway.Session.Player;
            if (player != null) {
                player.IdentityChanged -= OnIdentityChanged;
            }

            if (_identity != null) {
                _identity.CharacterChanged -= OnCharacterChanged;
                _identity = null;
            }

            var character = player?.Character;
            if (character != null) {
                UnRegisterEvents(character);
            }

            if (Network != null) {
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

        /// <summary>
        ///     Create commands.
        /// </summary>
        private void CreateCommands() {
            _commandHandler.Prefix = $"/{Acronym}";
            _commandHandler.Register(new Command { Name = "Enable", Description = ModText.Description_SS_Enable.GetString(), Execute = OnEnableOptionCommand });
            _commandHandler.Register(new Command { Name = "Disable", Description = ModText.Description_SS_Disable.GetString(), Execute = OnDisableOptionCommand });
            _commandHandler.Register(new Command { Name = "Set", Description = ModText.Description_SS_Set.GetString(), Execute = OnSetOptionCommand });
            _commandHandler.Register(new Command { Name = "List", Description = ModText.Description_SS_List.GetString(), Execute = OnListOptionsCommand });
            _commandHandler.Register(new Command { Name = "Help", Description = ModText.Description_SS_Help.GetString(), Execute = _commandHandler.ShowHelp });
        }

        /// <summary>
        ///     Initialize the logging system.
        /// </summary>
        private void InitializeLogging() {
            Log = Logger.ForScope<Mod>();
            if (MyAPIGateway.Multiplayer.MultiplayerActive) {
                if (MyAPIGateway.Multiplayer.IsServer) {
                    Log.Register(new WorldStorageHandler(LogFile, LogFormatter, DEFAULT_LOG_EVENT_LEVEL, IsDevVersion ? 0 : 500));
                } else if (IsDevVersion) {
                    Log.Register(new WorldStorageHandler(LogFile, LogFormatter, DEFAULT_LOG_EVENT_LEVEL, IsDevVersion ? 0 : 500));
                }
            } else {
                Log.Register(new WorldStorageHandler(LogFile, LogFormatter, DEFAULT_LOG_EVENT_LEVEL, IsDevVersion ? 0 : 500));
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
        ///     Load translations for this mod.
        /// </summary>
        private void LoadTranslation() {
            using (Log.BeginMethod(nameof(LoadTranslation))) {
                var currentLanguage = MyAPIGateway.Session.Config.Language;
                var supportedLanguages = new HashSet<MyLanguagesEnum>();

                MyTexts.LoadSupportedLanguages($"{ModContext.ModPathData}\\Localization", supportedLanguages);
                if (supportedLanguages.Contains(currentLanguage)) {
                    MyTexts.LoadTexts($"{ModContext.ModPathData}\\Localization", MyTexts.Languages[currentLanguage].CultureName);
                    Log.Info($"Loaded {MyTexts.Languages[currentLanguage].FullCultureName} translations.");
                } else if (supportedLanguages.Contains(MyLanguagesEnum.English)) {
                    MyTexts.LoadTexts($"{ModContext.ModPathData}\\Localization", MyTexts.Languages[MyLanguagesEnum.English].CultureName);
                    Log.Warning($"No {MyTexts.Languages[currentLanguage].FullCultureName} translations found. Fall back to {MyTexts.Languages[MyLanguagesEnum.English].FullCultureName} translations.");
                }
            }
        }

        /// <summary>
        ///     Called on <see cref="IMyIdentity.CharacterChanged" /> event. Used to check if we respawned.
        /// </summary>
        /// <param name="oldCharacter">The old character instance.</param>
        /// <param name="newCharacter">The new character instance.</param>
        private void OnCharacterChanged(IMyCharacter oldCharacter, IMyCharacter newCharacter) {
            var respawn = oldCharacter != newCharacter;

            UnRegisterEvents(oldCharacter);
            RegisterEvents(newCharacter);

            if (respawn) {
                State = State.Respawn;
            }
        }

        /// <summary>
        ///     Called on Disable option command.
        /// </summary>
        /// <param name="arguments">The arguments that should contain the option name.</param>
        private void OnDisableOptionCommand(string arguments) {
            if (!HasPermission) {
                MyAPIGateway.Utilities.ShowMessage(NAME, ModText.SS_NoPermissionError.GetString());
                return;
            }

            Option result;
            if (Enum.TryParse(arguments, true, out result)) {
                if (_optionsDictionary[result] == typeof(bool)) {
                    SetOption(result, false, true);
                } else {
                    MyAPIGateway.Utilities.ShowMessage(NAME, ModText.SS_OnlyBooleanAllowedError.GetString());
                }
            } else {
                MyAPIGateway.Utilities.ShowMessage(NAME, ModText.SS_UnknownOptionError.GetString(arguments));
            }
        }

        /// <summary>
        ///     Called on Enable command received.
        /// </summary>
        /// <param name="arguments">The arguments that should contain the option name.</param>
        private void OnEnableOptionCommand(string arguments) {
            if (!HasPermission) {
                MyAPIGateway.Utilities.ShowMessage(NAME, ModText.SS_NoPermissionError.GetString());
                return;
            }

            Option result;
            if (Enum.TryParse(arguments, true, out result)) {
                if (_optionsDictionary[result] == typeof(bool)) {
                    SetOption(result, true, true);
                } else {
                    MyAPIGateway.Utilities.ShowMessage(NAME, ModText.SS_OnlyBooleanAllowedError.GetString());
                }
            } else {
                MyAPIGateway.Utilities.ShowMessage(NAME, ModText.SS_UnknownOptionError.GetString(arguments));
            }
        }

        /// <summary>
        ///     Called on <see cref="IMyPlayer.IdentityChanged" /> event. Used keep track of
        ///     <see cref="IMyIdentity.CharacterChanged" /> event after identity change.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="identity"></param>
        private void OnIdentityChanged(IMyPlayer player, IMyIdentity identity) {
            _identity.CharacterChanged -= OnCharacterChanged;

            _identity = identity;
            _identity.CharacterChanged += OnCharacterChanged;
        }

        /// <summary>
        ///     Called on List command received.
        /// </summary>
        /// <param name="arguments">Arguments are ignored in this handler.</param>
        private void OnListOptionsCommand(string arguments) {
            MyAPIGateway.Utilities.ShowMessage(NAME, string.Join(", ", _optionsDictionary.Select(x => $"{x.Key} <{x.Value.Name}>")));
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
        ///     Called on <see cref="IMyCharacter.MovementStateChanged" /> event. Used to check if we leave a cockpit.
        /// </summary>
        /// <param name="character">The character who triggered this event.</param>
        /// <param name="oldState">The old movement state.</param>
        /// <param name="newState">The new movement state.</param>
        private void OnMovementStateChanged(IMyCharacter character, MyCharacterMovementEnum oldState, MyCharacterMovementEnum newState) {
            if (oldState == MyCharacterMovementEnum.Sitting) {
                var cockpit = MyAPIGateway.Session.ControlledObject as IMyCockpit;
                if (cockpit == null) {
                    return;
                }

                var velocities = cockpit.GetShipVelocities();
                var linearVelocity = velocities.LinearVelocity;
                var angularVelocity = velocities.AngularVelocity;

                bool? thruster;
                bool dampeners;

                var naturalGravity = cockpit.GetNaturalGravity();
                var gravity = cockpit.GetTotalGravity();

                var isGravityDetected = gravity.Length() > 0;
                var isArtificial = !(naturalGravity.Length() > 0);
                var isGroundInRange = IsGroundInRange(character, gravity);
                var isNotMoving = Math.Abs(linearVelocity.Length()) < SPEED_TOLERANCE && Math.Abs(angularVelocity.Length()) < SPEED_TOLERANCE;

                if (isGravityDetected) {
                    if (isGroundInRange) {
                        thruster = RemoveAutomaticJetpackActivation ? (bool?) null : false;
                        dampeners = isNotMoving;
                    } else {
                        thruster = RemoveAutomaticJetpackActivation ? (bool?) null : true;
                        dampeners = isNotMoving || !isArtificial;
                    }
                } else {
                    thruster = RemoveAutomaticJetpackActivation ? (bool?) null : true;
                    dampeners = isNotMoving;
                }

                _dataFromLastCockpit = new SuitData(dampeners, thruster, null, linearVelocity, angularVelocity);

                State = State.ExitCockpit;
            }
        }

        /// <summary>
        ///     Option message handler.
        /// </summary>
        /// <param name="sender">The sender who send the option message.</param>
        /// <param name="message">The option message received.</param>
        private void OnOptionMessageReceived(ulong sender, OptionMessage message) {
            if (message?.Option == null) {
                return;
            }

            try {
                switch (message.Option) {
                    case Option.AlwaysAutoHelmet:
                    case Option.AdditionalFuelWarning:
                        SetOption(message.Option, MyAPIGateway.Utilities.SerializeFromBinary<bool>(message.Value), Network.IsServer);
                        break;
                    case Option.FuelThreshold:
                        SetOption(message.Option, MyAPIGateway.Utilities.SerializeFromBinary<float>(message.Value), Network.IsServer);
                        break;
                    default:
                        return;
                }
            } catch (Exception exception) {
                using (Log.BeginMethod(nameof(OnOptionMessageReceived))) {
                    Log.Error(exception);
                }
            }
        }

        /// <summary>
        ///     Request Settings message handler.
        /// </summary>
        /// <param name="sender">The sender who requested settings.</param>
        /// <param name="message">The message from the requester.</param>
        private void OnRequestSettingsMessage(ulong sender, RequestSettingsMessage message) {
            if (Settings == null) {
                return;
            }

            try {
                var response = new SettingMessage {
                    Settings = Settings,
                    SteamId = message.SteamId
                };

                Network.Send(response, sender);
            } catch (Exception exception) {
                using (Log.BeginMethod(nameof(OnRequestSettingsMessage))) {
                    Log.Error(exception);
                }
            }
        }

        /// <summary>
        ///     Executed if Session is ready.
        /// </summary>
        private void OnSessionReady() {
            MyAPIGateway.Session.OnSessionReady -= OnSessionReady;
            RemoveAutomaticJetpackActivation = MyAPIGateway.Session.Mods.Any(x => x.PublishedFileId == REMOVE_AUTOMATIC_JETPACK_ACTIVATION_ID);

            var player = MyAPIGateway.Session.Player;
            player.IdentityChanged += OnIdentityChanged;

            _identity = player.Identity;
            if (_identity == null) {
                return;
            }

            _identity.CharacterChanged += OnCharacterChanged;

            var character = player.Character;
            if (character != null) {
                RegisterEvents(character);
            }
        }

        /// <summary>
        ///     Set an option to given value
        /// </summary>
        /// <param name="arguments">The arguments that should contain the value.</param>
        private void OnSetOptionCommand(string arguments) {
            if (!HasPermission) {
                MyAPIGateway.Utilities.ShowMessage(NAME, ModText.SS_NoPermissionError.GetString());
                return;
            }

            var array = arguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (array.Length < 2) {
                MyAPIGateway.Utilities.ShowMessage(NAME, ModText.SS_ArgumentError.GetString(arguments));
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
                        SetOption(result, value, true);
                    } else {
                        MyAPIGateway.Utilities.ShowMessage(NAME, ModText.SS_ConvertError.GetString(valueString, type.Name));
                    }
                } else if (type == typeof(float)) {
                    float value;
                    if (float.TryParse(valueString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)) {
                        SetOption(result, value, true);
                    } else {
                        MyAPIGateway.Utilities.ShowMessage(NAME, ModText.SS_ConvertError.GetString(valueString, type.Name));
                    }
                }
            } else {
                MyAPIGateway.Utilities.ShowMessage(NAME, ModText.SS_UnknownOptionError.GetString(optionString));
            }
        }

        /// <summary>
        ///     Settings received message handler.
        /// </summary>
        /// <param name="sender">The sender of the message.</param>
        /// <param name="message">The message.</param>
        private void OnSettingReceived(ulong sender, SettingMessage message) {
            if (message.Settings != null) {
                Settings = message.Settings;
                SetUpdateOrder(MyUpdateOrder.AfterSimulation);
            }
        }

        /// <summary>
        ///     Register character events.
        /// </summary>
        /// <param name="character">The character.</param>
        private void RegisterEvents(IMyCharacter character) {
            if (character != null) {
                character.MovementStateChanged += OnMovementStateChanged;
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

        /// <summary>
        ///     Set a given option to given value.
        /// </summary>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="option">Which option should be set.</param>
        /// <param name="value">The value for given option.</param>
        /// <param name="sync">Indicates if option should be synced.</param>
        private void SetOption<TValue>(Option option, TValue value, bool sync) {
            if (Network == null || Network.IsServer) {
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
                    default:
                        return;
                }

                SaveSettings();
            }

            if (Network != null) {
                if (!sync) {
                    return;
                }

                var message = new OptionMessage { Option = option, Value = MyAPIGateway.Utilities.SerializeToBinary(value) };

                if (Network.IsServer) {
                    Network.Sync(message);
                } else if (Network.IsClient) {
                    Network.SendToServer(message);
                }
            }
        }

        /// <summary>
        ///     UnRegister character events.
        /// </summary>
        /// <param name="character">The character.</param>
        private void UnRegisterEvents(IMyCharacter character) {
            if (character != null) {
                character.MovementStateChanged -= OnMovementStateChanged;
            }
        }
    }
}