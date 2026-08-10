using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BotPlacementSystemClient.Utils;
using Comfort.Common;
using EFT;
using EFT.Game.Spawning;
using UnityEngine;

namespace BotPlacementSystemClient.Spawning;

public class PmcGroupSpawner : MonoBehaviour
{
    private static BotBossSpawn _bossSpawnerClass;
    private static BotSpawner _botSpawner;
    private static IBotCreator _iBotCreator;
        
    public static readonly Dictionary<string, HashSet<string>> AllPmcGroups = new();
    private static readonly Dictionary<string, string> FollowerToLeader = new();
    private static readonly Dictionary<string, BotBossSpawn.CG_Class332> WavePmcGroupClassData = new();

    public static bool Initialized;
        
    public static async Task InitializePmcSpawner(BotBossSpawn bossSpawnerClass, BotSpawner botSpawner, IBotCreator botCreator)
    {
        AllPmcGroups.Clear();
        FollowerToLeader.Clear();
        WavePmcGroupClassData.Clear();
            
        _bossSpawnerClass = bossSpawnerClass;
        _botSpawner = botSpawner;
        _iBotCreator = botCreator;
            
        var gameWorld = Singleton<GameWorld>.Instance;
            
        if (!await Utility.WaitForHumanPlayer())
        {
            Initialized = true;
            return;
        }
            
        foreach (var iPlayer in gameWorld.RegisteredPlayers)
        {
            var player = iPlayer as Player;
            if (player != null && !player.IsAI && !Utility.IsPlayerHeadless(player) && player.Profile.Info.Settings.Role != WildSpawnType.marksman)
            {
                if (Plugin.DebugLogging)
                    Plugin.LogSource.LogInfo($"Caching connected player as human: {player.Profile.Info.Nickname}");
                    
                Utility.CachedConnectedPlayers.Add(player);
                if (player.Profile.Side is EPlayerSide.Bear or EPlayerSide.Usec)
                {
                    if (Plugin.DebugLogging)
                        Plugin.LogSource.LogInfo($"Caching connected player as PMC: {player.Profile.Info.Nickname}");
                    Utility.CachedPmcs.Add(player);
                }
                else
                {
                    if (Plugin.DebugLogging)
                        Plugin.LogSource.LogInfo($"Caching connected player as Scav: {player.Profile.Info.Nickname}");
                    Utility.CachedAssaultBots.Add(player);
                }
            }
        }
            
        Initialized = true;
    }
        
    public static async Task StartSpawnPmcGroup(BotCreationData creationData, BossLocationSpawn wave, BotSpawnParams spawnParams, int followersCount, BotZone botZone, List<ISpawnPoint> openedPositions)
    {
        BotBossSpawn.CG_Class332 cgClass = new BotBossSpawn.CG_Class332();
        cgClass.BotBossSpawn = _bossSpawnerClass;
        cgClass.creationData = creationData;
        cgClass.botZone = botZone;
        cgClass.followersCount = followersCount;
        cgClass.spawnParams = spawnParams;
        cgClass.wave = wave;
        cgClass.openedPositions = openedPositions;
        float time = cgClass.wave.Time;
        cgClass.spawnParams.ShallBeGroup = new ShallBeGroupParams(true, true, cgClass.followersCount + 1);
        GetProfileDataParams botProfileDataClass = new GetProfileDataParams(EPlayerSide.Savage, cgClass.wave.BossType, cgClass.wave.BossDif, time, cgClass.spawnParams);
        cgClass.side = EPlayerSide.Savage;
        bool flag = cgClass.wave.IsStartWave();
        ISpawnPoint spawnPoint = cgClass.openedPositions[0];
        cgClass.openedPositions.Remove(spawnPoint);
        cgClass.spawnProcessData = new BotBossSpawn.BossSpawnProcess(cgClass.wave, cgClass.botZone, spawnPoint);
        _bossSpawnerClass._spawnProcess.Add(cgClass.spawnProcessData);

        var leaderProfileId = creationData.Profiles[0].ProfileId;

        if (!AllPmcGroups.ContainsKey(leaderProfileId))
        {
            AllPmcGroups[leaderProfileId] = new HashSet<string>();
        }

        if (flag)
        {
            var canSpawn = _bossSpawnerClass._spawner.CanSpawnRole(botProfileDataClass);
            if (Plugin.DebugLogging)
                Plugin.LogSource.LogInfo($"{creationData.Profiles[0].Nickname} | CanSpawnRole: {canSpawn}");
                
            if (canSpawn)
            {
                SpawnLeader(cgClass.creationData, spawnPoint, cgClass.botZone, cgClass.followersCount, botProfileDataClass, new Action<BotOwner>(cgClass.method_0));
                await SpawnFollowers(cgClass.creationData, cgClass.botZone, cgClass.followersCount, cgClass.spawnParams, cgClass.wave, cgClass.side, cgClass.openedPositions, true, leaderProfileId);
            }
        }
        else
        {
            if (followersCount != 0)
            {
                WavePmcGroupClassData[leaderProfileId] = cgClass;
            }
            SpawnLeader(cgClass.creationData, spawnPoint, cgClass.botZone, cgClass.followersCount, botProfileDataClass, new Action<BotOwner>(cgClass.method_0));
        }
    }

