using System;
using System.Collections;
using System.Collections.Generic;
using GonzoDemo;
using GonzoDemo.AI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>Server-only encounter driver. DungeonManager replicates the route and door state.</summary>
[DisallowMultipleComponent]
public class DungeonSpawnManager : MonoBehaviour
{
    [Serializable]
    public class PoolEntry
    {
        public Minion Prefab;
        [Min(0)] public float Weight = 1;
    }
    [Serializable]
    public class SpawnPool
    {
        public string Name = "Default";
        public List<PoolEntry> Entries = new List<PoolEntry>();
    }
    [Serializable]
    public class Wave
    {
        [Tooltip("Used only when no compatible global or custom SpawnList asset is selected.")]
        [Min(0)] public int PoolIndex;
        [Min(1)] public int Count = 3;
        [Min(0)] public float SpawnInterval = 0.5f;
        [Min(0)] public float DelayBefore = 2;
        [Min(0.01f)] public float Difficulty = 1;
    }
    [Serializable]
    public class DifficultySettings
    {
        [Min(0.01f)] public float BaseMultiplier = 1;
        [Min(0)] public float PerRoom = 0.1f;
        [Min(0)] public float PerWave = 0.1f;
        [Min(0.01f)] public float Health = 1;
        [Min(0.01f)] public float Chakra = 1;
        [Min(0)] public float Damage = 1;
        [Min(0)] public float Speed = 1;
        [Min(0)] public float AttackSpeed = 1;
        [Min(0)] public float Armor = 1;
        [Min(0)] public float MagicResistance = 1;
    }

    [Header("Room")]
    [SerializeField] private DungeonManager dungeon;
    [SerializeField] private RoomObject room;
    [Tooltip("All direct children are spawn markers. Defaults to a descendant named spawners.")]
    [SerializeField] private Transform spawners;
    [Tooltip("Room-volume triggers on this object or its children. Auto-detected if empty.")]
    [SerializeField] private Collider[] roomTriggers;
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 10;
    [Header("Encounters")]
    [Tooltip("Optional enemy list override. If empty, DungeonManager selects one of this map's lists by party difficulty.")]
    [SerializeField] private SpawnListAsset customEnemySpawnList;
    [Tooltip("Optional reward list override. If empty, DungeonManager selects one of this map's lists by party difficulty.")]
    [SerializeField] private SpawnListAsset customRewardSpawnList;
    [Tooltip("Legacy per-room enemy pools used only when no matching SpawnList asset is available.")]
    public List<SpawnPool> Pools = new List<SpawnPool>();
    public List<Wave> Waves = new List<Wave> { new Wave() };
    public DifficultySettings Difficulty = new DifficultySettings();
    [Header("Reward room (assigned by DungeonManager)")]
    [SerializeField] private bool isRewardRoom;
    [Tooltip("Legacy per-room rewards used only when no matching SpawnList asset is available.")]
    public List<NetworkObject> RewardPrefabs = new List<NetworkObject>();
    [Header("Server events")]
    public UnityEvent OnEncounterStarted = new UnityEvent();
    public UnityEvent<int> OnWaveStarted = new UnityEvent<int>();
    public UnityEvent<int> OnWaveCleared = new UnityEvent<int>();
    public UnityEvent<Minion> OnMinionDied = new UnityEvent<Minion>();
    public UnityEvent OnRoomCleared = new UnityEvent();
    public UnityEvent OnEncounterAborted = new UnityEvent();
    public RoomObject Room => room;
    public bool IsRewardRoom => isRewardRoom;
    public bool IsRunning { get; private set; }
    public int CurrentWave { get; private set; }
    public int LivingMinions => living.Count;
    private bool CanRun => dungeon != null && dungeon.HasServerAuthority;
    private readonly Dictionary<Collider, NetworkObject> occupants = new Dictionary<Collider, NetworkObject>();
    private readonly List<Collider> stale = new List<Collider>();
    private readonly HashSet<Minion> living = new HashSet<Minion>();
    private readonly List<Minion> spawned = new List<Minion>();
    private readonly List<Vector3> spawnPositions = new List<Vector3>();
    private readonly List<DungeonRoomTrigger> triggers = new List<DungeonRoomTrigger>();
    private SpawnListAsset activeSpawnList;
    private List<SpawnListAsset.Entry> activeSpawnEntries;
    private int encounterDifficulty = 1;
    private int encounterPlayerLevel = 1;
    private Coroutine encounter;
    private bool completed;
    private float retryAt;

