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
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 2;
    [Header("Encounters")]
    public List<SpawnPool> Pools = new List<SpawnPool>();
    public List<Wave> Waves = new List<Wave> { new Wave() };
    public DifficultySettings Difficulty = new DifficultySettings();
    [Header("Reward room (assigned by DungeonManager)")]
    [SerializeField] private bool isRewardRoom;
    [Tooltip("One randomly selected network prefab is spawned instead of waves.")]
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
            if (RewardPrefabs == null || RewardPrefabs.Count == 0 || RewardPrefabs.Exists(prefab => prefab == null))
                error = "Configure valid network reward prefabs.";
            else if (RewardPrefabs.Exists(prefab => !dungeon.NetworkManager.NetworkConfig.Prefabs.Contains(prefab.gameObject)))
                error = "Register every reward prefab in NetworkPrefabs.";
            return error == null;
        }
        if (Difficulty == null || Waves == null || Waves.Count == 0) { error = "Configure difficulty and at least one wave."; return false; }
        foreach (var wave in Waves)
        {
            if (wave == null || wave.Count < 1 || wave.PoolIndex < 0 || Pools == null || wave.PoolIndex >= Pools.Count)
            { error = "A wave has an invalid count or pool index."; return false; }
            var pool = Pools[wave.PoolIndex];
            if (pool == null || pool.Entries == null || !pool.Entries.Exists(entry => entry != null && entry.Prefab != null && entry.Weight > 0))
            { error = "A wave references an empty pool."; return false; }
            foreach (var entry in pool.Entries)
                if (entry != null && entry.Prefab != null && entry.Weight > 0 &&
                    !dungeon.NetworkManager.NetworkConfig.Prefabs.Contains(entry.Prefab.gameObject))
                { error = "Register every minion prefab in NetworkPrefabs."; return false; }
        }
        return true;
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
            var prefab = SelectPrefab(Pools[wave.PoolIndex]);
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
        float scale = Mathf.Max(0.01f, Difficulty.BaseMultiplier * wave.Difficulty *
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
            var prefab = RewardPrefabs[UnityEngine.Random.Range(0, RewardPrefabs.Count)];
            reward = Instantiate(prefab, spawnPositions[UnityEngine.Random.Range(0, spawnPositions.Count)], Quaternion.identity);
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
