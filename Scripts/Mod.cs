using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace Sisk.SmarterSuit {
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class Mod : MySessionComponentBase {
        private const float SPEED_TOLERANCE = 0.01f;
        private const int WAIT_TICKS_UTIL_CHECK = 100;
        private SuitData _dataFromLastCockpit;
        private IMyIdentity _identity;
        private int _ticks;

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
            bool helmet;

            if (!MyAPIGateway.Session.SessionSettings.EnableOxygenPressurization) {
                var position = character.GetPosition();
                var oxygen = MyAPIGateway.Session.OxygenProviderSystem.GetOxygenInPoint(position);
                helmet = oxygen < 0.5;
            } else {
                helmet = character.EnvironmentOxygenLevel < 0.5;
            }

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
            }

            if (data.LinearVelocity.HasValue && data.AngularVelocity.HasValue) {
                character.Physics.SetSpeeds(data.LinearVelocity.Value, data.AngularVelocity.Value);
            } else if (data.LinearVelocity.HasValue) {
                character.Physics.SetSpeeds(data.LinearVelocity.Value, Vector3.Zero);
            } else if (data.AngularVelocity.HasValue) {
                character.Physics.SetSpeeds(Vector3.Zero, data.AngularVelocity.Value);
            }
        }

        /// <inheritdoc />
        public override void BeforeStart() {
            if (MyAPIGateway.Utilities.IsDedicated) {
                return;
            }

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

        /// <inheritdoc />
        public override void UpdateAfterSimulation() {
            if (State == State.None) {
                return;
            }

            IMyCharacter character;
            bool? helmet = null;

            if (State == State.CheckOxygenAfterRespawn) {
                _ticks++;
                if (_ticks < WAIT_TICKS_UTIL_CHECK) {
                    return;
                }

                _ticks = 0;

                character = MyAPIGateway.Session.Player.Character;
                helmet = character.EnvironmentOxygenLevel < 0.5;
                SetSuitFunctions(character, new SuitData(null, null, helmet, null, null));
                State = State.None;
                return;
            }

            character = MyAPIGateway.Session.Player.Character;

            if (State == State.ExitCockpit) {
                SetSuitFunctions(character, _dataFromLastCockpit);
                State = State.None;
                return;
            }

            if (State == State.Respawn) {
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
                var linearVelocity = Vector3.Zero;
                var angularVelocity = Vector3.Zero;
                var gravity = character.Physics.Gravity;

                var physics = cubeGrid.Physics;
                if (physics != null) {
                    linearVelocity = physics.LinearVelocity;
                    angularVelocity = physics.AngularVelocity;
                }

                bool thruster;
                bool dampeners;

                var isGravityDetected = gravity.Length() > 0;
                var isGroundInRange = IsGroundInRange(character, gravity);
                var isNotMoving = Math.Abs(linearVelocity.Length()) < SPEED_TOLERANCE && Math.Abs(angularVelocity.Length()) < SPEED_TOLERANCE;

                if (isGravityDetected) {
                    if (isGroundInRange) {
                        thruster = false;
                        dampeners = isNotMoving;
                    } else {
                        thruster = true;
                        dampeners = isNotMoving;
                    }
                } else {
                    thruster = true;
                    dampeners = isNotMoving;
                }

                if (!MyAPIGateway.Session.SessionSettings.EnableOxygenPressurization) {
                    helmet = CheckHelmetNeeded(character);
                    State = State.None;
                } else {
                    State = State.CheckOxygenAfterRespawn;
                }

                var data = new SuitData(dampeners, thruster, helmet, linearVelocity, angularVelocity);
                SetSuitFunctions(character, data);
            }
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
                var helmet = CheckHelmetNeeded(character);

                bool thruster;
                bool dampeners;

                var naturalGravity = cockpit.GetNaturalGravity();
                var gravity = cockpit.GetTotalGravity();

                var isGravityDetected = gravity.Length() > 0;
                var isArtificial = !(naturalGravity.Length() > 0);
                var isGroundInRange = IsGroundInRange(character, gravity);
                var isNotMoving = Math.Abs(linearVelocity.Length()) < SPEED_TOLERANCE && Math.Abs(angularVelocity.Length()) < SPEED_TOLERANCE;

                if (isGravityDetected) {
                    if (isGroundInRange) {
                        thruster = false;
                        dampeners = isNotMoving || !isArtificial;
                    } else {
                        thruster = true;
                        dampeners = isNotMoving || !isArtificial;
                    }
                } else {
                    thruster = true;
                    dampeners = isNotMoving;
                }

                _dataFromLastCockpit = new SuitData(dampeners, thruster, helmet, linearVelocity, angularVelocity);

                State = State.ExitCockpit;
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