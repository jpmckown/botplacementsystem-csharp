using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;

namespace BotPlacementSystemServer.Patches;

using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Services.InRaid;

[Injectable]
public class AdjustPmcSpawnsPatch: AbstractPatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(RaidTimeAdjustmentService),"AdjustPMCSpawns");
    }

    [PatchPrefix]
    public static bool Prefix()
    {
        return false;
    }
}