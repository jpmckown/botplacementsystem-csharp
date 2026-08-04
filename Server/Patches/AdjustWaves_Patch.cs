using System.Reflection;
using BotPlacementSystemServer.Controllers;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Location;
using SPTarkov.Server.Core.Services.InRaid;

namespace BotPlacementSystemServer.Patches;

public class AdjustWaves_Patch : AbstractPatch
{
    // Assigned by PatchManager during OnLoad - 4.1 removed ServiceLocator
    public static PmcSpawns PmcSpawns;
    public static ScavSpawns ScavSpawns;

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(RaidTimeAdjustmentService),"AdjustWaves");
    }

    [PatchPrefix]
    public static bool Prefix(LocationBase mapBase, RaidChanges raidAdjustments)
    {
        var locationName = mapBase.Id.ToLowerInvariant();

        var simulatedStart = raidAdjustments.SimulatedRaidStartSeconds ?? 0d;
        var totalRaidSeconds = (raidAdjustments.RaidTimeMinutes ?? 0d) * 60;

        if (simulatedStart > 60d)
        {
            var mapBosses = mapBase.BossLocationSpawn
                .Where(x => x.Time == -1
                            && !string.Equals(x.BossName, "pmcUSEC", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(x.BossName, "pmcBEAR", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var newPmcs = PmcSpawns.GenerateScavRaidRemainingPmcs(locationName, totalRaidSeconds);
            mapBase.BossLocationSpawn = newPmcs;

            foreach (var boss in mapBosses)
                mapBase.BossLocationSpawn.Add(boss);

            mapBase.Waves = ScavSpawns.GetLateStartMapData(locationName, totalRaidSeconds);
        }

        return false;
    }
}
