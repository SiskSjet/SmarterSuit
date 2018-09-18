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
        private State State { get; set; }

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

        private void OnCharacterChanged(IMyCharacter oldCharacter, IMyCharacter newCharacter) {
            var respawn = oldCharacter != newCharacter;

            UnRegisterEvents(oldCharacter);
            RegisterEvents(newCharacter);

            if (respawn) {
                State = State.Respawn;
            }
        }

        private void OnIdentityChanged(IMyPlayer player, IMyIdentity identity) {
            _identity.CharacterChanged -= OnCharacterChanged;

            _identity = identity;
            _identity.CharacterChanged += OnCharacterChanged;
        }

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

        private void RegisterEvents(IMyCharacter character) {
            character.MovementStateChanged += OnMovementStateChanged;
        }

        private void UnRegisterEvents(IMyCharacter character) {
            character.MovementStateChanged -= OnMovementStateChanged;
        }
    }
}