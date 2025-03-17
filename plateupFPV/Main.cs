using Controllers;
using Kitchen;
using KitchenLib;
using KitchenMods;
using PreferenceSystem;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/*
 * TODO:
 * - Figure why crane toggles again (object reference error in log).
 * - fix above.  Priority.
 * 
 *
 */

namespace KitchenFirstPersonView
{
    public class Main : BaseMod, IModSystem
    {
        internal const string modID = "com.allegedlyluca.plateupfpv";
        private const string modName = "First Person View";
        private const string author = "AllegedlyLuca";
        private const string modVersion = "0.1.0";
        private const string compatibleVersions = ">=1.1.4";

        #region PreferenceSystem object and Preference identifiers
        internal static PreferenceSystemManager PrefManager;
        internal const string PreferenceIdFirstPersonViewEnabled = "IsFPVEnabled";
        internal const string PreferenceIdFieldOfView = "PlayerFieldOfView";
        internal const string PreferenceIdLookSensitivity = "LookSensitivity";
        internal const string PreferenceIdIsPlayerModelVisible = "IsPlayerModelVisible";
        internal const string PreferenceIdIsDebugEnabled = "IsDebugEnabled";
        #endregion

        internal static int PlayerSource = 0;
        internal static SourceIdentifier ThisIsMyController;

        internal static Camera FirstPersonCameraObject = null;
        internal static GameObject FirstPersonPlayerGameObject = null;

        internal static int PlayerID = 0;
        internal static string PlayerUsername = null;
        internal static string PlayerUsernameIDString = null;
        internal static bool IsPlayerCrane = false;
        internal static IDictionary<PlayerInfo, int> LocalPlayers = new Dictionary<PlayerInfo, int>();

        public static AssetBundle Bundle;

        public Main() : base(modID, modName, author, modVersion, compatibleVersions, Assembly.GetExecutingAssembly()) { }

        protected override void OnInitialise()
        {
            ManageControls.SetInitialInputSource();
            FPVLogger.Info("Initialisation complete!");
            foreach(PlayerInfo player in Players.Main.All())
            {
                FPVLogger.Debug("Player initialised: " + player.Username);
                FPVLogger.Debug("Player ID: " + player.ID);
            }
        }

        protected override void OnUpdate()
        {
        }

        protected override void OnPostActivate(KitchenMods.Mod mod)
        {
            new FPVLogger(InitLogger());
            RegisterPreferences();
        }

        /// <summary>
        /// Registers the mod's preferences with PreferenceSystem.
        /// </summary>
        private void RegisterPreferences()
        {
            FPVLogger.Info("Registering preferences.");
            PrefManager = new PreferenceSystemManager(modID, modName);

            PrefManager
                .AddLabel("Player camera perspective")
                .AddInfo("Toggleable via F5 on keyboard only!")
                .AddOption<bool>(PreferenceIdFirstPersonViewEnabled, false,
                    [false, true],
                    ["Third-person (default)", "First-person"]);

            PrefManager
                .AddLabel("Field of View")
                .AddInfo("How wide or narrow the camera perspective is.")
                .AddOption<int>(PreferenceIdFieldOfView, 60,
                    [30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100, 105, 110],
                    ["30", "35", "40", "45", "50", "55", "60 (default)", "65", "70", "75", "80", "85", "90", "95", "100", "105", "110"]);

            PrefManager
                .AddLabel("Look sensitivity")
                .AddOption<float>(PreferenceIdLookSensitivity, 5f,
                    [1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f, 5.5f, 6f, 6.5f, 7f, 7.5f, 8f, 8.5f, 9f],
                    ["1", "1.5", "2", "2.5", "3", "3.5", "4", "4.5", "5 (default)", "5.5", "6", "6.5", "7", "7.5", "8", "8.5", "9"]);

            PrefManager
                .AddLabel("Show player model in first person")
                .AddOption<bool>(PreferenceIdIsPlayerModelVisible, false,
                    [false, true],
                    ["No", "Yes"]);

            PrefManager
                .AddLabel("Log debug data")
                .AddInfo("This generates a lot of log file entries.  Only enable it if you need to.")
                .AddOption<bool>(PreferenceIdIsDebugEnabled, false,
                    [false, true],
                    ["No", "Yes"]);

            PrefManager.RegisterMenu(PreferenceSystemManager.MenuType.PauseMenu);

            FPVLogger.Info("Preference registration complete.");
        }

        internal static bool IsFirstPersonViewEnabled()
        {
            return PrefManager.Get<bool>(PreferenceIdFirstPersonViewEnabled);
        }

        internal static void SetFirstPersonStateInPreferences(CameraState IntendedState)
        {
            if(IntendedState == CameraState.FirstPerson)
            {
                PrefManager.Set<bool>(PreferenceIdFirstPersonViewEnabled, true);
                return;
            }
            PrefManager.Set<bool>(PreferenceIdFirstPersonViewEnabled, false);
        }

        internal static bool IsDebugEnabled()
        {
            return PrefManager.Get<bool>(PreferenceIdIsDebugEnabled);
        }
    }
}
