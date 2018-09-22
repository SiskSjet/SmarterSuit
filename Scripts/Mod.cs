using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using Sisk.Utils.Logging;
using Sisk.Utils.Logging.DefaultHandler;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace Sisk.SmarterSuit {
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class Mod : MySessionComponentBase {
        public const string NAME = "Smarter Suit";

        // important: change to info | warning | error or none before publishing this mod.
        private const LogEventLevel DEFAULT_LOG_EVENT_LEVEL = LogEventLevel.All;

        private const string LOG_FILE_TEMPLATE = "{0}.log";
        private const ulong REMOVE_AUTOMATIC_JETPACK_ACTIVATION_ID = 782845808;
        private const float SPEED_TOLERANCE = 0.01f;
        private const int TICKS_UNTIL_OXYGEN_CHECK = 100;
        private static readonly string LogFile = string.Format(LOG_FILE_TEMPLATE, NAME);
        private SuitData _dataFromLastCockpit;
        private IMyIdentity _identity;
        private int _ticks;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Mod" /> session component.
        /// </summary>
        public Mod() {
            InitializeLogging();
        }

        /// <summary>
        ///     Logger used for logging.
        /// </summary>
        public ILogger Log { get; private set; }

        /// <summary>
        ///     Indicates if the 'Remove all automatic jetpack activation' is available.
        /// </summary>
        private bool RemoveAutomaticJetpackActivation { get; set; }

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

            if (data.Dampeners != null && character.EnabledDamping != data.Dampeners.Value) {
                character.SwitchDamping();
            }

            if (data.Helmet != null && character.EnabledHelmet != data.Helmet.Value) {
                character.SwitchHelmet();
            }

            if (data.Thruster != null && character.EnabledThrusts != data.Thruster.Value) {
                character.SwitchThrusts();

                if (data.LinearVelocity.HasValue && data.AngularVelocity.HasValue) {
                    character.Physics.SetSpeeds(data.LinearVelocity.Value, data.AngularVelocity.Value);
                } else if (data.LinearVelocity.HasValue) {
                    character.Physics.SetSpeeds(data.LinearVelocity.Value, Vector3.Zero);
                } else if (data.AngularVelocity.HasValue) {
                    character.Physics.SetSpeeds(Vector3.Zero, data.AngularVelocity.Value);
                }
            }
        }

        /// <inheritdoc />
        public override void LoadData() {
            if (MyAPIGateway.Utilities.IsDedicated) {
                return;
            }

            MyAPIGateway.Session.OnSessionReady += OnSessionReady;
        }

        /// <inheritdoc />
        public override void UpdateAfterSimulation() {
            if (State == State.None) {
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
        }

            if (Log != null) {
                Log.Info("Logging stopped");
                Log.Flush();
                Log.Close();
                Log = null;
            }
        }
        /// <summary>
        ///     Initialize the logging system.
        /// </summary>
        private void InitializeLogging() {
            Log = Logger.ForScope<Mod>();
            Log.Register(new WorldStorageHandler(LogFile, LogFormatter, DEFAULT_LOG_EVENT_LEVEL, 25));

            using (Log.BeginMethod(nameof(InitializeLogging))) {
                Log.Info("Logging initialized");
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

        private void OnSessionReady() {
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
        ///     Register character events.
        /// </summary>
        /// <param name="character">The character.</param>
        private void RegisterEvents(IMyCharacter character) {
            if (character != null) {
                character.MovementStateChanged += OnMovementStateChanged;
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