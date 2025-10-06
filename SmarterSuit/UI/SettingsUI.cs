using RichHudFramework.Client;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using Sisk.SmarterSuit.Data;
using System;
using System.Linq;

namespace Sisk.SmarterSuit.UI {

    internal class SettingsUI {

        public void Draw() {
            if (RichHudClient.Registered) { }
        }

        public void Init(string modName) {
            RichHudClient.Init(modName, HudInit, ClientReset);
        }

        private void ClientReset() { }

        private ControlCategory GetAlignToGravitySettings() {
            var gravity = new ControlTile() {
                new TerminalCheckbox() {
                    Name = "Align to gravity",
                    Value = Mod.Static.Settings.AlignToGravity,
                    CustomValueGetter = () => Mod.Static.Settings.AlignToGravity,
                    ControlChangedHandler = (sender, args) => Mod.Static.SetOption(Option.AlignToGravity, (sender as TerminalCheckbox).Value, false),
                    ToolTip = new RichText(ToolTip.DefaultText) {
                        "Will align the player to the gravity."
                    },
                },

                new TerminalSlider() {
                    Name = "Delay",
                    CustomValueGetter = () => (float)Math.Round(Mod.Static.Settings.AlignToGravityDelay * 16f / 1000f, 0),
                    ControlChangedHandler = (sender, args) => {
                        var slider = sender as TerminalSlider;
                        var value = Math.Round(slider.Value, 0);
                        Mod.Static.SetOption(Option.AlignToGravityDelay, (int)Math.Round(value * 1000f / 16f, 0), false);
                        slider.ValueText = $"{value} s";
                    },
                    ToolTip = new RichText(ToolTip.DefaultText) {
                        "This will delay the align to gravity check for x ticks after the player toggled his helmet manualy."
                    },
                    Min = 0,
                    Max = 30,
                    ValueText = $"{Math.Round(Mod.Static.Settings.AlignToGravityDelay * 16f / 1000f, 0)} s",
                }
            };

            return new ControlCategory() {
                HeaderText = "Align to gravity",
                SubheaderText = "Configure align to gravity behavior",
                TileContainer = {
                    gravity
                },
            };
        }

        private ControlCategory GetBroadcastSettings() {
            var broadcast = new ControlTile() {
                new TerminalCheckbox() {
                    Name = "Remember broadcast",
                    Value = Mod.Static.Settings.RememberBroadcast,
                    CustomValueGetter = () => Mod.Static.Settings.RememberBroadcast,
                    ControlChangedHandler = (sender, args) => Mod.Static.SetOption(Option.RememberBroadcast, (sender as TerminalCheckbox).Value, false),
                    ToolTip = new RichText(ToolTip.DefaultText) {
                        "Will remember the last broadcast state."
                    },
                }
            };

            return new ControlCategory() {
                HeaderText = "Broadcast",
                SubheaderText = "Configure broadcast behavior",
                TileContainer = {
                    broadcast
                },
            };
        }

        private ControlCategory GetDampenderSettings() {
            var dropdown = new TerminalDropdown<DisableAutoDampenerOption>() {
                Name = "Disable auto dampener",
                ControlChangedHandler = (sender, args) => {
                    var element = sender as TerminalDropdown<DisableAutoDampenerOption>;
                    Mod.Static.SetOption(Option.DisableAutoDampener, element.Value.AssocObject, false);
                },
                ToolTip = new RichText(ToolTip.DefaultText) {
                    "Will disable the auto dampener."
                },
            };

            foreach (var preset in Enum.GetValues(typeof(DisableAutoDampenerOption)).Cast<DisableAutoDampenerOption>()) {
                dropdown.List.Add(preset.ToString(), preset);
            }

            dropdown.Value = dropdown.List.FirstOrDefault(x => x.AssocObject == Mod.Static.Settings.DisableAutoDampener);
            dropdown.CustomValueGetter = () => dropdown.List.FirstOrDefault(x => x.AssocObject == Mod.Static.Settings.DisableAutoDampener);

            var dampener = new ControlTile() {
                dropdown,
                new TerminalSlider() {
                    Name = "Halted speed tolerance",
                    Value = Mod.Static.Settings.HaltedSpeedTolerance,
                    CustomValueGetter = () => Mod.Static.Settings.HaltedSpeedTolerance,
                    ControlChangedHandler = (sender, args) => {
                        var slider = sender as TerminalSlider;
                        var value = (float)Math.Round(slider.Value, 2);
                        Mod.Static.SetOption(Option.HaltedSpeedTolerance, value, false);
                        slider.ValueText = $"{value} %";
                    },
                    ToolTip = new RichText(ToolTip.DefaultText) {
                        "The tolerance for the auto dampener."
                    },
                    Min = 0,
                    Max = 2,
                    ValueText = $"{Mod.Static.Settings.HaltedSpeedTolerance} m/s",
                }
            };

            return new ControlCategory() {
                HeaderText = "Dampener",
                SubheaderText = "Configure dampener behavior",
                TileContainer = {
                    dampener
                },
            };
        }

