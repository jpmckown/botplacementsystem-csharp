using BotPlacementSystemServer.Controllers;
using BotPlacementSystemServer.Patches;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace BotPlacementSystemServer;

[Injectable(TypePriority = OnLoadOrder.Preload + 50)]
public class PatchManager(PmcSpawns pmcSpawns, ScavSpawns scavSpawns) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        // 4.1 dropped ServiceLocator, so hand the patches their dependencies here instead
        AdjustWaves_Patch.PmcSpawns = pmcSpawns;
        AdjustWaves_Patch.ScavSpawns = scavSpawns;

        new AdjustWaves_Patch().Enable();
        new AdjustPmcSpawns_Patch().Enable();
        new ReplaceBotHostility_Patch().Enable();

        return Task.CompletedTask;
    }
}
