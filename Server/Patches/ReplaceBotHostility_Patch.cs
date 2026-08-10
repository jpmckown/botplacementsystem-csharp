using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;

namespace BotPlacementSystemServer.Patches;

using SPTarkov.Server.Core.Services.Server;

public class ReplaceBotHostilityPatch: AbstractPatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(SeasonalEventService),"ReplaceBotHostility");
    }

    [PatchPrefix]
    public static bool Prefix()
    {
        return false;
    }
}