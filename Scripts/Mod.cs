using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Common.ObjectBuilders.Definitions;
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
using VRage.Utils;
using VRageMath;

// ReSharper disable UsePatternMatching

namespace Sisk.SmarterSuit {
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class Mod : MySessionComponentBase {
        public const string NAME = "Smarter Suit";
        private const LogEventLevel DEFAULT_LOG_EVENT_LEVEL = LogEventLevel.Info | LogEventLevel.Warning | LogEventLevel.Error;

        private const float GRAVITY = 9.81f;
        private const string HYDROGEN_BOTTLE_ID = "MyObjectBuilder_GasContainerObject/HydrogenBottle";
        private const string LOG_FILE_TEMPLATE = "{0}.log";
        private const ushort NETWORK_ID = 51501;
        private const ulong REMOVE_AUTOMATIC_JETPACK_ACTIVATION_ID = 782845808;
        private const string SETTINGS_FILE = "settings.xml";
        private const int TICKS_UNTIL_FUEL_CHECK = 30;
        private const int TICKS_UNTIL_OXYGEN_CHECK = 30;

        private static readonly string LogFile = string.Format(LOG_FILE_TEMPLATE, NAME);
        private ChatHandler _chatHandler;
        private SuitData _dataFromLastCockpit;
        private int _fuelCheckTicks;

        private bool _hasWaitedATick;
        private IMyIdentity _identity;
        private bool _isFuelUnderThresholdBefore;

        private bool _lastDampenerState;
        private NetworkHandlerBase _networkHandler;
        private int _ticks;

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
                    MyAPIGateway.Utilities.ShowMessage(NAME, ModText.SS_NoPermissionError.GetString());
                    break;
                case Result.Error:
                    MyAPIGateway.Utilities.ShowMessage(NAME, ModText.SS_SetOptionError.GetString(option, value));
                    break;
                case Result.Success:
                    MyAPIGateway.Utilities.ShowMessage(NAME, ModText.SS_SetOptionSuccess.GetString(option, value));
                    break;
            }
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

            float bottleFillLevel = 0;
            var items = character.GetInventory().GetItems();
            foreach (var item in items) {
                if (item.Content.ToString() == HYDROGEN_BOTTLE_ID) {
                    var bottle = item.Content as MyObjectBuilder_GasContainerObject;
                    if (bottle != null) {
                        bottleFillLevel += bottle.GasLevel;
                    }
                }
            }

