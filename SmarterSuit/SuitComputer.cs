using System;
using System.Collections.Generic;
using System.Linq;
using Jakaria.API;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using Sisk.SmarterSuit.Data;
using Sisk.SmarterSuit.Settings;
using Sisk.Utils.Logging;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

// Sandbox.Game.Entities.IMyControllableEntity have to be the explicit type to not produce ambiguous call errors.
using IMyControllableEntity = Sandbox.Game.Entities.IMyControllableEntity;

namespace Sisk.SmarterSuit {

    public class SuitComputer {
        private const float GRAVITY = 9.81f;
        private const string HYDROGEN_BOTTLE_ID = "MyObjectBuilder_GasContainerObject/HydrogenBottle";
        private const string HYDROGEN_ID = "MyObjectBuilder_GasProperties/Hydrogen";
        private const double MAX_RUNTIME_IN_MILLISECONDS = 1;
        private const int MAX_SIMULTANEOUS_WORK = 16;
        private const string MEDICAL_ROOM = "MyObjectBuilder_MedicalRoom";
        private const string SURVIVAL_KIT = "MyObjectBuilder_SurvivalKit";
        private const int TICKS_UNTIL_FUEL_CHECK = 30;
        private const int TICKS_UNTIL_LIGHT_CHECK = 30;
        private const int TICKS_UNTIL_OXYGEN_CHECK = 30;
        private readonly List<DelayedWork> _delayedWorkQueue = new List<DelayedWork>();
        private readonly IMyPlayer _player;
        private readonly Queue<Work> _workQueue = new Queue<Work>();
        private readonly Queue<Work> _workQueueForUpdate = new Queue<Work>();
        private int _autoAlignTicks;
        private int _autoHelmetTicks;
        private int _fuelCheckTicks;

        private IMyIdentity _identity;
        private bool _isAutoAlignRunning;
        private bool _isFlying;
        private bool _lastDampenerState;
        private bool _lastLightState;
        private bool _stopAutoAlign;
        private bool _wasFuelUnderThresholdBefore;

        /// <summary>
        ///     Creates a new instance of <see cref="SuitComputer" />.
        /// </summary>
        /// <param name="player">The local player used to register required events.</param>
        private SuitComputer(IMyPlayer player) {
            Log = Mod.Static.Log.ForScope<SuitComputer>();

            _player = player;
            player.IdentityChanged += OnIdentityChanged;

            _identity = player.Identity;
            _identity.CharacterChanged += OnCharacterChanged;

            var character = player.Character;
            if (character != null) {
                RegisterEvents(character);
                _isFlying = character.CurrentMovementState == MyCharacterMovementEnum.Flying;
            }
        }

        /// <summary>
        ///     Logger used for logging.
        /// </summary>
        private ILogger Log { get; }

        /// <summary>
        ///     Create a new instance of <see cref="SuitComputer" />.
        /// </summary>
        /// <returns>A new instance of <see cref="SuitComputer" /> or null if something when wrong.</returns>
        public static SuitComputer Create() {
            var player = MyAPIGateway.Session.LocalHumanPlayer;
            if (player?.Identity == null) {
                // todo: throw an error?
                Mod.Static.Log.ForScope<SuitComputer>().Error("Player or player identity was not set at this point...");

                return null;
            }

            return new SuitComputer(player);
        }

        /// <summary>
        ///     Un-register all events.
        /// </summary>
        public void Close() {
            _player.IdentityChanged -= OnIdentityChanged;
            _identity.CharacterChanged -= OnCharacterChanged;
            if (_player.Character != null) {
                _player.Character.MovementStateChanged -= OnMovementStateChanged;
            }
        }

        /// <summary>
        ///     Will delay the AutoHelmet check for the configured delay.
        /// </summary>
        public void DelayAutoHelmet() {
            _autoHelmetTicks = Math.Max(-Mod.Static.Settings.DelayAfterManualHelmet, _autoHelmetTicks - Mod.Static.Settings.DelayAfterManualHelmet);
        }