    private static void SpawnLeader(BotCreationData creationData, ISpawnPoint point, BotZone ss, int followers, GetProfileDataParams data, Action<BotOwner> callback)
    {
        if (Plugin.DebugLogging)
            Plugin.LogSource.LogInfo($"{creationData.Profiles[0].Nickname} | SpawnLeader: SpawnStopped={creationData.SpawnStopped} ProfileCount={creationData.Profiles.Count}");
            
        BotBossSpawn.CG_Class335 @class = new BotBossSpawn.CG_Class335();
        @class.data = data;
        @class.followers = followers;
        @class.callback = callback;
        List<ISpawnPoint> list = new List<ISpawnPoint> { point };
        SpawnBotsInZoneOnPositions(list, ss, creationData, new Action<BotOwner>(@class.method_0));
    }

    private static async Task SpawnFollowers(BotCreationData bossCreationData, BotZone zone, int followersCount, BotSpawnParams spawnParams, BossLocationSpawn wave, EPlayerSide side, List<ISpawnPoint> pointsToSpawn, bool forceSpawn, string leaderProfileId)
    {
        List<BossLocationSpawnSubData> escors = wave.GetEscors();
        if (escors != null)
        {
            await GenerateFollowerData(bossCreationData, zone, side, wave, escors, spawnParams, pointsToSpawn, forceSpawn, leaderProfileId);
        }
        else if (followersCount > 0)
        {
            BotCreationData BotCreationData = await BotCreationData.Create(new GetProfileDataParams(EPlayerSide.Savage, wave.EscortType, wave.EscortDif, wave.Time, spawnParams, false), _iBotCreator, followersCount, _botSpawner);
            RegisterFollowers(leaderProfileId, BotCreationData.Profiles);
            TryToSpawnInZoneAndDelay(zone, BotCreationData, false, true, pointsToSpawn, forceSpawn);
        }
    }

    private static async Task GenerateFollowerData(BotCreationData creationData, BotZone zone, EPlayerSide side, BossLocationSpawn wave, List<BossLocationSpawnSubData> escorts, BotSpawnParams spawnParams, List<ISpawnPoint> pointsToSpawn, bool forceSpawn, string leaderProfileId)
    {
        if (wave.EscortCount > pointsToSpawn.Count)
        {
            pointsToSpawn = null;
        }
            
        foreach (BossLocationSpawnSubData bossLocationSpawnSubData in escorts)
        {
            List<ISpawnPoint> list = null;
            if (pointsToSpawn != null)
            {
                list = new List<ISpawnPoint>();
                for (int i = 0; i < bossLocationSpawnSubData.BossEscortAmount; i++)
                {
                    if (pointsToSpawn.Count > 0)
                    {
                        ISpawnPoint spawnPoint = pointsToSpawn.First();
                        list.Add(spawnPoint);
                        pointsToSpawn.Remove(spawnPoint);
                    }
                }
                if (bossLocationSpawnSubData.BossEscortAmount != list.Count)
                {
                    list = null;
                }
            }
                
            BotCreationData result = await BotCreationData.Create(new GetProfileDataParams(side, bossLocationSpawnSubData.BossEscortType, bossLocationSpawnSubData.EscortDifficulty, wave.Time, spawnParams), _iBotCreator, bossLocationSpawnSubData.BossEscortAmount, _botSpawner);
            RegisterFollowers(leaderProfileId, result.Profiles);
            TryToSpawnInZoneAndDelay(zone, result, false, true, list, forceSpawn);
            await Task.Yield();
            list = null;
        }
    }