            return oxygenComponent.GetGasFillLevel(MyCharacterOxygenComponent.HydrogenId) < threshold && bottleFillLevel < 0.1;
        }

        /// <summary>
        ///     Checks if the ground is close by.
        /// </summary>
        /// <param name="character">The character used to check distance.</param>
        /// <param name="gravity">The gravity direction used to determine in which direction we check.</param>
        /// <returns>Return true if there is ground in 5m distance.</returns>
        private static bool IsGroundInRange(IMyCharacter character, Vector3 gravity) {
            if (gravity.Length() > 0) {
                var position = character.WorldAABB.Center;
                var from = position;

                var offset = Vector3D.Distance(character.GetPosition(), position);
                var strength = gravity.Length() / GRAVITY;
                var length = (float)offset + 5 / (strength > 1 ? strength : 1);

                gravity.Normalize();
                var to = position + gravity * length;
                var results = new List<IHitInfo>();
                MyAPIGateway.Physics.CastRay(from, to, results);
                return results.Any();
            }

            return false;
        }

        /// <summary>
        ///     Checks if enough oxygen around the given character.
        /// </summary>
        /// <param name="character">The character used to check if a helmet is needed.</param>
        /// <returns>Return true if enough oxygen is available.</returns>
        private static bool IsHelmetNeeded(IMyCharacter character) {
            float oxygen;
            if (!MyAPIGateway.Session.SessionSettings.EnableOxygenPressurization) {
                var position = character.GetPosition();
                oxygen = MyAPIGateway.Session.OxygenProviderSystem.GetOxygenInPoint(position);
            } else {
                oxygen = character.OxygenLevel;
            }

            var helmet = oxygen < 0.5;
            return helmet;
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

            MyAPIGateway.Utilities.ShowNotification(MyTexts.GetString(MySpaceTexts.NotificationFuelLow), 2500, "Red");
        }

        /// <inheritdoc />
        public override void HandleInput() {
            if (Settings == null || !Settings.AlwaysAutoHelmet || MyAPIGateway.Gui.ChatEntryVisible || MyAPIGateway.Gui.IsCursorVisible) {
                return;
            }

            var input = MyAPIGateway.Input;
            if (input.IsNewGameControlReleased(MyStringId.Get("HELMET"))) {
                _ticks = Math.Max(-Settings.DelayAfterManualHelmet, _ticks - Settings.DelayAfterManualHelmet);
            }
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

        /// <inheritdoc />
        public override void UpdateAfterSimulation() {
            if (State == State.None) {
                if (Settings != null && Settings.AlwaysAutoHelmet && MyAPIGateway.Session.SessionSettings.EnableOxygen) {
                    _ticks++;
                    if (_ticks >= TICKS_UNTIL_OXYGEN_CHECK - 1) {
                        State = State.CheckOxygenAfterDelay;
                    }
                }

                if (Settings != null && Settings.AdditionalFuelWarning && MyAPIGateway.Session.ControlledObject != null && MyAPIGateway.Session.ControlledObject is IMyCharacter) {
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

            var character = MyAPIGateway.Session.LocalHumanPlayer.Character;
            if (character == null) {
                return;
            }

            bool? helmet = null;

            switch (State) {
                case State.CheckOxygenAfterDelay:
                    _ticks++;
                    if (_ticks < TICKS_UNTIL_OXYGEN_CHECK) {
                        return;
                    }

                    _ticks = 0;

                    helmet = IsHelmetNeeded(character);
                    SetSuitFunctions(character, new SuitData(null, null, helmet, null, null));

                    State = State.None;

                    break;
                case State.ExitCockpit:
                    SetSuitFunctions(character, _dataFromLastCockpit);
                    State = State.None;

                    break;
                case State.Respawn:
                    if (!_hasWaitedATick) {
                        _hasWaitedATick = true;
                        return;
                    }

                    _hasWaitedATick = false;

                    var entity = MyAPIGateway.Session.ControlledObject;
                    var atMedicalRoom = character == entity;
                    if (!atMedicalRoom) {
                        State = State.None;
                        return;
                    }

                    var medicalRoom = GetMedialRoom(character);
                    if (medicalRoom == null) {
                        State = State.None;
                        return;
                    }

                    bool? thruster;
                    bool? dampeners;
                    Vector3? linearVelocity = Vector3.Zero;
                    Vector3? angularVelocity = Vector3.Zero;

                    var cubeGrid = medicalRoom.CubeGrid;
                    var gravity = character.Physics.Gravity;
                    var physics = cubeGrid.Physics;
                    if (physics != null) {
                        linearVelocity = physics.LinearVelocity;
                        angularVelocity = physics.AngularVelocity;
                    }

                    var isGravityDetected = gravity.Length() > 0;
                    var isGroundInRange = IsGroundInRange(character, gravity);
                    var isNotMoving = Math.Abs(linearVelocity.Value.Length()) < Settings.HaltedSpeedTolerance && Math.Abs(angularVelocity.Value.Length()) < Settings.HaltedSpeedTolerance;

                    if (isGravityDetected) {
                        if (isGroundInRange) {
                            thruster = RemoveAutomaticJetpackActivation ? (bool?)null : false;
                            dampeners = isNotMoving;
                        } else {
                            thruster = RemoveAutomaticJetpackActivation ? (bool?)null : true;
                            dampeners = isNotMoving;
                        }
                    } else {
                        thruster = RemoveAutomaticJetpackActivation ? (bool?)null : true;
                        dampeners = isNotMoving;
                    }

                    if (Settings.DisableAutoDampener != DisableAutoDamenerOption.Disable) {
                        dampeners = Settings.DisableAutoDampener == DisableAutoDamenerOption.All ? (bool?)_lastDampenerState : null;
                    }

                    if (MyAPIGateway.Session.SessionSettings.EnableOxygenPressurization) {
                        State = State.CheckOxygenAfterDelay;
                    } else {
                        helmet = IsHelmetNeeded(character);
                        State = State.None;
                    }

                    var data = new SuitData(dampeners, thruster, helmet, linearVelocity, angularVelocity);
                    SetSuitFunctions(character, data);

                    break;
            }
        }

        /// <inheritdoc />
        protected override void UnloadData() {
            Log?.EnterMethod(nameof(UnloadData));

            MyAPIGateway.Session.OnSessionReady -= OnSessionReady;

            if (_chatHandler != null) {
                _chatHandler.Close();
                _chatHandler = null;
            }

            var player = MyAPIGateway.Session.LocalHumanPlayer;
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
                    Settings.AlwaysAutoHelmet = (bool)(object)value;
                    break;
                case Option.AdditionalFuelWarning:
                    Settings.AdditionalFuelWarning = (bool)(object)value;
                    break;
                case Option.FuelThreshold:
                    Settings.FuelThreshold = (float)(object)value;
                    break;
                case Option.DisableAutoDampener:
                    Settings.DisableAutoDampener = (DisableAutoDamenerOption)(object)value;
                    break;
                case Option.HaltedSpeedTolerance:
                    Settings.HaltedSpeedTolerance = (float)(object)value;
                    break;
                case Option.DelayAfterManualHelmet:
                    Settings.DelayAfterManualHelmet = (int)(object)value;
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

                Texts.LoadSupportedLanguages(supportedLanguages);
                if (supportedLanguages.Contains(currentLanguage)) {
                    Texts.LoadTexts(currentLanguage);
                    Log.Info($"Loaded {currentLanguage} translations.");
                } else if (supportedLanguages.Contains(MyLanguagesEnum.English)) {
                    Texts.LoadTexts();
                    Log.Warning($"No {currentLanguage} translations found. Fall back to {MyLanguagesEnum.English} translations.");
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
        ///     Called on <see cref="IMyCharacter.MovementStateChanged" /> event. Used to check if we leave a cockpit.
        /// </summary>
        /// <param name="character">The character who triggered this event.</param>
        /// <param name="oldState">The old movement state.</param>
        /// <param name="newState">The new movement state.</param>
        private void OnMovementStateChanged(IMyCharacter character, MyCharacterMovementEnum oldState, MyCharacterMovementEnum newState) {
            if (Settings.DisableAutoDampener == DisableAutoDamenerOption.All && (newState == MyCharacterMovementEnum.Sitting || newState == MyCharacterMovementEnum.Died)) {
                _lastDampenerState = character.EnabledDamping;
            }

            if (oldState == MyCharacterMovementEnum.Sitting) {
                var cockpit = MyAPIGateway.Session.ControlledObject as IMyCockpit;
                if (cockpit == null) {
                    return;
                }

                var helmet = IsHelmetNeeded(character);

                var velocities = cockpit.GetShipVelocities();
                var linearVelocity = velocities.LinearVelocity;
                var angularVelocity = velocities.AngularVelocity;

                bool? thruster;

                var gravity = cockpit.GetTotalGravity();
                var isGravityDetected = gravity.Length() > 0;
                var isGroundInRange = IsGroundInRange(character, gravity);

                if (isGravityDetected) {
                    if (isGroundInRange) {
                        thruster = RemoveAutomaticJetpackActivation ? (bool?)null : false;
                    } else {
                        thruster = RemoveAutomaticJetpackActivation ? (bool?)null : true;
                    }
                } else {
                    thruster = RemoveAutomaticJetpackActivation ? (bool?)null : true;
                }

                _dataFromLastCockpit = new SuitData(null, thruster, helmet, linearVelocity, angularVelocity);
                State = State.ExitCockpit;
            }
        }

        /// <summary>
        ///     Executed if Session is ready.
        /// </summary>
        private void OnSessionReady() {
            MyAPIGateway.Session.OnSessionReady -= OnSessionReady;
            RemoveAutomaticJetpackActivation = MyAPIGateway.Session.Mods.Any(x => x.PublishedFileId == REMOVE_AUTOMATIC_JETPACK_ACTIVATION_ID);

            var player = MyAPIGateway.Session.LocalHumanPlayer;
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

            SetUpdateOrder(MyUpdateOrder.AfterSimulation);
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
        ///     Sets suit functions.
        /// </summary>
        /// <param name="character">The character which should enable/disable the systems.</param>
        /// <param name="data">A data structure to check which systems should be enabled/disabled</param>
        private void SetSuitFunctions(IMyCharacter character, SuitData data) {
            if (character == null) {
                return;
            }

            if (data.Dampeners != null && character.EnabledDamping != data.Dampeners.Value) {
                character.SwitchDamping();
            }

            if (data.Thruster != null && character.EnabledThrusts != data.Thruster.Value) {
                character.SwitchThrusts();
            }

            if (character.EnabledThrusts) {
                if (data.LinearVelocity.HasValue && data.AngularVelocity.HasValue) {
                    character.Physics.SetSpeeds(data.LinearVelocity.Value, data.AngularVelocity.Value);
                } else if (data.LinearVelocity.HasValue) {
                    character.Physics.SetSpeeds(data.LinearVelocity.Value, Vector3.Zero);
                } else if (data.AngularVelocity.HasValue) {
                    character.Physics.SetSpeeds(Vector3.Zero, data.AngularVelocity.Value);
                }
            }

            if (data.Helmet != null && character.EnabledHelmet != data.Helmet.Value) {
                character.SwitchHelmet();
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