        /// <summary>
        ///     Resets the AutoAlign timeout when flying.
        /// </summary>
        public void ResetAutoAlignTimeout() {
            using (Log.BeginMethod(nameof(ResetAutoAlignTimeout))) {
                var character = MyAPIGateway.Session.LocalHumanPlayer?.Character;
                if (character == null) {
                    Log.Warning("No character found for local player.");
                    return;
                }

                if (character.CurrentMovementState == MyCharacterMovementEnum.Flying) {
                    _autoAlignTicks = 0;
                    _stopAutoAlign = true;
                }
            }
        }

        /// <summary>
        ///     Update <see cref="SuitComputer" />.
        /// </summary>
        public void Update() {
            if (Mod.Static.Settings == null) {
                return;
            }

            var startTime = DateTime.UtcNow;
            if (MyAPIGateway.Session.ControlledObject != null) {
                if (Mod.Static.Settings.AlwaysAutoHelmet && MyAPIGateway.Session.SessionSettings.EnableOxygen) {
                    _autoHelmetTicks++;
                    if (_autoHelmetTicks >= TICKS_UNTIL_OXYGEN_CHECK - 1) {
                        _autoHelmetTicks = 0;
                        _workQueue.Enqueue(new Work(ToggleHelmetIfNeeded));
                    }
                }

                if (MyAPIGateway.Session.ControlledObject is IMyCharacter) {
                    if (Mod.Static.Settings.AdditionalFuelWarning) {
                        _fuelCheckTicks++;

                        if (_fuelCheckTicks >= TICKS_UNTIL_FUEL_CHECK) {
                            _fuelCheckTicks = 0;
                            _workQueue.Enqueue(new Work(ShowFuelLowWarningIfNeeded));
                        }
                    }

                    if (Mod.Static.Settings.AlignToGravity) {
                        if (_isFlying) {
                            _autoAlignTicks++;
                            if (!_isAutoAlignRunning && _autoAlignTicks >= Mod.Static.Settings.AlignToGravityDelay) {
                                _autoAlignTicks = 0;
                                _workQueue.Enqueue(new Work(AutoAlign));
                            }
                        } else if (_isAutoAlignRunning) {
                            _autoAlignTicks = 0;
                            _isAutoAlignRunning = false;
                            _stopAutoAlign = true;
                        }
                    }
                }
            }

            int amount;
            if (_delayedWorkQueue.Any()) {
                for (var i = 0; i < _delayedWorkQueue.Count; i++) {
                    var work = _delayedWorkQueue[i];
                    work.UpdateTicks();
                    if (work.RunAfterTicks <= 0) {
                        _workQueueForUpdate.Enqueue(work);
                        _delayedWorkQueue.RemoveAt(i);
                    }
                }
            }

            if (_workQueue.Any()) {
                amount = _workQueueForUpdate.Count + _workQueue.Count < MAX_SIMULTANEOUS_WORK ? _workQueue.Count : MAX_SIMULTANEOUS_WORK - _workQueueForUpdate.Count;
                for (var i = 0; i < amount; i++) {
                    _workQueueForUpdate.Enqueue(_workQueue.Dequeue());
                }
            }

            if (_workQueueForUpdate.Any()) {
                amount = _workQueueForUpdate.Count < MAX_SIMULTANEOUS_WORK ? _workQueueForUpdate.Count : MAX_SIMULTANEOUS_WORK - _workQueueForUpdate.Count;
                for (var i = 0; i < amount; i++) {
                    if (NotOverRuntime(startTime)) {
                        _workQueueForUpdate.Dequeue()?.DoWork();
                    } else {
                        Log.Warning($"R: {(DateTime.UtcNow - startTime).TotalMilliseconds:F2} -> {_workQueueForUpdate.Peek()?.Name} will be executed in next update.");
                    }
                }
            }
        }

