using System.Collections.Generic;
using VRage;

namespace Sisk.SmarterSuit.Localization {
    public static class Lang {
        public static IDictionary<string, string> de => new Dictionary<string, string> {
            { "Description_SS_Enable", "[option] Aktiviert eine Option" },
            { "Description_SS_Disable", "[option] Deaktiviert eine Option" },
            { "Description_SS_List", "Listet alle Optionen auf" },
            { "Description_SS_Help", "Zeigt eine Hilfeseite an" },
            { "SS_NoPermissionError", "Sie haben keine Berechtigung, diese Option festzulegen." },
            { "SS_UnknownOptionError", "Unbekannte Option '{0}'." },
            { "SS_OnlyBooleanAllowedError", "Nur 'Boolean' Optionen können benutzt werden." },
            { "Description_SS_Set", "[option] [value] Legt eine Option auf den angegebenen Value fest." },
            { "SS_ConvertError", "Konnte '{0}' nicht in {1} konvertieren." },
            { "SS_ArgumentError", "Falsche Argumente. Erwartet [option] [value] Argumente." },
            { "SS_SetOptionSuccess", "{0} erfolgreich auf {1} festgelegt." },
            { "SS_SetOptionError", "Fehler beim Festlegen von {0} auf {1}." }
        };

        public static IDictionary<string, string> en => new Dictionary<string, string> {
            { "Description_SS_Enable", "[option] Enables an option" },
            { "Description_SS_Disable", "[option] Disables an option" },
            { "Description_SS_List", "Lists all options" },
            { "Description_SS_Help", "Shows a help page" },
            { "SS_NoPermissionError", "You do not have permission to set this option." },
            { "SS_UnknownOptionError", "Unknown option '{0}'." },
            { "SS_OnlyBooleanAllowedError", "Only Boolean options can be used." },
            { "Description_SS_Set", "[option] [value] Set an option to value." },
            { "SS_ConvertError", "Could not convert '{0}' to {1}." },
            { "SS_ArgumentError", "Wrong arguments. Expect [option] [value] arguments." },
            { "SS_SetOptionSuccess", "{0} successfully set to {1}." },
            { "SS_SetOptionError", "Failed to set {0} to {1}." }
        };

        public static bool Contains(MyLanguagesEnum language) {
            switch (language) {
                case MyLanguagesEnum.English:
                case MyLanguagesEnum.German:
                    return true;
                case MyLanguagesEnum.Czech:
                case MyLanguagesEnum.Slovak:
                case MyLanguagesEnum.Russian:
                case MyLanguagesEnum.Spanish_Spain:
                case MyLanguagesEnum.French:
                case MyLanguagesEnum.Italian:
                case MyLanguagesEnum.Danish:
                case MyLanguagesEnum.Dutch:
                case MyLanguagesEnum.Icelandic:
                case MyLanguagesEnum.Polish:
                case MyLanguagesEnum.Finnish:
                case MyLanguagesEnum.Hungarian:
                case MyLanguagesEnum.Portuguese_Brazil:
                case MyLanguagesEnum.Estonian:
                case MyLanguagesEnum.Norwegian:
                case MyLanguagesEnum.Spanish_HispanicAmerica:
                case MyLanguagesEnum.Swedish:
                case MyLanguagesEnum.Catalan:
                case MyLanguagesEnum.Croatian:
                case MyLanguagesEnum.Romanian:
                case MyLanguagesEnum.Ukrainian:
                case MyLanguagesEnum.Turkish:
                case MyLanguagesEnum.Latvian:
                case MyLanguagesEnum.ChineseChina:
                    return false;
            }

            return false;
        }
    }
}