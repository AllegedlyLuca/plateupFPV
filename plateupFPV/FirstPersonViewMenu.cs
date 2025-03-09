using Kitchen;
using Kitchen.Modules;
using KitchenLib;
using KitchenLib.Preferences;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KitchenFirstPersonView
{
    public class FirstPersonViewMenu<T> : KLMenu<T>
    {
        public FirstPersonViewMenu(Transform container, ModuleList moduleList) : base(container, moduleList)
        {
            Setup(0);
        }

        public override void Setup(int player_id)
        {
            FPVLogger.DebugLog("Creating in-game configuration menu.");
            AddButton("Toggle First Person", delegate (int i)
            {
                FirstPersonPlayerView.UpdateView.CreatePlayerToToggleSingleton(player_id);
            }, 0, 1f, 0.2f);
            AddLabel("First Person Camera Options");

            AddSelect<int>(FPVEnabledOption);
            FPVEnabledOption.OnChanged += delegate (object _, int result)
            {
                PreferenceInt preferenceInt = Main.PrefManager.GetPreference<PreferenceInt>(Main.FPV_ENABLED_ID);
                preferenceInt.Set(result);
                Main.PrefManager.Save();
            };

            New<SpacerElement>();

            AddLabel("Sensitivity");
            AddSelect<float>(SensitivityOption);
            SensitivityOption.OnChanged += delegate (object _, float result)
            {
                PreferenceFloat preferenceFloat = Main.PrefManager.GetPreference<PreferenceFloat>(Main.SENSITIVITY_ID);
                preferenceFloat.Set(result);
                Main.PrefManager.Save();
            };

            New<SpacerElement>();

            AddLabel("Field Of View");
            AddSelect<int>(FOVOption);
            FOVOption.OnChanged += delegate (object _, int result)
            {
                PreferenceInt preferenceInt = Main.PrefManager.GetPreference<PreferenceInt>(Main.FOV_ID);
                preferenceInt.Set(result);
                Main.PrefManager.Save();
            };

            New<SpacerElement>();

            AddLabel("Player Model Visibility");
            AddSelect<int>(VisiblePlayerModelOption);
            VisiblePlayerModelOption.OnChanged += delegate (object _, int result)
            {
                PreferenceInt preferenceInt = Main.PrefManager.GetPreference<PreferenceInt>(Main.PLAYER_MODEL_VISIBLE_ID);
                preferenceInt.Set(result);
                Main.PrefManager.Save();
            };

            New<SpacerElement>(true);
            New<SpacerElement>(true);
            AddButton(base.Localisation["MENU_BACK_SETTINGS"], delegate (int i)
            {
                this.RequestPreviousMenu();
            }, 0, 1f, 0.2f);
        }

        private Option<float> SensitivityOption = new Option<float>(
            new List<float> { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f, 5.5f, 6f, 6.5f, 7f, 7.5f, 8f, 8.5f, 9f }, 
            (float)Main.PrefManager.Get<PreferenceFloat>(Main.SENSITIVITY_ID), 
            new List<string> { "1", "1.5", "2", "2.5", "3", "3.5", "4", "4.5", "5", "5.5", "6", "6.5", "7", "7.5", "8", "8.5", "9" }
            );

        private Option<int> FOVOption = new Option<int>(
            new List<int> { 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100, 105, 110 },
            (int)Main.PrefManager.Get<PreferenceInt>(Main.FOV_ID),
            new List<string> { "30", "35", "40", "45", "50", "55", "60", "65", "70", "75", "80", "85", "90", "95", "100", "105", "110" }
            );

        private Option<int> VisiblePlayerModelOption = new Option<int>(
            new List<int> { 0, 1 },
            (int)Main.PrefManager.Get<PreferenceInt>(Main.PLAYER_MODEL_VISIBLE_ID),
            new List<string> { "Disabled", "Enabled" }
            );

        private Option<int> FPVEnabledOption = new Option<int>(
            new List<int> { 0, 1 },
            (int)Main.PrefManager.Get<PreferenceInt>(Main.FPV_ENABLED_ID),
            new List<string> { "Disabled", "Enabled" }
            );
    }
}
