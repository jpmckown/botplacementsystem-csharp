using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BotPlacementSystemClient.Patches;

internal class AssaultGroupPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(ProfileSettings), nameof(ProfileSettings.TryChangeRoleToAssaultGroup));
    }

    [PatchPrefix]
    private static bool PatchPrefix(ProfileSettings __instance)
    {
        if(__instance.Role == WildSpawnType.assaultGroup)
        {
            __instance.Role = WildSpawnType.assault;
        }
        return false;
    }
}