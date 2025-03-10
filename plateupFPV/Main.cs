using Kitchen;
using KitchenLib;
using KitchenLib.Event;
using KitchenLib.Logging;
using KitchenLib.Logging.Exceptions;
using KitchenLib.Preferences;
using KitchenMods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking.Types;

namespace KitchenFirstPersonView
{
    public class Main : BaseMod, IModSystem
    {
        internal const string modID = "com.allegedlyluca.plateupfpv";
        private const string modName = "First Person View";
        private const string author = "AllegedlyLuca";
        private const string modVersion = "0.0.1";
        private const string compatibleVersions = ">=1.1.4";

        #region Preferences
        public const string FOV_ID = "fov";
        public const string SENSITIVITY_ID = "sensitivity";
        public const string PLAYER_MODEL_VISIBLE_ID = "playerModelVisible";
        public const string FPV_ENABLED_ID = "firstPersonCamera";
        #endregion

        public static Dictionary<string, int> DefaultValuesDict;
        internal static PreferenceManager PrefManager;
        internal static PreferenceFloat SensitivityPreference = new PreferenceFloat(SENSITIVITY_ID, 5.0f);
        internal static PreferenceInt FOVPreference = new PreferenceInt(FOV_ID, 65);
        internal static PreferenceInt PlayerModelVisibilityPreference = new PreferenceInt(PLAYER_MODEL_VISIBLE_ID, 0);
        internal static PreferenceInt FPVEnabledPreference = new PreferenceInt(FPV_ENABLED_ID, 0);

        public static bool IsLoaded = false;
        internal static int FPVCounter = 0;

#if DEBUG
        public const bool DEBUG_MODE = true;
#else
        public const bool DEBUG_MODE = false;
#endif

        public static AssetBundle Bundle;

        public Main() : base(modID, modName, author, modVersion, compatibleVersions, Assembly.GetExecutingAssembly()) { }

        protected override void OnInitialise()
        {
            FPVLogger.Log(0, "Initialisation complete!");
        }

        protected override void OnUpdate()
        {
        }

        protected override void OnPostActivate(KitchenMods.Mod mod)
        {
            if (IsLoaded)
                return;

            new FPVLogger(InitLogger());

            FPVLogger.Log("Registering preferences.");
            Bundle = mod.GetPacks<AssetBundleModPack>().SelectMany(e => e.AssetBundles).First() ?? throw new MissingAssetBundleException(modID);
            RegisterPreferences();
            FPVLogger.Log("Preference registration complete.");
            IsLoaded = true;
        }

        private void RegisterPreferences()
        {
            PrefManager = new PreferenceManager(modID);

            PrefManager.RegisterPreference(SensitivityPreference);
            PrefManager.RegisterPreference(FOVPreference);
            PrefManager.RegisterPreference(PlayerModelVisibilityPreference);
            PrefManager.RegisterPreference(FPVEnabledPreference);

            PrefManager.Load();

            ModsPreferencesMenu<PauseMenuAction>.RegisterMenu("First Person View", typeof(FirstPersonViewMenu<PauseMenuAction>), typeof(PauseMenuAction));

            Events.PreferenceMenu_PauseMenu_CreateSubmenusEvent += (s, args) => {
                args.Menus.Add(typeof(FirstPersonViewMenu<PauseMenuAction>), new FirstPersonViewMenu<PauseMenuAction>(args.Container, args.Module_list));
            };
        }
    }
}
