using Controllers;
using Kitchen;
using KitchenLib;
using KitchenMods;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FirstPersonView
{
    public class Main : BaseMod, IModSystem
    {
        internal const string modID = "com.allegedlyluca.plateupfpv";
        private const string modName = "First Person View";
        private const string author = "AllegedlyLuca";
        private static readonly string modVersion = typeof(FirstPersonView.Main).Assembly.GetName().Version.ToString();
        // compatibleVersions refers to the version of PlateUp! the mod is intended to work with!
        private const string compatibleVersions = ">=1.1.4";

        internal static int PlayerSource = 0;
        internal static SourceIdentifier ThisIsMyController;

        internal static Camera FirstPersonCameraObject = null;
        internal static GameObject PlayerGameObject = null;

        internal static int PlayerID = 0;
        internal static string PlayerUsername = null;
        internal static string PlayerUsernameIDString = null;
        internal static IDictionary<PlayerInfo, int> LocalPlayers = new Dictionary<PlayerInfo, int>();

        internal static Material OriginalSkybox;

        public static AssetBundle Bundle;

        public Main() : base(modID, modName, author, modVersion, compatibleVersions, Assembly.GetExecutingAssembly()) { }

        protected override void OnInitialise()
        {
            ManageControls.SetInitialInputSource();
            FPVLogger.Info("Initialisation complete for First Player View v"+ modVersion +"!");
        }

        protected override void OnUpdate()
        {
        }

        protected override void OnPostActivate(KitchenMods.Mod mod)
        {
            new FPVLogger(InitLogger());
            PreferenceHandler.RegisterPreferences(modID, modName);
        }
    }
}
