using PreferenceSystem;

namespace FirstPersonView
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
        internal static void RegisterPreferences(string modID, string modName)
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
                    ["Third person (default)", "First person"]);

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
        /// <summary>
        /// Determines whether first person mode is enabled or disabled.
        /// </summary>
        /// <returns>The state of first person mode.</returns>
        internal static bool GetFirstPersonStateSetting()
        {
            return PrefManager.Get<bool>(PreferenceIdFirstPersonViewState);
        }

        /// <summary>
        /// Sets the state of first person mode to the given state.
        /// </summary>
        /// <param name="IntendedState">The camera state intended.</param>
        internal static void SetFirstPersonStateSetting(CameraState IntendedState)
        {
            PrefManager.Set<bool>(PreferenceIdFirstPersonViewState, IntendedState == CameraState.FirstPerson ? true : false);
        }
        #endregion

        #region Field of view
        /// <summary>
        /// Returns the current field of view in degrees.
        /// </summary>
        /// <returns>Integer value containing field of view in degrees.</returns>
        internal static int GetFieldOfViewSetting()
        {
            return PrefManager.Get<int>(PreferenceIdFieldOfView);
        }
        #endregion

        #region Look sensitivity
        /// <summary>
        /// Returns the current look sensitivity on a scale between 1 and 10.
        /// </summary>
        /// <returns>Float value containing current look sensitivity.</returns>
        internal static float GetLookSensitivitySetting()
        {
            return PrefManager.Get<float>(PreferenceIdLookSensitivity);
        }
        #endregion

        #region Body visibility state
        /// <summary>
        /// Returns whether the player model is visible in first person or not.
        /// </summary>
        /// <returns>Whether the player model is visible in first person or not.</returns>
        internal static BodyState GetBodyVisibilitySetting()
        {
            return PrefManager.Get<bool>(PreferenceIdBodyVisibility) ? BodyState.Displayed : BodyState.Hidden;
        }

        /// <summary>
        /// Sets whether you should be able to see the player model while in first person.
        /// </summary>
        /// <param name="IntendedState">The intended BodyState</param>
        internal static void SetBodyVisibilitySetting(BodyState IntendedState)
        {
            PrefManager.Set<bool>(PreferenceIdBodyVisibility, IntendedState == BodyState.Displayed ? true : false);
        }
        #endregion

        /// <summary>
        /// Returns whether debug mode is enabled.
        /// </summary>
        /// <returns>Boolean value on if debug is enabled (true) or disabled (false).</returns>
        internal static bool GetDebugSetting()
        {
            return PrefManager.Get<bool>(PreferenceIdIsDebugEnabled);
        }
    }
}