        /// <summary>
        ///     Calculates alignment to given gravity.
        /// </summary>
        /// <param name="character">The character for which this is calculated.</param>
        /// <param name="gravity">The gravity vector.</param>
        /// <param name="up">The up vector.</param>
        /// <param name="forward">The forward vector.</param>
        private static void CalculateAlignment(IMyCharacter character, ref Vector3 gravity, ref Vector3 up, ref Vector3 forward) {
            var invertedNormalizedGravity = -Vector3.Normalize(gravity);
            var direction = invertedNormalizedGravity;

            if (character.Physics != null) {
                var supportNormal = character.Physics.SupportNormal;
                var definition = character.Definition as MyCharacterDefinition;
                if (definition != null) {
                    if (definition.RotationToSupport == MyEnumCharacterRotationToSupport.OneAxis) {
                        var num1 = invertedNormalizedGravity.Dot(ref supportNormal);
                        if (!MyUtils.IsZero(num1 - 1f) && !MyUtils.IsZero(num1 + 1f)) {
                            var cross = invertedNormalizedGravity.Cross(supportNormal);
                            cross.Normalize();
                            direction = Vector3.Lerp(supportNormal, invertedNormalizedGravity, Math.Abs(cross.Dot(character.WorldMatrix.Forward)));
                        }
                    } else if (definition.RotationToSupport == MyEnumCharacterRotationToSupport.Full) {
                        direction = supportNormal;
                    }
                }
            }

            var dot = Vector3.Dot(up, direction) / (up.Length() * direction.Length());
            if (float.IsNaN(dot) || float.IsNegativeInfinity(dot) || float.IsPositiveInfinity(dot)) {
                dot = 1f;
            }

            dot = MathHelper.Clamp(dot, -1f, 1f);
            if (MyUtils.IsZero(dot - 1f, 1E-08f)) {
                return;
            }

            var angle = !MyUtils.IsZero(dot + 1f, 1E-08f) ? (float)Math.Acos(dot) : 0.1f;
            angle = Math.Min(Math.Abs(angle), 0.04f) * Math.Sign(angle);
            var axis = Vector3.Cross(up, direction);
            if (axis.LengthSquared() <= 0.0) {
                return;
            }

            axis = Vector3.Normalize(axis);
            up = Vector3.TransformNormal(up, Matrix.CreateFromAxisAngle(axis, angle));
            forward = Vector3.TransformNormal(forward, Matrix.CreateFromAxisAngle(axis, angle));
        }

        /// <summary>
        ///     Check oxygen available around given character.
        /// </summary>
        /// <param name="character">Character used to check oxygen level around him.</param>
        /// <returns>Return the oxygen level around the given character.</returns>
        private static float GetOxygenLevel(IMyCharacter character) {
            float oxygen;
            if (!MyAPIGateway.Session.SessionSettings.EnableOxygenPressurization) {
                var position = character.GetPosition();
                oxygen = MyAPIGateway.Session.OxygenProviderSystem.GetOxygenInPoint(position);
            } else {
                oxygen = character.OxygenLevel;
            }

            return oxygen;
        }

        /// <summary>
        ///     Gets the medical room that is closest to the given entity.
        /// </summary>
        /// <param name="entity">The entity used to find the closest medical room.</param>
        /// <returns>Return the closest medical room or <see langword="null" />.</returns>
        private static IMyTerminalBlock GetRespawnLocation(IMyEntity entity) {
            var sphere = entity.PositionComp.WorldVolume;
            var entities = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref sphere).OfType<IMyCubeGrid>().ToList();
            var medicalRooms = new List<IMyTerminalBlock>();
            var blocks = new List<IMyTerminalBlock>();

