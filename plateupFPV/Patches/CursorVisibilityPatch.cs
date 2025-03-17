using HarmonyLib;

namespace FirstPersonView.Patches
{
    [HarmonyPatch(typeof(Controllers.MouseUI))]
    [HarmonyPatch("UpdateMouseVisibility")]
    class CursorVisibilityPatch
    {
        static bool Prefix()
        {
            return false;
        }
    }
}