        private ControlCategory GetFuelSettings() {
            var fuel = new ControlTile() {
                new TerminalCheckbox() {
                    Name = "Additional fuel warning",
                    Value = Mod.Static.Settings.AdditionalFuelWarning,
                    CustomValueGetter = () => Mod.Static.Settings.AdditionalFuelWarning,
                    ControlChangedHandler = (sender, args) => Mod.Static.SetOption(Option.AdditionalFuelWarning, (sender as TerminalCheckbox).Value, false),
                    ToolTip = new RichText(ToolTip.DefaultText) {
                        "Will show a warning if the fuel is below the threshold."
                    },
                },

                new TerminalSlider() {
                    Name = "Fuel threshold",
                    Value = Mod.Static.Settings.FuelThreshold,
                    CustomValueGetter = () => Mod.Static.Settings.FuelThreshold,
                    ControlChangedHandler = (sender, args) => {
                        var slider = sender as TerminalSlider;
                        var value = (float)Math.Round(slider.Value, 2);
                        Mod.Static.SetOption(Option.FuelThreshold, value, false);
                        slider.ValueText = $"{value} %";
                    },
                    ToolTip = new RichText(ToolTip.DefaultText) {
                        "The threshold for the fuel warning."
                    },
                    Min = 0,
                    Max = 1,
                    ValueText = $"{Mod.Static.Settings.FuelThreshold} %",
                }
            };

            return new ControlCategory() {
                HeaderText = "Fuel",
                SubheaderText = "Configure fuel warning behavior",
                TileContainer = {
                    fuel
                },
            };
        }

        private ControlCategory GetHelmetSettings() {
            var helmet = new ControlTile() {
                new TerminalCheckbox() {
                    Name = "Automatic toggle helmet",
                    Value = Mod.Static.Settings.AlwaysAutoHelmet,
                    CustomValueGetter = () => Mod.Static.Settings.AlwaysAutoHelmet,
                    ControlChangedHandler = (sender, args) => Mod.Static.SetOption(Option.AlwaysAutoHelmet, (sender as TerminalCheckbox).Value, false),
                    ToolTip = new RichText(ToolTip.DefaultText) {
                        "Will check every time if helmet is needed."
                    },
                },

                new TerminalSlider() {
                    Name = "Delay",
                    CustomValueGetter = () =>(float) Math.Round(Mod.Static.Settings.DelayAfterManualHelmet * 16f / 1000f, 0),
                    ControlChangedHandler = (sender, args) => {
                        var slider = sender as TerminalSlider;
                        var value = Math.Round(slider.Value, 0);
                        Mod.Static.SetOption(Option.DelayAfterManualHelmet, (int)(value * 1000f / 16f), false);
                        slider.ValueText = $"{value} s";
                    },
                    ToolTip = new RichText(ToolTip.DefaultText) {
                        "This will delay the auto helmet check for x ticks after the player toggled his helmet manualy."
                    },
                    Min = 0,
                    Max = 30,
                    ValueText = $"{Math.Round(Mod.Static.Settings.DelayAfterManualHelmet * 16f / 1000f, 0)} s",
                }
            };

            var light = new ControlTile() {
                new TerminalCheckbox() {
                    Name = "Switch helmet light",
                    Value = Mod.Static.Settings.SwitchHelmetLight,
                    CustomValueGetter = () => Mod.Static.Settings.SwitchHelmetLight,
                    ControlChangedHandler = (sender, args) => Mod.Static.SetOption(Option.SwitchHelmetLight, (sender as TerminalCheckbox).Value, false),
                    ToolTip = new RichText(ToolTip.DefaultText) {
                        "Will toggle the helmet light when toggling the helmet."
                    },
                },
                new TerminalCheckbox() {
                    Name = "Turn lights back on",
                    Value = Mod.Static.Settings.TurnLightsBackOn,
                    CustomValueGetter = () => Mod.Static.Settings.TurnLightsBackOn,
                    ControlChangedHandler = (sender, args) => Mod.Static.SetOption(Option.TurnLightsBackOn, (sender as TerminalCheckbox).Value, false),
                    ToolTip = new RichText(ToolTip.DefaultText) {
                        "Will turn the helmet light back on if it was on before toggling the helmet."
                    },
                }
            };

            return new ControlCategory() {
                HeaderText = "Helmet",
                SubheaderText = "Configure helmet behavior",
                TileContainer = {
                    helmet,
                    light
                },
            };
        }

        private void HudInit() {
            RichHudTerminal.Root.Enabled = true;

            RichHudTerminal.Root.AddRange(new IModRootMember[] {
                new ControlPage() {
                    Name = "Settings",
                    CategoryContainer = {
                        GetHelmetSettings(),
                        GetFuelSettings(),
                        GetDampenderSettings(),
                        GetAlignToGravitySettings(),
                        GetBroadcastSettings()
                    },
                },
           });
        }
    }
}