    public void Bind(DungeonManager manager, RoomObject assignedRoom, bool reward)
    {
        dungeon = manager;
        room = assignedRoom;
        isRewardRoom = reward;
    }
    private void Awake()
    {
        if (room == null) room = GetComponentInParent<RoomObject>();
        if (room == null) room = GetComponentInChildren<RoomObject>(true);
        if (room == null && transform.parent != null)
        {
            var siblings = transform.parent.GetComponentsInChildren<RoomObject>(true);
            if (siblings.Length == 1) room = siblings[0];
        }
        if (spawners == null)
            foreach (Transform child in (room != null && room.transform.parent != null ? room.transform.parent : transform).GetComponentsInChildren<Transform>(true))
                if (string.Equals(child.name, "spawners", StringComparison.OrdinalIgnoreCase)) { spawners = child; break; }
        if (roomTriggers == null || roomTriggers.Length == 0)
            roomTriggers = Array.FindAll(GetComponentsInChildren<Collider>(true), item => item.isTrigger);
        foreach (var trigger in roomTriggers)
        {
            if (trigger == null || !trigger.isTrigger) continue;
            var relay = trigger.GetComponent<DungeonRoomTrigger>() ?? trigger.gameObject.AddComponent<DungeonRoomTrigger>();
            relay.Owner = this;
            if (!triggers.Contains(relay)) triggers.Add(relay);
            if (trigger.attachedRigidbody == null)
            {
                var body = trigger.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
            }
        }
    }
    internal void Observe(Collider collider)
    {
        if (!CanRun || !enabled) return;
        var player = collider.GetComponentInParent<PlayerStats>();
        if (player == null || !player.IsSpawned) return;
        var network = player.GetComponentInParent<NetworkObject>();
        if (network != null && network.IsPlayerObject) occupants[collider] = network;
    }
    internal void Leave(Collider collider)
    {
        if (!CanRun) return;
        foreach (var trigger in triggers) if (trigger != null && trigger.Contains(collider)) return;
        occupants.Remove(collider);
    }
    private void Update()
    {
        if (!CanRun || room == null) return;
        stale.Clear();
        foreach (var entry in occupants)
            if (entry.Key == null || !entry.Key.enabled || !entry.Key.gameObject.activeInHierarchy ||
                entry.Value == null || !entry.Value.IsSpawned) stale.Add(entry.Key);
        foreach (var collider in stale) occupants.Remove(collider);
        if (IsRunning)
        {
            if (!HasLivingPlayers()) AbortEncounter("No living players remain connected.");
            return;
        }
        if (completed || Time.time < retryAt || room.RoomIndex <= 0 ||
            room.RoomIndex != dungeon.ClearingIndex + 1 || !AllPlayersInside()) return;
        if (!ValidateEncounter(out string error))
        {
            Debug.LogError($"{name}: {error}", this);
            retryAt = Time.time + 5;
            return;
        }
        if (!dungeon.TryBeginEncounter(this)) return;
        IsRunning = true;
        encounter = StartCoroutine(RunEncounter());
    }
    private bool HasLivingPlayers()
    {
        foreach (var client in dungeon.NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject;
            if (player != null && player.IsSpawned && player.TryGetComponent<PlayerStats>(out var stats) && stats.GetHealth() > 0) return true;
        }
        return false;
    }
    private bool AllPlayersInside()
    {
        int count = 0;
        foreach (var client in dungeon.NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject;
            if (player == null || !player.IsSpawned) return false;
            var stats = player.GetComponent<PlayerStats>();
            if (stats == null || stats.GetHealth() <= 0) continue;
            count++;
            if (!occupants.ContainsValue(player)) return false;
        }
        return count > 0;
    }
    private bool ValidateEncounter(out string error)
    {
        error = null;
        activeSpawnList = null;
        activeSpawnEntries = null;
        encounterDifficulty = GetPartyDifficulty();
        encounterPlayerLevel = GetPartyDifficulty(false);
        spawnPositions.Clear();
        if (spawners == null || spawners.childCount == 0) { error = "Configure children under spawners."; return false; }
        foreach (Transform marker in spawners)
        {
            if (isRewardRoom) spawnPositions.Add(marker.position);
            else if (NavMesh.SamplePosition(marker.position, out var hit, navMeshSampleRadius, NavMesh.AllAreas)) spawnPositions.Add(hit.position);
        }
        if (spawnPositions.Count == 0) { error = "No spawn marker lies on a NavMesh."; return false; }
        if (isRewardRoom)
        {
            if (TryResolveSpawnList(true, customRewardSpawnList, out activeSpawnList, out activeSpawnEntries))
            {
                if (activeSpawnEntries.Exists(entry => entry.Prefab.GetComponent<NetworkObject>() == null ||
                    !dungeon.NetworkManager.NetworkConfig.Prefabs.Contains(entry.Prefab)))
                    error = "Register every reward prefab in NetworkPrefabs and include a NetworkObject.";
            }
            else if (customRewardSpawnList != null)
                error = "The custom reward SpawnList has no valid network reward prefabs with positive weights.";
            else if (RewardPrefabs == null || RewardPrefabs.Count == 0 || RewardPrefabs.Exists(prefab => prefab == null))
                error = "Configure a reward SpawnList asset or legacy reward prefabs.";
            else if (RewardPrefabs.Exists(prefab => !dungeon.NetworkManager.NetworkConfig.Prefabs.Contains(prefab.gameObject)))
                error = "Register every reward prefab in NetworkPrefabs.";
            return error == null;
        }
        if (Difficulty == null || Waves == null || Waves.Count == 0) { error = "Configure difficulty and at least one wave."; return false; }
        bool hasSpawnList = TryResolveSpawnList(false, customEnemySpawnList, out activeSpawnList, out activeSpawnEntries);
        if (customEnemySpawnList != null && !hasSpawnList)
        { error = "The custom enemy SpawnList has no valid Minion prefabs with positive weights."; return false; }
        foreach (var wave in Waves)
        {
            if (wave == null || wave.Count < 1 ||
                (activeSpawnList == null && (wave.PoolIndex < 0 || Pools == null || wave.PoolIndex >= Pools.Count)))
            { error = "A wave has an invalid count or pool index."; return false; }
            if (activeSpawnList != null)
                continue;
            var pool = Pools[wave.PoolIndex];
            if (pool == null || pool.Entries == null || !pool.Entries.Exists(entry => entry != null && entry.Prefab != null && entry.Weight > 0))
            { error = "A wave references an empty pool."; return false; }
            foreach (var entry in pool.Entries)
                if (entry != null && entry.Prefab != null && entry.Weight > 0 &&
                    !dungeon.NetworkManager.NetworkConfig.Prefabs.Contains(entry.Prefab.gameObject))
                { error = "Register every minion prefab in NetworkPrefabs."; return false; }
        }
        if (activeSpawnList != null)
        {
            if (activeSpawnEntries == null || activeSpawnEntries.Count == 0)
            { error = "The selected enemy SpawnList has no valid Minion prefabs with positive weights."; return false; }
            if (activeSpawnEntries.Exists(entry => entry.Prefab.GetComponent<NetworkObject>() == null ||
                !dungeon.NetworkManager.NetworkConfig.Prefabs.Contains(entry.Prefab)))
            { error = "Register every enemy prefab in NetworkPrefabs and include a NetworkObject."; return false; }
        }
        return true;
    }

