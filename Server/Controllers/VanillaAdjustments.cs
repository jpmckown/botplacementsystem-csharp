using BotPlacementSystemServer.Globals;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Utils.Cloners;

namespace BotPlacementSystemServer.Controllers;

using SPTarkov.Server.Core.Models.Spt.Tables;

[Injectable]
public class VanillaAdjustments(
    ICloner cloner,
    LocationConfig locationConfig,
    PmcConfig pmcConfig,
    BotConfig botConfig,
    BotTable botTable)
{

    public void DisableVanillaSettings()
    {
        // LocationConfig.SplitWaveIntoSingleSpawnSettins.Enabled = false;
        locationConfig.RogueLighthouseSpawnTimeSettings.Enabled = false;
        locationConfig.AddOpenZonesToAllMaps = false;
        locationConfig.AddCustomBotWavesToMaps = false;
        locationConfig.EnableBotTypeLimits = false;
    }

    public void DisableNewSpawnSystem(LocationBase locationBase)
    {
        locationBase.NewSpawn = false;
        locationBase.OfflineNewSpawn = false;
        locationBase.OldSpawn = true;
        locationBase.OfflineOldSpawn = true;
    }

    public void DisableOldSpawnSystem(LocationBase locationBase)
    {
        if (locationBase.Id.ToLowerInvariant() == "laboratory" && !ModConfig.Config.ScavConfig.Waves.AllowScavsOnLaboratory || 
            (locationBase.Id.Equals("labyrinth", StringComparison.InvariantCultureIgnoreCase) && !ModConfig.Config.ScavConfig.Waves.AllowScavsOnLabyrinth)) 
            return;
        
        locationBase.NewSpawn = true;
        locationBase.OfflineNewSpawn = true;
        locationBase.OldSpawn = false;
        locationBase.OfflineOldSpawn = false;
    }

    public void EnableAllSpawnSystems(LocationBase locationBase)
    {
        if ((locationBase.Id.Equals("laboratory", StringComparison.InvariantCultureIgnoreCase) && !ModConfig.Config.ScavConfig.Waves.AllowScavsOnLaboratory) || 
            (locationBase.Id.Equals("labyrinth", StringComparison.InvariantCultureIgnoreCase) && !ModConfig.Config.ScavConfig.Waves.AllowScavsOnLabyrinth)) 
            return;
        
        locationBase.NewSpawn = true;
        locationBase.OfflineNewSpawn = true;
        locationBase.OldSpawn = true;
        locationBase.OfflineOldSpawn = true;
        if (locationBase.NonWaveGroupScenario is not null)
        {
            locationBase.NonWaveGroupScenario.Chance = 0;
        }
    }

    public void DisableAllSpawnSystems(LocationBase locationBase)
    {
        locationBase.NewSpawn = false;
        locationBase.OfflineNewSpawn = false;
        locationBase.OldSpawn = false;
        locationBase.OfflineOldSpawn = false;
    }

    public void AdjustNewWaveSettings(LocationBase locationBase)
    {
        if ((locationBase.Id.Equals("laboratory", StringComparison.InvariantCultureIgnoreCase) && !ModConfig.Config.ScavConfig.Waves.AllowScavsOnLaboratory) || 
            (locationBase.Id.Equals("labyrinth", StringComparison.InvariantCultureIgnoreCase) && !ModConfig.Config.ScavConfig.Waves.AllowScavsOnLabyrinth)) 
            return;

        if (ModConfig.Config.ScavConfig.Waves.EnableCustomTimers && (locationBase.Id.Contains("factory") || locationBase.Id.Contains("labyrinth") || locationBase.Id.Contains("laboratory")))
        {
            // Start-Stop Time for spawns
            locationBase.BotStart = ModConfig.Config.ScavConfig.Waves.StartSpawns;
            locationBase.BotStop = (int)locationBase.EscapeTimeLimit * 60 - ModConfig.Config.ScavConfig.Waves.StopSpawns;

            // Start-Stop wave times for active spawning
            locationBase.BotSpawnTimeOnMin = 45;
            locationBase.BotSpawnTimeOnMax = 75;

            // Start-Stop wave wait times between active spawning
            locationBase.BotSpawnTimeOffMin = 120;
            locationBase.BotSpawnTimeOffMax = 240;

            // Probably how often it checks to spawn while active spawning
            locationBase.BotSpawnPeriodCheck = 15;

            // Bot count required to trigger a spawn
            locationBase.BotSpawnCountStep = 1;
        }
        else
        {
            // Start-Stop Time for spawns
            locationBase.BotStart = ModConfig.Config.ScavConfig.Waves.StartSpawns;
            locationBase.BotStop = (int)locationBase.EscapeTimeLimit * 60 - ModConfig.Config.ScavConfig.Waves.StopSpawns;

            // Start-Stop wave times for active spawning
            locationBase.BotSpawnTimeOnMin = ModConfig.Config.ScavConfig.Waves.ActiveTimeMin;
            locationBase.BotSpawnTimeOnMax = ModConfig.Config.ScavConfig.Waves.ActiveTimeMax;

            // Start-Stop wave wait times between active spawning
            locationBase.BotSpawnTimeOffMin = ModConfig.Config.ScavConfig.Waves.QuietTimeMin;
            locationBase.BotSpawnTimeOffMax = ModConfig.Config.ScavConfig.Waves.QuietTimeMax;

            // Probably how often it checks to spawn while active spawning
            locationBase.BotSpawnPeriodCheck = ModConfig.Config.ScavConfig.Waves.CheckToSpawnTimer;

            // Bot count required to trigger a spawn
            locationBase.BotSpawnCountStep = ModConfig.Config.ScavConfig.Waves.PendingBotsToTrigger;
        }
        
        locationBase.BotLocationModifier.NonWaveSpawnBotsLimitPerPlayer = ModConfig.Config.ScavConfig.Waves.NonWaveSpawnBotsLimitPerPlayer;
        locationBase.BotLocationModifier.NonWaveSpawnBotsLimitPerPlayerPvE = ModConfig.Config.ScavConfig.Waves.NonWaveSpawnBotsLimitPerPlayer;
    }

    public void RemoveExistingWaves(LocationBase locationBase)
    {
        locationBase.Waves = [];
    }

    public void CheckAndAddScavBrainTypes()
    {
        if (!botConfig.PlayerScavBrainType.ContainsKey("labyrinth"))
        {
            botConfig.PlayerScavBrainType["labyrinth"] = cloner.Clone(botConfig.PlayerScavBrainType["laboratory"]);
        }
        
        if (!botConfig.AssaultBrainType.ContainsKey("labyrinth"))
        {
            botConfig.AssaultBrainType["labyrinth"] = cloner.Clone(botConfig.AssaultBrainType["laboratory"]);
        }
    }

    public void FixPmcHostility(LocationBase locationBase)
    {
        var hostility = locationBase.BotLocationModifier?.AdditionalHostilitySettings.ToList();
        if (hostility is not null || hostility.Any())
        {
            for (var bot = 0; bot < hostility.Count; bot++)
            {
                if (hostility[bot].BotRole == "pmcUSEC" || hostility[bot].BotRole == "pmcBEAR")
                {
                    var newHostilitySettings = cloner.Clone(ModConfig.HostilityDefaults);
                    newHostilitySettings.BotRole = hostility[bot].BotRole;
                    hostility[bot] = newHostilitySettings;
                }

                // Fix scav hostility settings for every map
                if (hostility[bot].BotRole == "assault" || hostility[bot].BotRole == "marksman")
                {
                    var newHostilitySettings = cloner.Clone(ModConfig.HostilityDefaults);
                    newHostilitySettings.BotRole = hostility[bot].BotRole;
                    foreach (var botType in newHostilitySettings.AlwaysEnemies)
                    {
                        if (botType == "pmcBEAR" || botType == "pmcUSEC") continue;
                        
                        newHostilitySettings.AlwaysFriends.Add(botType);
                        newHostilitySettings.AlwaysEnemies.Remove(botType);
                    }
                    hostility[bot] = newHostilitySettings;
                }
            }
        }

        var databaseBots = botTable.Types;
        foreach (var (bot, data) in databaseBots)
        {
            if (bot.Contains("assault") || bot.Contains("marksman"))
            {
                foreach (var (difficulty, dataSet) in databaseBots[bot].BotDifficulty)
                {
                    if (databaseBots[bot].BotDifficulty[difficulty].Mind.EnemyBotTypes is null)
                    {
                        databaseBots[bot].BotDifficulty[difficulty].Mind.EnemyBotTypes = new List<WildSpawnType>();
                    }
                    databaseBots[bot].BotDifficulty[difficulty].Mind.EnemyBotTypes.Add(WildSpawnType.pmcUSEC);
                    databaseBots[bot].BotDifficulty[difficulty].Mind.EnemyBotTypes.Add(WildSpawnType.pmcBEAR);
                }
            }
        }

        foreach (var (bot, data) in pmcConfig.HostilitySettings)
        {
            if (pmcConfig.HostilitySettings[bot].AdditionalEnemyTypes is not null)
            {
                if (!pmcConfig.HostilitySettings[bot].AdditionalEnemyTypes.Contains("assault")) 
                    pmcConfig.HostilitySettings[bot].AdditionalEnemyTypes.Add("assault");
                
                if (!pmcConfig.HostilitySettings[bot].AdditionalEnemyTypes.Contains("pmcBEAR")) 
                    pmcConfig.HostilitySettings[bot].AdditionalEnemyTypes.Add("pmcBEAR");
                
                if (!pmcConfig.HostilitySettings[bot].AdditionalEnemyTypes.Contains("pmcUSEC")) 
                    pmcConfig.HostilitySettings[bot].AdditionalEnemyTypes.Add("pmcUSEC");
            }
            pmcConfig.HostilitySettings[bot].SavageEnemyChance = 100;
            pmcConfig.HostilitySettings[bot].BearEnemyChance = 100;
            pmcConfig.HostilitySettings[bot].UsecEnemyChance = 100;
            pmcConfig.HostilitySettings[bot].SavagePlayerBehaviour = "AlwaysEnemies";

            foreach (var chancedEnemy in pmcConfig.HostilitySettings[bot].ChancedEnemies)
            {
                chancedEnemy.EnemyChance = 100;
            }
        }
    }
    
    public void RemoveCustomPmcWaves()
    {
        pmcConfig.RemoveExistingPmcWaves = false;
        pmcConfig.CustomPmcWaves = new Dictionary<string, List<BossLocationSpawn>>();
    }
}