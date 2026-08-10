using System;
using System.Linq;
using System.Reflection;
using BotPlacementSystemClient.Utils;
using Comfort.Common;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using Systems.Effects;
using UnityEngine;

namespace BotPlacementSystemClient.Patches;

using JsonType;

internal class NonWavesSpawnSystemPatch : ModulePatch
{
    private static float _nextDespawnCheckTime = 0f;
        
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(NonWavesSpawnScenario), nameof(NonWavesSpawnScenario.Update));
    }

    [PatchPrefix]
    private static bool PatchPrefix(
        NonWavesSpawnScenario __instance,
        ref BotsController ____botsController,
        ref AbstractGame ____game,
        ref LocationSettings.Location ____location,
        ref WDictionary<BotDifficulty> ____botDifficultiesWights,
        ref WDictionary<WildSpawnType> ____wildTypesWeights,
        ref NonWaveGroupScenario ____groupScenario,
        ref bool ____maxWasReached,
        ref bool ____started,
        ref bool ____isSpawnTime,
        ref float ____botSpawnTime,
        ref float ____botSpawnPeriodCheck,
        ref float ____lastCheckTime)
    {
        ref var isActive = ref ____started;
        ref var isAtBotCap = ref ____maxWasReached;
        ref var isInSpawnWindow = ref ____isSpawnTime;
        ref var nextWindowToggleTime = ref ____botSpawnTime;
        ref var spawnAttemptInterval = ref ____botSpawnPeriodCheck;
        ref var lastSpawnAttemptTime = ref ____lastCheckTime;
        ref var difficultyWeights = ref ____botDifficultiesWights;

        if (__instance == null || !isActive) return true;

        if (____game.PastTime < (float)____location.BotStart || ____game.PastTime > (float)____location.BotStop)
            return false;

        if (nextWindowToggleTime.Equals(null) || nextWindowToggleTime <= ____game.PastTime)
        {
            isInSpawnWindow = !isInSpawnWindow;
            nextWindowToggleTime = ____game.PastTime + (isInSpawnWindow
                ? MyExtensions.Random((float)____location.BotSpawnTimeOnMin, (float)____location.BotSpawnTimeOnMax)
                : MyExtensions.Random((float)____location.BotSpawnTimeOffMin, (float)____location.BotSpawnTimeOffMax));
        }

        if (!isInSpawnWindow)
        {
            return false;
        }

        if (____game.PastTime - lastSpawnAttemptTime < spawnAttemptInterval)
        {
            return false;
        }
        lastSpawnAttemptTime = ____game.PastTime;

        var freeSlots = (____botsController._maxCount - Plugin.SoftCap) - ____botsController.AliveLoadingDelayedBotsCount;

        if (isAtBotCap)
        {
            if (freeSlots < ____location.BotSpawnCountStep)
            {
                return false;
            }
            spawnAttemptInterval = 15f;
            isAtBotCap = false;
        }
        else if (freeSlots <= 0)
        {
            spawnAttemptInterval = Math.Max((float)____location.BotSpawnPeriodCheck, 15f);
            isAtBotCap = ____botsController._maxCount - ____botsController.AliveAndLoadingBotsCount <= 0;
            return false;
        }
            
        if (Utility.BotsSpawnedPerPlayer >= ____botsController.BotLocationModifier.NonWaveSpawnBotsLimitPerPlayerPvE)
        {
            return false;
        }

        var mapName = Utility.CurrentLocation;
        if (Utility.CurrentMapZones.Count == 0)
        {
            Utility.CurrentMapZones = ____botsController.BotSpawner._allBotZones.ToList();
        }

        var botZone = GetValidBotZone(WildSpawnType.assault, 1, ____botsController.BotSpawner._allBotZones, mapName, ____botsController);
        ____botsController.ActivateBotsByWave(new SpawnWave
        {
            BotsCount = 1,
            Time = Time.time,
            Difficulty = difficultyWeights.Random(),
            IsPlayers = MyExtensions.IsTrue100(Plugin.PScavChance),
            Side = EPlayerSide.Savage,
            WildSpawnType = WildSpawnType.assault,
            SpawnAreaName = botZone,
            WithCheckMinMax = false,
            ChanceGroup = 0,
        });
        Utility.BotsSpawnedPerPlayer += 1d / Math.Max(1, Utility.CachedConnectedPlayers.Count);

        if (!(Time.time >= _nextDespawnCheckTime) || !Plugin.DespawnFurthest)
        {
            return false;
        }
            
        _nextDespawnCheckTime = Time.time + Plugin.DespawnTimer;
        var center = GetPlayerCountAndCenter();
        DespawnFurthestBots(____botsController, center);

        return false;
    }
        
    private static void DespawnFurthestBots(BotsController botsController, Vector3 centerOfPlayers)
    {
        var despawnDistance = Plugin.DespawnDistance;

        if (Plugin.DespawnPmcs)
        {
            foreach (var pmc in Utility.CachedPmcs)
            {
                if (pmc == null || !pmc.HealthController.IsAlive) continue;
                if (Vector3.Distance(pmc.Position, centerOfPlayers) >= despawnDistance)
                    AttemptToDespawnBot(botsController, pmc.AIData.BotOwner);
            }
        }

        foreach (var scav in Utility.CachedAssaultBots)
        {
            if (scav == null || !scav.HealthController.IsAlive) continue;
            if (Vector3.Distance(scav.Position, centerOfPlayers) >= despawnDistance)
                AttemptToDespawnBot(botsController, scav.AIData.BotOwner);
        }
    }
        
    private static Vector3 GetPlayerCountAndCenter()
    {
        var centerPoint = Vector3.zero;
        var count = 0;

        foreach (var player in Utility.CachedConnectedPlayers)
        {
            if (player == null || !player.HealthController.IsAlive)
            {
                continue;
            }
                
            centerPoint += player.Position;
            count++;
        }

        return count == 0 ? centerPoint : centerPoint / count;
    }

    private static void AttemptToDespawnBot(BotsController botsController, BotOwner botToDespawn)
    {
        var effectsCommutator = Singleton<Effects>.Instance.EffectsCommutator;
        var gameWorld = Singleton<GameWorld>.Instance;

        if (effectsCommutator is null || gameWorld is null) return;

        var botPlayer = botToDespawn.GetPlayer;
            
        effectsCommutator.StopBleedingForPlayer(botPlayer);
        gameWorld.UnregisterPlayer(botToDespawn);
        gameWorld.UnregisterPlayer(botPlayer);
        botToDespawn.Deactivate();
        botToDespawn.Dispose();
        botsController.BotDied(botToDespawn);
        botsController.DestroyInfo(botPlayer);
        UnityEngine.Object.DestroyImmediate(botToDespawn.gameObject);
        UnityEngine.Object.Destroy(botToDespawn);
    }
        
    private static string GetValidBotZone(WildSpawnType botType, int count, BotZone[] allZones, string location, BotsController _botsController)
    {
        if (Utility.CachedNonSnipeZones == null || Utility.CachedNonSnipeZones.Count == 0)
        {
            Utility.CachedNonSnipeZones = allZones.Where(x => !x.SnipeZone).ToList();
        }
        var botZones = Utility.CachedNonSnipeZones.OrderBy(_ => MyExtensions.Random(0f, 1f)).ToList();

        if (Plugin.EnableHotzones && MyExtensions.IsTrue100(Plugin.HotzoneScavChance) && Utility.MapHotSpots.TryGetValue(location, out var spot))
        {
            var hotSpotZone = spot.RandomElement();
            return hotSpotZone;
        }
        foreach (var currentZone in botZones)
        {
            if (_botsController.Bots.GetListByZone(currentZone).Count(x => x.IsRole(WildSpawnType.assault)) < Plugin.ZoneScavCap)
            {
                return currentZone.NameZone;
            }
        }

        return "";
    }
}