    private int GetPartyDifficulty(bool includePartySize = true)
    {
        int count = 0;
        long levelTotal = 0;
        if (dungeon != null && dungeon.NetworkManager != null)
        {
            foreach (var client in dungeon.NetworkManager.ConnectedClientsList)
            {
                var player = client.PlayerObject;
                if (player == null || !player.IsSpawned || !player.TryGetComponent<PlayerStats>(out var stats))
                    continue;
                levelTotal += Mathf.Max(1, stats.Level);
                count++;
            }
        }

        if (count == 0)
            return 1;

        float averageLevel = (float)levelTotal / count;
        return Mathf.Clamp(Mathf.FloorToInt(averageLevel + (includePartySize ? count : 0)), 1, 10);
    }

    private bool TryResolveSpawnList(
        bool rewards,
        SpawnListAsset customList,
        out SpawnListAsset selectedList,
        out List<SpawnListAsset.Entry> selectedEntries)
    {
        selectedList = null;
        selectedEntries = null;

        if (customList != null)
        {
            selectedList = customList;
            selectedEntries = GetValidEntries(customList, rewards);
            return selectedEntries.Count > 0;
        }

        int bestDistance = int.MaxValue;
        List<SpawnListAsset> bestLists = new List<SpawnListAsset>();
        List<SpawnListAsset> universalLists = new List<SpawnListAsset>();
        foreach (SpawnListAsset candidate in dungeon.SpawnLists)
        {
            if (candidate == null || GetValidEntries(candidate, rewards).Count == 0)
                continue;

            if (candidate.Difficulty == 0)
            {
                universalLists.Add(candidate);
                continue;
            }

            int distance = Mathf.Abs(Mathf.Clamp(candidate.Difficulty, 1, 10) - encounterDifficulty);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestLists.Clear();
            }
            if (distance == bestDistance)
                bestLists.Add(candidate);
        }