    private static void RegisterFollowers(string leaderProfileId, IEnumerable<Profile> profiles)
    {
        if (!AllPmcGroups.TryGetValue(leaderProfileId, out var followerSet))
        {
            followerSet = new HashSet<string>();
            AllPmcGroups[leaderProfileId] = followerSet;
        }

        foreach (var profile in profiles)
        {
            followerSet.Add(profile.ProfileId);
            FollowerToLeader[profile.ProfileId] = leaderProfileId;
        }
    }

    private static void SpawnBotsInZoneOnPositions(List<ISpawnPoint> openedPositions, BotZone botZone, BotCreationData data, Action<BotOwner> callback = null)
    {
        AddSpawnPointDataAndSpawn(openedPositions, botZone, data, callback, _botSpawner._cancellationTokenSource.Token).HandleExceptions();
    }

    private static async Task AddSpawnPointDataAndSpawn(List<ISpawnPoint> spawnPoints, BotZone botZone, BotCreationData data, Action<BotOwner> callback, CancellationToken cancellationToken)
    {
        _botSpawner._inSpawnProcess += spawnPoints.Count;
        if (!data.SpawnStopped)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                for (var i = 0; i < spawnPoints.Count; i++)
                {
                    var spawnPoint = spawnPoints[i];

                    if (spawnPoint.Categories.ContainPlayerCategory())
                    {
                        var corePointID = Singleton<IBotGame>.Instance.BotsController.CoversData.GetClosest(spawnPoint.Position).CorePointInGame.Id;
                        if (Plugin.DebugLogging)
                        {
                            Plugin.LogSource.LogInfo(
                                $"{data.Profiles[i].Nickname} - {i} | CorePoint ContainPlayerCategory, old {spawnPoint.CorePointId} -> {corePointID}");
                        }
                        data.AddPosition(spawnPoint.Position, corePointID);
                    }
                    else
                    {
                        if (Plugin.DebugLogging)
                        {
                            Plugin.LogSource.LogInfo(
                                $"{data.Profiles[i].Nickname} - {i} | CorePoint is good ({spawnPoint.CorePointId})");
                        }
                        data.AddPosition(spawnPoint.Position, spawnPoint.CorePointId);
                    }
                }
                spawnPoints.Clear();
                SpawnBot(botZone, data, callback, cancellationToken);
                await Task.Yield();
            }
        }
    }

    private static void TryToSpawnInZoneAndDelay(BotZone botZone, BotCreationData data, bool withCheckMinMax, bool newWave, List<ISpawnPoint> pointsToSpawn = null, bool forcedSpawn = false)
    {
        if (data.SpawnStopped)
        {
            return;
        }
            
        TryToSpawnInZoneInner(botZone, data, data.Count, withCheckMinMax, newWave, pointsToSpawn, forcedSpawn);
    }

    private static SimpleBotSpawnDelayModel TryToSpawnInZoneInner(BotZone botZone, BotCreationData data, int count, bool withCheckMinMax, bool newWave, List<ISpawnPoint> pointsToSpawn = null, bool forcedSpawn = false)
    {
        if (data.SpawnStopped)
        {
            return null;
        }
            
        if (DebugBotData.UseDebugData && DebugBotData.Instance.spawnInstantly)
        {
            forcedSpawn = true;
        }
            
        if (!_botSpawner._botCreator.StartProfilesLoaded)
        {
            return new SimpleBotSpawnDelayModel(botZone, count, data, new Action<SimpleBotSpawnDelayModel>(_botSpawner.CheckSpawnOnFreeAfterDelay));
        }
            
        if (DebugBotData.UseDebugData && DebugBotData.Instance.spawnInstantly)
        {
            List<ISpawnPoint> array = _botSpawner._spawnSystem.SelectAISpawnPoints(data, botZone, count, null, ActionIfNotEnoughPoints.DuplicateIfAtLeastOne);
            SpawnBotsInZoneOnPositions(array, botZone, data, null);
            return new SimpleBotSpawnDelayModel(botZone, 0, data, new Action<SimpleBotSpawnDelayModel>(_botSpawner.CheckSpawnOnFreeAfterDelay));
        }
            
        if (!data.CanAtZoneByType(botZone, _botSpawner.BotGame.BotsController.ZonesLeaveController))
        {
            return new SimpleBotSpawnDelayModel(botZone, count, data, new Action<SimpleBotSpawnDelayModel>(_botSpawner.CheckSpawnOnFreeAfterDelay));
        }
            
        _botSpawner._bots.GetListByZone(botZone);
        bool flag = data.IsBossOrFollowerByTime();
            
        if (withCheckMinMax && !botZone.HaveFreeSpace(count) && !flag && !forcedSpawn)
        {
            return new SimpleBotSpawnDelayModel(botZone, count, data, new Action<SimpleBotSpawnDelayModel>(_botSpawner.CheckSpawnOnFreeAfterDelay));
        }
            
        if (newWave)
        {
            Action<SpawnedWave> onSpawnedWave = (x) => new SpawnedWave(botZone, count, data);
            _botSpawner.OnSpawnedWave += onSpawnedWave;
        }
            
        int num;
        int num2;
        if (withCheckMinMax && !forcedSpawn)
        {
            _botSpawner.CheckOnMax(count, out num, out num2, false);
        }
        else
        {
            num = 0;
            num2 = count;
        }
            
        if (Plugin.DebugLogging)
            Plugin.LogSource.LogInfo($"{data.Profiles[0].Nickname} | SpawnCheck Req:{count} Block:{num} Allow:{num2} Alive:{_botSpawner._bots.BotOwners.Count()} InProcess:{_botSpawner._inSpawnProcess} Max:{_botSpawner.MaxBots}");
            
        if (num > 0)
        {
            return new SimpleBotSpawnDelayModel(botZone, num, data, new Action<SimpleBotSpawnDelayModel>(_botSpawner.CheckSpawnOnFreeAfterDelay));
        }
            
        if (num2 > 0)
        {
            if (flag)
            {
                data.IsSpawnOnStart();
            }
                
            count = num2;
            List<ISpawnPoint> list2;
            if (pointsToSpawn != null)
            {
                list2 = pointsToSpawn;
            }
            else
            {
                list2 = _botSpawner._spawnSystem.SelectAISpawnPoints(data, botZone, count, null, ActionIfNotEnoughPoints.DuplicateIfAtLeastOne);
                if (count > list2.Count)
                {
                    if (!forcedSpawn)
                    {
                        var num3 = count - list2.Count;
                        return new SimpleBotSpawnDelayModel(botZone, num3, data, new Action<SimpleBotSpawnDelayModel>(_botSpawner.CheckSpawnOnFreeAfterDelay));
                    }
                    list2 = _botSpawner._spawnSystem.SelectAISpawnPoints(data, botZone, count, null, ActionIfNotEnoughPoints.ReturnFoundPoints);
                }
            }
            SpawnBotsInZoneOnPositions(list2, botZone, data, null);
        }
        return null;
    }

    private static void SpawnBot(BotZone zone, BotCreationData data, Action<BotOwner> callback, CancellationToken cancellationToken)
    {
        BotSpawner.CG_Class1164 @class = new BotSpawner.CG_Class1164();
        @class.botSpawner_0 = _botSpawner;
        @class.data = data;
        @class.callback = callback;
            
        if (_botSpawner._gameEnd)
            return;
            
        if (@class.data.SpawnStopped)
        {
            if (Plugin.DebugLogging)
                Plugin.LogSource.LogInfo($"{data.Profiles[0].Nickname} | SpawnStopped");
                
            _botSpawner._inSpawnProcess--;
            return;
        }
            
        @class.stopWatch = new Stopwatch();
        @class.stopWatch.Start();
        @class.shallBeGroup = @class.data.SpawnParams != null && @class.data.SpawnParams.ShallBeGroup != null && @class.data.SpawnParams.ShallBeGroup.Group && @class.data.SpawnParams.ShallBeGroup.RemainCount > 0;
            
        if (@class.shallBeGroup)
        {
            @class.data.SpawnParams.ShallBeGroup.DescreaseCount();
        }
            
        _botSpawner._botCreator.ActivateBot(@class.data, zone, @class.shallBeGroup, new Func<BotOwner, BotZone, BotsGroup>(GetGroupAndSetEnemies), new Action<BotOwner>(@class.method_0), cancellationToken);

        var spawnedBotProfileId = data.Profiles[0].ProfileId;
        if (!WavePmcGroupClassData.TryGetValue(spawnedBotProfileId, out var originalClassData)) return;

        SpawnFollowers(@originalClassData.creationData, @originalClassData.botZone, @originalClassData.followersCount, @originalClassData.spawnParams, @originalClassData.wave, @originalClassData.side, @originalClassData.openedPositions, true, spawnedBotProfileId).HandleExceptions();
    }

    private static BotsGroup GetGroupAndSetEnemies(BotOwner bot, BotZone zone)
    {
        var side = bot.Profile.Info.Side;
        var botProfileId = bot.Profile.ProfileId;
        List<BotOwner> botOwners = [];

        if (Plugin.DebugLogging)
            Plugin.LogSource.LogInfo($"{bot.Profile.Nickname} | GetGroupAndSetEnemies: IsLeader={AllPmcGroups.ContainsKey(botProfileId)} | IsFollower={FollowerToLeader.ContainsKey(botProfileId)}");
            
        lock (Utility.SpawnPointLock)
        {
            if (!Utility.CachedPmcs.Contains(bot.GetPlayer))
            {
                Utility.CachedPmcs.Add(bot.GetPlayer);
            }

            Utility.ReservedSpawnPositions.RemoveWhere(pos => Vector3.Distance(pos, bot.Position) < 10f);
        }
            
        if (AllPmcGroups.ContainsKey(botProfileId))
        {
            foreach (var botOwner in _botSpawner.GetBotEnemiesList(bot))
            {
                botOwners.Add(botOwner);
            }
                
            _botSpawner.SetBotAsEnemy(bot);
            var botsGroup = new BotsGroup(zone, _botSpawner.BotGame, bot, botOwners, _botSpawner._deadBodiesController, _botSpawner._allPlayers, true);
                
            if (bot.SpawnProfileData.SpawnParams.ShallBeGroup != null)
            {
                botsGroup.TargetMembersCount = bot.SpawnProfileData.SpawnParams.ShallBeGroup.StartCount;
            }
                
            _botSpawner.Groups.Add(zone, side, botsGroup, true);
            return botsGroup;
        }

        if (FollowerToLeader.TryGetValue(botProfileId, out var leaderId))
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                foreach (var (botZone, botGroup) in _botSpawner.Groups)
                {
                    foreach (var group in botGroup._bossGroup)
                    {
                        if (group._initialBot.ProfileId == leaderId)
                        {
                            if (Plugin.DebugLogging)
                                Plugin.LogSource.LogInfo($"{bot.Profile.Nickname} found Leader={leaderId} - Attempts: {attempt}");
                                
                            _botSpawner.SetBotAsEnemy(bot);
                            return group;
                        }
                    }
                }
                Thread.Sleep(50);
            }
                
            if (Plugin.DebugLogging)
                Plugin.LogSource.LogInfo($"[ABPS] GetGroupAndSetEnemies: {bot.Profile.Nickname} could not find leader group after retries, leaderId={leaderId}");
        }

        return null;
    }
}