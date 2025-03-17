using KitchenLib.Preferences;
using PreferenceSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KitchenFirstPersonView
{
    internal class PreferenceHandler
    {
        private static bool PreferencesRegistered = false;

        private const string PreferenceIdFirstPersonViewState = "IsFPVEnabled";
        private const string PreferenceIdFieldOfView = "PlayerFieldOfView";
        private const string PreferenceIdLookSensitivity = "LookSensitivity";
        private const string PreferenceIdBodyVisibility = "IsPlayerModelVisible";
        private const string PreferenceIdIsDebugEnabled = "IsDebugEnabled";

        private static PreferenceSystemManager PrefManager;
        
        /// <summary>
        /// Registers the mod's preferences with PreferenceSystem.
        /// </summary>
        public static void RegisterPreferences(string modID, string modName)
        {
            if(PreferencesRegistered)
            {
                return;
            }

            FPVLogger.Info("Registering preferences.");
            PrefManager = new PreferenceSystemManager(modID, modName);

            PrefManager
                .AddLabel("Player camera perspective")
                .AddInfo("Toggleable via F5 on keyboard only!")
                .AddOption<bool>(PreferenceIdFirstPersonViewState, false,
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
                .AddOption<bool>(PreferenceIdBodyVisibility, false,
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
            PreferencesRegistered = true;
        }

        #region First person state
        internal static bool GetFirstPersonStateSetting()
        {
            return PrefManager.Get<bool>(PreferenceIdFirstPersonViewState);
        }

        internal static void SetFirstPersonStateSetting(CameraState IntendedState)
        {
            PrefManager.Set<bool>(PreferenceIdFirstPersonViewState, IntendedState == CameraState.FirstPerson ? true : false);
        }
        #endregion

        #region Field of view
        internal static int GetFieldOfViewSetting()
        {
            return PrefManager.Get<int>(PreferenceIdFieldOfView);
        }
        #endregion

        #region Look sensitivity
        internal static float GetLookSensitivitySetting()
        {
            return PrefManager.Get<float>(PreferenceIdLookSensitivity);
        }
        #endregion

        #region Body visibility state
        internal static BodyState GetBodyVisibilitySetting()
        {
            return PrefManager.Get<bool>(PreferenceIdBodyVisibility) ? BodyState.Displayed : BodyState.Hidden;
        }

        internal static void SetBodyVisibilitySetting(BodyState IntendedState)
        {
            PrefManager.Set<bool>(PreferenceIdBodyVisibility, IntendedState == BodyState.Displayed ? true : false);
        }
        #endregion

        internal static bool IsDebugEnabledSetting()
        {
            return PrefManager.Get<bool>(PreferenceIdIsDebugEnabled);
        }
    }
}