        // Universal lists never displace the normal nearest-level candidates.
        bestLists.AddRange(universalLists);
        if (bestLists.Count == 0)
            return false;

        selectedList = bestLists[UnityEngine.Random.Range(0, bestLists.Count)];
        selectedEntries = GetValidEntries(selectedList, rewards);
        return selectedEntries.Count > 0;
    }

    private static List<SpawnListAsset.Entry> GetValidEntries(SpawnListAsset list, bool rewards)
    {
        var result = new List<SpawnListAsset.Entry>();
        if (list == null || list.Spawn == null)
            return result;

        foreach (var entry in list.Spawn)
        {
            if (entry == null || entry.Prefab == null || entry.Weight <= 0)
                continue;

            bool isMinion = entry.Prefab.GetComponent<Minion>() != null;
            bool isNetworkPrefab = entry.Prefab.GetComponent<NetworkObject>() != null;
            if (rewards ? (!isMinion && isNetworkPrefab) : isMinion)
                result.Add(entry);
        }
        return result;
    }
    private IEnumerator RunEncounter()
    {
        yield return null;
        if (!CanRun || !IsRunning) yield break;
        OnEncounterStarted.Invoke();
        if (!CanRun || !IsRunning) yield break;
        if (isRewardRoom)
        {
            if (!TrySpawnReward()) { AbortEncounter("Reward could not be spawned."); yield break; }
            FinishEncounter();
            yield break;
        }
        for (int i = 0; i < Waves.Count; i++)
        {
            var wave = Waves[i];
            CurrentWave = i + 1;
            yield return new WaitForSeconds(Mathf.Max(0, wave.DelayBefore));
            if (!CanRun || !IsRunning) yield break;
            OnWaveStarted.Invoke(CurrentWave);
            for (int j = 0; j < wave.Count; j++)
            {
                if (!CanRun || !IsRunning) yield break;
                if (!TrySpawnMinion(wave, i)) { AbortEncounter("Minion could not be spawned."); yield break; }
                if (j + 1 < wave.Count) yield return new WaitForSeconds(Mathf.Max(0, wave.SpawnInterval));
            }
            while (living.Count > 0)
            {
                if (!CanRun || !IsRunning) yield break;
                living.RemoveWhere(unit => unit == null || !unit.IsSpawned || unit.IsDead);
                yield return null;
            }
            if (!CanRun || !IsRunning) yield break;
            OnWaveCleared.Invoke(CurrentWave);
            if (!CanRun || !IsRunning) yield break;
        }
        FinishEncounter();
    }
    private Minion SelectPrefab(SpawnPool pool)
    {
        if (activeSpawnEntries != null && activeSpawnEntries.Count > 0)
        {
            float totalWeight = 0;
            foreach (var entry in activeSpawnEntries)
                totalWeight += entry.Weight;
            float globalChoice = UnityEngine.Random.value * totalWeight;
            Minion globalFallback = null;
            foreach (var entry in activeSpawnEntries)
            {
                var prefab = entry.Prefab.GetComponent<Minion>();
                globalFallback = prefab;
                globalChoice -= entry.Weight;
                if (globalChoice <= 0) return prefab;
            }
            return globalFallback;
        }

        float total = 0;
        foreach (var entry in pool.Entries) if (entry != null && entry.Prefab != null && entry.Weight > 0) total += entry.Weight;
        float choice = UnityEngine.Random.value * total;
        Minion fallback = null;
        foreach (var entry in pool.Entries)
        {
            if (entry == null || entry.Prefab == null || entry.Weight <= 0) continue;
            fallback = entry.Prefab;
            choice -= entry.Weight;
            if (choice <= 0) return fallback;
        }
        return fallback;
    }
    private bool TrySpawnMinion(Wave wave, int waveIndex)
    {
        Minion unit = null;
        try
        {
            var prefab = SelectPrefab(activeSpawnList != null ? null : Pools[wave.PoolIndex]);
            Vector3 point = spawnPositions[UnityEngine.Random.Range(0, spawnPositions.Count)];
            var agent = prefab.GetComponent<NavMeshAgent>();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(point, out var hit, navMeshSampleRadius, filter)) return false;
            unit = Instantiate(prefab, hit.position, Quaternion.identity);
            unit.NetworkObject.Spawn();
            if (!unit.IsSpawned || !unit.Agent.isOnNavMesh) throw new InvalidOperationException("Minion is not on its agent NavMesh.");
            ApplyDifficulty(unit, wave, waveIndex);
            living.Add(unit);
            spawned.Add(unit);
            unit.Died += MinionDied;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (unit != null)
            {
                if (unit.IsSpawned) unit.NetworkObject.Despawn(true); else Destroy(unit.gameObject);
            }
            return false;
        }
    }
    private void ApplyDifficulty(Minion unit, Wave wave, int waveIndex)
    {
        bool adaptiveList = activeSpawnList != null && activeSpawnList.Difficulty == 0;
        int enemyLevel = adaptiveList ? encounterPlayerLevel : dungeon.GetEnemySpawnLevel(room, encounterDifficulty);
        unit.SetLevel(enemyLevel);
        // Universal list prefabs use level-one stats and scale to the player level.
        float spawnListScale = activeSpawnList != null
            ? (float)enemyLevel / Mathf.Clamp(activeSpawnList.Difficulty, 1, 10)
            : (float)enemyLevel / Mathf.Max(1, encounterDifficulty);
        float scale = Mathf.Max(0.01f, Difficulty.BaseMultiplier * spawnListScale * wave.Difficulty *
            (1 + Difficulty.PerRoom * Mathf.Max(0, room.RoomIndex - 1) + Difficulty.PerWave * waveIndex));
        unit.SetMaxHealth(Mathf.Max(1, unit.GetStat(MinionStat.MaxHealth) * scale * Difficulty.Health));
        unit.SetHealth(unit.GetStat(MinionStat.MaxHealth));
        unit.SetMaxChakra(unit.GetStat(MinionStat.MaxChakra) * scale * Difficulty.Chakra);
        unit.SetChakra(unit.GetStat(MinionStat.MaxChakra));
        unit.SetSpeed(unit.GetStat(MinionStat.Speed) * scale * Difficulty.Speed);
        unit.SetAttackSpeed(unit.GetStat(MinionStat.AttackSpeed) * scale * Difficulty.AttackSpeed);
        unit.SetArmor(unit.GetStat(MinionStat.Armor) * scale * Difficulty.Armor);
        unit.SetMagicResistance(unit.GetStat(MinionStat.MagicResistance) * scale * Difficulty.MagicResistance);
        foreach (var attack in unit.GetComponents<MinionAttack>()) attack.Damage *= scale * Difficulty.Damage;
    }
    private bool TrySpawnReward()
    {
        NetworkObject reward = null;
        try
        {
            NetworkObject prefab;
            if (activeSpawnEntries != null && activeSpawnEntries.Count > 0)
            {
                float totalWeight = 0;
                foreach (var entry in activeSpawnEntries)
                    totalWeight += entry.Weight;
                float choice = UnityEngine.Random.value * totalWeight;
                SpawnListAsset.Entry fallback = null;
                SpawnListAsset.Entry selected = null;
                foreach (var entry in activeSpawnEntries)
                {
                    fallback = entry;
                    choice -= entry.Weight;
                    if (choice <= 0)
                    {
                        selected = entry;
                        break;
                    }
                }
                selected ??= fallback;
                prefab = selected.Prefab.GetComponent<NetworkObject>();
            }
            else
            {
                prefab = RewardPrefabs[UnityEngine.Random.Range(0, RewardPrefabs.Count)];
            }
            reward = Instantiate(prefab, spawnPositions[UnityEngine.Random.Range(0, spawnPositions.Count)], Quaternion.identity);
            if (activeSpawnList != null && activeSpawnList.Difficulty == 0 && reward.TryGetComponent<AbilityBall>(out var ability))
                ability.Level = encounterPlayerLevel;
            reward.Spawn();
            if (!reward.IsSpawned) throw new InvalidOperationException("Reward was not spawned.");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (reward != null) { if (reward.IsSpawned) reward.Despawn(true); else Destroy(reward.gameObject); }
            return false;
        }
    }
    private void MinionDied(Minion unit)
    {
        if (!CanRun || !living.Remove(unit)) return;
        unit.Died -= MinionDied;
        OnMinionDied.Invoke(unit);
    }
    private void FinishEncounter()
    {
        if (!CanRun) return;
        completed = true;
        IsRunning = false;
        encounter = null;
        dungeon.CompleteEncounter(this);
        OnRoomCleared.Invoke();
    }
    private void AbortEncounter(string reason)
    {
        if (!CanRun) return;
        Debug.LogWarning($"{name}: encounter aborted. {reason}", this);
        ResetEncounter();
        retryAt = Time.time + 3;
        OnEncounterAborted.Invoke();
    }
    public void ResetEncounter()
    {
        // IsServer may already be reset during network shutdown. Only a former server
        // encounter can own spawned objects or a running coroutine requiring cleanup.
        if (Application.isPlaying && !CanRun && !IsRunning && spawned.Count == 0) return;
        if (encounter != null) StopCoroutine(encounter);
        encounter = null;
        foreach (var unit in spawned)
        {
            if (unit == null) continue;
            unit.Died -= MinionDied;
            if (unit.IsSpawned && unit.IsServer) unit.NetworkObject.Despawn(true);
        }
        spawned.Clear(); living.Clear();
        completed = false;
        IsRunning = false;
        CurrentWave = 0;
        if (dungeon != null) dungeon.CancelEncounter(this);
    }
    private void OnDisable()
    {
        ResetEncounter();
        occupants.Clear();
    }
}