            foreach (var cubeGrid in entities) {
                blocks.Clear();
                MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(cubeGrid).GetBlocksOfType(blocks, x => x.BlockDefinition.TypeIdString == MEDICAL_ROOM || x.BlockDefinition.TypeIdString == SURVIVAL_KIT);
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
        /// <param name="character">The character for which will be checked.</param>
        /// <param name="threshold">The threshold.</param>
        /// <returns>Return true if fuel is under given threshold.</returns>
        private static bool IsFuelUnderThreshold(IMyCharacter character, float threshold) {
            if (MyAPIGateway.Session.CreativeMode) {
                return false;
            }

            var jetpackComponent = character.Components.Get<MyCharacterJetpackComponent>();
            var oxygenComponent = character.Components.Get<MyCharacterOxygenComponent>();
            if (jetpackComponent == null || oxygenComponent == null) {
                return false;
            }

            float bottleFillLevel = 0;
            var inventory = character.GetInventory() as MyInventory;
            if (inventory != null) {
                var items = inventory.GetItems();
                foreach (var item in items) {
                    if (item.Content.GetObjectId().ToString() == HYDROGEN_BOTTLE_ID) {
                        var bottle = item.Content as MyObjectBuilder_GasContainerObject;
                        if (bottle != null) {
                            bottleFillLevel += bottle.GasLevel;
                        }
                    }
                }
            }

            var hydrogenStorage = 0.125f;
            var definition = character.Definition as MyCharacterDefinition;
            var suitResourceDefinition = definition?.SuitResourceStorage.FirstOrDefault(x => x.Id.ToString() == HYDROGEN_ID);
            if (suitResourceDefinition != null) {
                hydrogenStorage = suitResourceDefinition.MaxCapacity / 1000;
            }

            return oxygenComponent.GetGasFillLevel(MyCharacterOxygenComponent.HydrogenId) < threshold && bottleFillLevel < hydrogenStorage;
        }

        /// <summary>
        ///     Checks if the ground is close by.
        /// </summary>
        /// <param name="character">The character used to check distance.</param>
        /// <param name="gravity">The gravity direction used to determine in which direction we check.</param>
        /// <returns>Return true if there is ground in 5m distance.</returns>
        private static bool IsGroundInRange(IMyCharacter character, Vector3 gravity) {
            var results = new List<IHitInfo>();
            var position = character.WorldAABB.Center;
            var matrix = character.WorldMatrix;
            var from = position + matrix.Forward * .5;
            var to = from + matrix.Down * 2;

            if (gravity.Length() > 0) {
                var offset = Vector3D.Distance(character.GetPosition(), from);
                var strength = gravity.Length() / GRAVITY;
                var length = (float)offset + 5 / (strength > 1 ? strength : 1);

                gravity.Normalize();
                to = from + gravity * length;
            }

            MyAPIGateway.Physics.CastRay(from, to, results);
            return results.Any();
        }

        /// <summary>
        ///     Checks if the ground is close by.
        /// </summary>
        /// <param name="character">The character used to check distance.</param>
        /// <returns>Return true if there is ground in 5m distance.</returns>
        private static bool IsGroundInRange(IMyCharacter character) {
            return IsGroundInRange(character, Vector3.Zero);
        }

        /// <summary>
        ///     Check if runtime is not above limit.
        /// </summary>
        /// <param name="startTime">The start time.</param>
        /// <returns>Return true if runtime is not above limit.</returns>
        private static bool NotOverRuntime(DateTime startTime) {
            var runtime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            return runtime < MAX_RUNTIME_IN_MILLISECONDS;
        }

        /// <summary>
        ///     Show a 'fuel low' warning.
        /// </summary>
        private static void ShowFuelLowWarningNotification(IMyCharacter character) {
            var soundEmitter = new MyEntity3DSoundEmitter((MyEntity)character);
            var pair = new MySoundPair("ArcHudVocFuelLow");
            soundEmitter.PlaySingleSound(pair);

            MyAPIGateway.Utilities.ShowNotification(MyTexts.GetString(MySpaceTexts.NotificationFuelLow), 2500, "Red");
        }

        /// <summary>
        ///     Auto Align character to gravity.
        /// </summary>
        private void AutoAlign(Work.Data obj) {
            using (Log.BeginMethod(nameof(AutoAlign))) {
                var character = MyAPIGateway.Session.LocalHumanPlayer?.Character;
                if (character == null) {
                    Log.Warning("No character found for local player.");
                    return;
                }

                _isAutoAlignRunning = true;

                var physics = character.Physics;
                if (physics != null) {
                    var gravity = physics.Gravity;
                    if (gravity.Length() > 0) {
                        gravity.Normalize();
                        var matrix = character.WorldMatrix;
                        var up = (Vector3)matrix.Up;
                        var forward = (Vector3)matrix.Forward;

                        CalculateAlignment(character, ref gravity, ref up, ref forward);
                        character.SetWorldMatrix(MatrixD.CreateWorld(matrix.Translation, forward, up));
                    }
                } else {
                    Log.Error("No physics found for character.");
                }

                if (!_stopAutoAlign && _isFlying) {
                    _workQueue.Enqueue(new Work(AutoAlign));
                } else {
                    _isAutoAlignRunning = false;
                    _stopAutoAlign = false;
                    _autoAlignTicks = 0;
                }
            }
        }

        private void MoveAndRotate(Work.Data workData) {
            using (Log.BeginMethod(nameof(MoveAndRotate))) {
                var data = workData as MoveAnRotateData;
                if (data == null) {
                    Log.Warning("Invalid workData.");
                    return;
                }

                var character = MyAPIGateway.Session.LocalHumanPlayer?.Character;
                if (character == null) {
                    Log.Warning("No character found for local player.");
                    return;
                }

                var movementIndicator = data.MoveIndicator;
                var rotationIndicator = data.RotationIndicator;
                var rollIndicator = data.RollIndicator;

                character.MoveAndRotate(movementIndicator, rotationIndicator, rollIndicator);
            }
        }

        /// <summary>
        ///     Called if character for local player is changed.
        ///     Used to remap events for the character of the local player.
        /// </summary>
        /// <param name="oldCharacter"></param>
        /// <param name="newCharacter"></param>
        private void OnCharacterChanged(IMyCharacter oldCharacter, IMyCharacter newCharacter) {
            var isRespawn = oldCharacter != newCharacter;
            var oldBroadcastState = false;

            if (oldCharacter != null) {
                oldCharacter.MovementStateChanged -= OnMovementStateChanged;

                // Sandbox.Game.Entities.IMyControllableEntity have to be the explicit type to not produce ambiguous call errors.
                var control = oldCharacter as IMyControllableEntity;
                if (control != null) {
                    oldBroadcastState = control.EnabledBroadcasting;
                }
            }

            newCharacter.MovementStateChanged += OnMovementStateChanged;

            if (isRespawn) {
                _workQueue.Enqueue(new Work(Respawned, new BroadcastWorkData(oldBroadcastState)));
            }
        }

        /// <summary>
        ///     Called if local player identity changed (e.g. died with permadeath on).
        ///     Used to remap CharacterChanged for the local player identity.
        /// </summary>
        /// <param name="player">The player which identity was changed.</param>
        /// <param name="identity">The new identity.</param>
        private void OnIdentityChanged(IMyPlayer player, IMyIdentity identity) {
            _identity.CharacterChanged -= OnCharacterChanged;
            _identity = identity;
            if (identity != null) {
                _identity.CharacterChanged += OnCharacterChanged;
            }
        }

        /// <summary>
        ///     Called on <see cref="IMyCharacter.MovementStateChanged" /> event. Used to check if we leave a cockpit.
        /// </summary>
        /// <param name="character">The character who triggered this event.</param>
        /// <param name="oldState">The old movement state.</param>
        /// <param name="newState">The new movement state.</param>
        private void OnMovementStateChanged(IMyCharacter character, MyCharacterMovementEnum oldState, MyCharacterMovementEnum newState) {
            if ((newState == MyCharacterMovementEnum.Sitting || newState == MyCharacterMovementEnum.Died)) {
                if (Mod.Static.Settings.DisableAutoDampener == DisableAutoDampenerOption.All) {
                    _lastDampenerState = character.EnabledDamping;
                }

                if (Mod.Static.Settings.SwitchHelmetLight) {
                    _lastLightState = character.EnabledLights;
                    _workQueue.Enqueue(new Work(ToggleHelmetLightIfNeeded));
                }

                if (Mod.Static.Settings.AlwaysAutoHelmet) {
                    _workQueue.Enqueue(new Work(ToggleHelmetIfNeeded));
                }
            }

            _isFlying = newState == MyCharacterMovementEnum.Flying;
            switch (oldState) {
                case MyCharacterMovementEnum.Ladder:
                case MyCharacterMovementEnum.LadderOut:
                case MyCharacterMovementEnum.LadderDown:
                case MyCharacterMovementEnum.LadderUp:
                    switch (newState) {
                        case MyCharacterMovementEnum.Ladder:
                        case MyCharacterMovementEnum.LadderOut:
                        case MyCharacterMovementEnum.LadderDown:
                        case MyCharacterMovementEnum.LadderUp:
                            break;

                        default:
                            var position = character.GetPosition();
                            var sphere = character.PositionComp.WorldVolume;
                            var entities = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref sphere).OfType<IMyCubeGrid>().ToList();
                            var cubeGrid = entities.OrderBy(x => Vector3.Distance(x.GetPosition(), position)).FirstOrDefault();
                            _workQueue.Enqueue(new Work(ToggleJetpackAndDampenersIfNeeded, new ThrusterWorkData(cubeGrid, leavedLadder: oldState == MyCharacterMovementEnum.LadderOut)));
                            break;
                    }

                    break;

                case MyCharacterMovementEnum.Sitting:
                    if (Mod.Static.Settings.AlwaysAutoHelmet) {
                        _workQueue.Enqueue(new Work(ToggleHelmetIfNeeded));
                    }

                    if (!Mod.Static.RemoveAutomaticJetpackActivationModAvailable) {
                        var cockpit = MyAPIGateway.Session.ControlledObject as IMyCockpit;
                        if (cockpit == null) {
                            return;
                        }

                        _workQueue.Enqueue(new Work(ToggleJetpackAndDampenersIfNeeded, new ThrusterWorkData(cockpit)));
                    }

                    if (Mod.Static.Settings.SwitchHelmetLight) {
                        _workQueue.Enqueue(new Work(ToggleHelmetLightIfNeeded));
                    }

                    break;
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

        private void Respawned(Work.Data workData) {
            using (Log.BeginMethod(nameof(Respawned))) {
                var character = MyAPIGateway.Session.LocalHumanPlayer?.Character;
                if (character == null) {
                    Log.Warning("No character found for local player.");
                    return;
                }

                var respawnLocation = GetRespawnLocation(character);
                if (respawnLocation != null) {
                    var lastEntity = respawnLocation.CubeGrid;
                    _workQueue.Enqueue(new Work(ToggleHelmetIfNeeded));
                    if (!Mod.Static.RemoveAutomaticJetpackActivationModAvailable) {
                        _workQueue.Enqueue(new Work(ToggleJetpackAndDampenersIfNeeded, new ThrusterWorkData(lastEntity, true)));
                    }

                    if (workData != null && Mod.Static.Settings.RememberBroadcast) {
                        _workQueue.Enqueue(new Work(SetBroadcast, workData));
                    }
                }
            }
        }

        private void SetBroadcast(Work.Data workData) {
            using (Log.BeginMethod(nameof(SetBroadcast))) {
                var data = workData as BroadcastWorkData;
                if (data == null) {
                    Log.Warning("Invalid workData.");
                    return;
                }

                var character = MyAPIGateway.Session.LocalHumanPlayer?.Character;
                if (character == null) {
                    Log.Warning("No character found for local player.");
                    return;
                }

                // Sandbox.Game.Entities.IMyControllableEntity have to be the explicit type to not produce ambiguous call errors.
                var control = character as IMyControllableEntity;
                if (control != null) {
                    // don't know why EnabledBroadcasting == data.State is working. I would expect EnabledBroadcasting != data.State to be correct, but it would produce a reverse behavior.
                    if (control.EnabledBroadcasting == data.State) {
                        control.SwitchBroadcasting();
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="workData"></param>
        private void ShowFuelLowWarningIfNeeded(Work.Data workData) {
            using (Log.BeginMethod(nameof(ShowFuelLowWarningIfNeeded))) {
                var character = MyAPIGateway.Session.LocalHumanPlayer?.Character;
                if (character == null) {
                    Log.Warning("No character found for local player.");
                    return;
                }

                var isFuelUnderThreshold = IsFuelUnderThreshold(character, Mod.Static.Settings.FuelThreshold);
                if (isFuelUnderThreshold && !_wasFuelUnderThresholdBefore) {
                    ShowFuelLowWarningNotification(character);
                }

                _wasFuelUnderThresholdBefore = isFuelUnderThreshold;
            }
        }

        /// <summary>
        ///     Will enable dampeners when grid is not moving or planetary gravity is detected and no ground is in range.
        /// </summary>
        private void ToggleDampenersIfNeeded(IMyCharacter character, bool isNotMoving, bool hasGravity, bool isGroundInRange, bool lastDampenerState) {
            if (Mod.Static.Settings.DisableAutoDampener != DisableAutoDampenerOption.Mod) {
                var dampenersRequired = Mod.Static.Settings.DisableAutoDampener == DisableAutoDampenerOption.All ? lastDampenerState : isNotMoving || hasGravity && !isGroundInRange;
                if (character.EnabledDamping != dampenersRequired) {
                    character.SwitchDamping();
                }
            }
        }

        /// <summary>
        ///     Will open the character helmet if enough oxygen is around or close it if not.
        /// </summary>
        private void ToggleHelmetIfNeeded(Work.Data workData) {
            using (Log.BeginMethod(nameof(ToggleHelmetIfNeeded))) {
                var character = MyAPIGateway.Session.LocalHumanPlayer?.Character;
                if (character == null) {
                    Log.Warning("No character found for local player.");
                    return;
                }

                var underwater = false;
                if (Mod.Static.WaterModAvailable && WaterModAPI.Registered) {
                    underwater = WaterModAPI.IsUnderwater(character.GetHeadMatrix(true).Translation);
                }

                var oxygen = GetOxygenLevel(character);
                var suitOxy = character.GetSuitGasFillLevel(MyCharacterOxygenComponent.OxygenId);
                var required = oxygen <= 0.5 || underwater;
                var shouldOpen = oxygen > 0.6 && !underwater;
                if (character.CurrentMovementState == MyCharacterMovementEnum.Sitting) {
                    var cockpit = MyAPIGateway.Session.ControlledObject as IMyCockpit;
                    if (cockpit != null) {
                        required = cockpit.OxygenFilledRatio == 0 && oxygen <= 0.5;
                        shouldOpen = !required;
                    }
                }

                if (required && !character.EnabledHelmet || !required && character.EnabledHelmet && suitOxy > 0.2 && shouldOpen || !required && character.EnabledHelmet && suitOxy < 0.2) {
                    character.SwitchHelmet();
                }
            }
        }

        private void ToggleHelmetLightIfNeeded(Work.Data data) {
            using (Log.BeginMethod(nameof(ToggleHelmetLightIfNeeded))) {
                var character = MyAPIGateway.Session.LocalHumanPlayer?.Character;
                if (character == null) {
                    Log.Warning("No character found for local player.");
                    return;
                }

                if (character.EnabledLights && (character.CurrentMovementState == MyCharacterMovementEnum.Sitting)) {
                    character.SwitchLights();
                }

                if (Mod.Static.Settings.TurnLightsBackOn && _lastLightState != character.EnabledLights && (character.CurrentMovementState != MyCharacterMovementEnum.Sitting && character.CurrentMovementState != MyCharacterMovementEnum.Died)) {
                    character.SwitchLights();
                }
            }
        }

        /// <summary>
        ///     Will enable jetpack when no gravity is present and no ground is in range.
        ///     Will enable dampeners when allowed and grid is not moving or planetary gravity is detected and no ground is in
        ///     range.
        /// </summary>
        private void ToggleJetpackAndDampenersIfNeeded(Work.Data workData) {
            using (Log.BeginMethod(nameof(ToggleJetpackAndDampenersIfNeeded))) {
                var data = workData as ThrusterWorkData;
                if (data == null) {
                    Log.Warning("Invalid workData.");
                    return;
                }

                var character = MyAPIGateway.Session.LocalHumanPlayer?.Character;
                if (character == null) {
                    Log.Warning("No character found for local player.");
                    return;
                }

                var lastEntity = data.LastEntity;
                var allowSwitchingDampeners = data.AllowSwitchingDampeners;
                var leavedLadder = data.LeavedLadder;

                var gravity = Vector3D.Zero;
                var linearVelocity = Vector3D.Zero;
                var angularVelocity = Vector3D.Zero;
                if (lastEntity != null && !lastEntity.Closed) {
                    var cockpit = lastEntity as IMyCockpit;
                    if (cockpit != null) {
                        gravity = cockpit.GetTotalGravity();
                        var velocities = cockpit.GetShipVelocities();
                        linearVelocity = velocities.LinearVelocity;
                        angularVelocity = velocities.AngularVelocity;
                    } else {
                        var physics = lastEntity.Physics;
                        if (physics == null) {
                            Log.Warning("No physics found for entity.");
                        } else {
                            gravity = physics.Gravity;
                            linearVelocity = physics.LinearVelocity;
                            angularVelocity = physics.AngularVelocity;
                        }
                    }
                } else {
                    var physics = character.Physics;
                    if (physics == null) {
                        Log.Warning("No physics found for local player character.");
                        return;
                    }

                    gravity = physics.Gravity;
                }

                var hasGravity = gravity.Length() > 0;
                var isGroundInRange = hasGravity ? IsGroundInRange(character, gravity) : IsGroundInRange(character);
                var isNotMoving = Math.Abs(linearVelocity.Length()) < Mod.Static.Settings.HaltedSpeedTolerance && Math.Abs(angularVelocity.Length()) < Mod.Static.Settings.HaltedSpeedTolerance;
                var thrustRequired = !isGroundInRange;
                if (!thrustRequired && !hasGravity) {
                    linearVelocity += character.WorldMatrix.Down * 2;
                }

                if (leavedLadder) {
                    var moveAnRotateData = new MoveAnRotateData(new Vector3(0, 0, -1), Vector2.Zero, 0);
                    MoveAndRotate(moveAnRotateData);
                    for (var i = 0; i < 10; i++) {
                        _delayedWorkQueue.Add(new DelayedWork(MoveAndRotate, 1 + i, moveAnRotateData));
                    }
                }

                character.Physics.SetSpeeds(linearVelocity, angularVelocity);

                if (allowSwitchingDampeners) {
                    ToggleDampenersIfNeeded(character, isNotMoving, hasGravity, isGroundInRange, _lastDampenerState);
                }

                if (character.EnabledThrusts != thrustRequired) {
                    character.SwitchThrusts();
                }
            }
        }
    }
}