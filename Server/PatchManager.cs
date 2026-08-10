using BotPlacementSystemServer.Patches;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace BotPlacementSystemServer;

using SPTarkov.Reflection.Patching;

[Injectable(TypePriority = OnLoadOrder.Preload)]
public class PatchManager(IEnumerable<IRuntimePatch> patches) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken token)
    {
        foreach (var patch in patches)
        {
            patch.Enable();
        }
        
        return Task.CompletedTask;
    }
}