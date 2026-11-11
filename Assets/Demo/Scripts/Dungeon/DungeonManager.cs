using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class DungeonManager : NetworkBehaviour
{
    [Min(1)]
    [Tooltip("Maximum number of rooms in the route, including the main room.")]
    public int RoomCount = 5;

    [Tooltip("Randomly selected starting room for the current route.")]
    public RoomObject MainRoom;

    [SerializeField]
    private List<RoomObject> selectedRooms = new List<RoomObject>();

    [SerializeField]
    private List<DoorObject> selectedDoors = new List<DoorObject>();

    public IReadOnlyList<RoomObject> SelectedRooms => selectedRooms;
    public IReadOnlyList<DoorObject> SelectedDoors => selectedDoors;
    [UnityEngine.Serialization.FormerlySerializedAs("ClearingIndex")]
    [SerializeField, Min(0)] private int previewClearingIndex;
    [Min(1)] public int RewardRoomInterval = 3;
    [Header("Spawn lists")]
    [Tooltip("Optional spawn lists for this map. If empty, assets are loaded from Resources/Spawners.")]
    [SerializeField] private List<SpawnListAsset> mapSpawnLists = new List<SpawnListAsset>();
    [Range(0f, 1f)]
    [Tooltip("Per-enemy chance for a difficult room to spawn one level above the party difficulty.")]
    public float DifficultRoomBonusLevelChance = 0.5f;
    private readonly NetworkVariable<int> clearance = new NetworkVariable<int>(0);
    private readonly NetworkVariable<int> encounterRoom = new NetworkVariable<int>(-1);
    private readonly NetworkVariable<int> rewardInterval = new NetworkVariable<int>(3);
    private NetworkList<int> route = new NetworkList<int>();
    private readonly List<RoomObject> roomCatalog = new List<RoomObject>();
    private readonly List<SpawnListAsset> resourceSpawnLists = new List<SpawnListAsset>();
    private bool presentationDirty;
    private bool runInitializationPending;
    public IReadOnlyList<SpawnListAsset> SpawnLists =>
        mapSpawnLists != null && mapSpawnLists.Count > 0 ? mapSpawnLists : resourceSpawnLists;
    public int ClearingIndex => IsSpawned ? clearance.Value : previewClearingIndex;
    public int ActiveEncounterIndex => IsSpawned ? encounterRoom.Value : -1;
    public bool HasServerAuthority => IsSpawned && IsServer;

    private static readonly Color UnselectedDoorColor = Color.red;
    private static readonly Color CurrentDoorColor = Color.blue;
    private static readonly Color UpcomingDoorColor = Color.green;
    private static readonly Color ClearedDoorColor = Color.yellow;

    public override void OnNetworkSpawn()
    {
        BuildCatalog();
        route.OnListChanged += RouteChanged;
        clearance.OnValueChanged += StateChanged;
        encounterRoom.OnValueChanged += StateChanged;
        rewardInterval.OnValueChanged += StateChanged;
        if (IsServer) runInitializationPending = true;
        else ApplyReplicatedRoute();
    }

    public override void OnNetworkDespawn()
    {
        route.OnListChanged -= RouteChanged;
        clearance.OnValueChanged -= StateChanged;
        encounterRoom.OnValueChanged -= StateChanged;
        rewardInterval.OnValueChanged -= StateChanged;
        foreach (RoomObject room in roomCatalog)
            if (room != null)
                room.GetSpawnManager()?.ResetEncounter();
    }

    private void BuildCatalog()
    {
        LoadResourceSpawnLists();
        roomCatalog.Clear();
        foreach (var room in FindObjectsByType<RoomObject>(FindObjectsInactive.Include))
            if (room.gameObject.scene == gameObject.scene) roomCatalog.Add(room);
        // Scene objects have the same hierarchy on all peers, unlike instance IDs.
        roomCatalog.Sort((a, b) => string.CompareOrdinal(SceneKey(a.transform), SceneKey(b.transform)));
    }

    private void LoadResourceSpawnLists()
    {
        resourceSpawnLists.Clear();
        resourceSpawnLists.AddRange(Resources.LoadAll<SpawnListAsset>("Spawners"));
    }

    public int GetEnemySpawnLevel(RoomObject room, int partyDifficulty)
    {
        int baseLevel = Mathf.Clamp(partyDifficulty, 1, 10);
        if (room == null)
            return baseLevel;

        switch (room.SpawnDifficulty)
        {
            case RoomSpawnDifficulty.Easy:
                return Random.Range(1, baseLevel + 1);
            case RoomSpawnDifficulty.Difficult:
                if (baseLevel < 10 && Random.value < Mathf.Clamp01(DifficultRoomBonusLevelChance))
                    return baseLevel + 1;
                return baseLevel;
            default:
                return baseLevel;
        }
    }

    private static string SceneKey(Transform item)
    {
        string key = item.GetSiblingIndex().ToString("D6");
        while (item.parent != null) { item = item.parent; key = item.GetSiblingIndex().ToString("D6") + "/" + key; }
        return key;
    }

    private void RouteChanged(NetworkListEvent<int> change) => presentationDirty = true;
    private void StateChanged(int previous, int current) => presentationDirty = true;
    private void LateUpdate()
    {
        if (!IsSpawned) return;

        // Scene objects can receive OnNetworkSpawn before other scene components
        // finish Start(). Build the host run on the first LateUpdate so their
        // room/door references and runtime setup are ready.
        if (IsServer && runInitializationPending)
        {
            runInitializationPending = false;
            BuildCatalog();
            if (roomCatalog.Count == 0)
            {
                Debug.LogError($"{name}: no RoomObject instances were found in scene '{gameObject.scene.name}'. The dungeon run was not generated.", this);
                return;
            }

            RebuildRoute();
            if (selectedRooms.Count == 0)
                Debug.LogError($"{name}: route generation returned no rooms in scene '{gameObject.scene.name}'. Check the room catalog and connections.", this);
            return;
        }

        if (!presentationDirty) return;
        presentationDirty = false;
        if (!IsServer) ApplyReplicatedRoute();
        else RefreshDoorStates();
    }

    private void ApplyReplicatedRoute()
    {
        selectedRooms.Clear();
        foreach (int index in route)
            if (index >= 0 && index < roomCatalog.Count) selectedRooms.Add(roomCatalog[index]);
        MainRoom = selectedRooms.Count > 0 ? selectedRooms[0] : null;
        UpdatePlayerSpawnPoints();
        DeactivateRoomsOutsideRoute(roomCatalog);
        RefreshDoorStates();
    }

    private void PublishRoute()
    {
        if (!HasServerAuthority) return;
        route.Clear();
        foreach (RoomObject room in selectedRooms) route.Add(roomCatalog.IndexOf(room));
    }

    public bool TryBeginEncounter(DungeonSpawnManager spawner)
    {
        if (!HasServerAuthority || spawner == null || ActiveEncounterIndex >= 0 ||
            spawner.Room == null || spawner.Room.RoomIndex != ClearingIndex + 1 ||
            !selectedRooms.Contains(spawner.Room)) return false;
        encounterRoom.Value = spawner.Room.RoomIndex;
        RefreshDoorStates();
        return true;
    }

    public void CompleteEncounter(DungeonSpawnManager spawner)
    {
        if (!HasServerAuthority || spawner == null || spawner.Room == null ||
            ActiveEncounterIndex != spawner.Room.RoomIndex) return;
        clearance.Value = ActiveEncounterIndex;
        encounterRoom.Value = -1;
        RefreshDoorStates();
    }

    public void CancelEncounter(DungeonSpawnManager spawner)
    {
        if (!HasServerAuthority || spawner == null || spawner.Room == null || ActiveEncounterIndex != spawner.Room.RoomIndex) return;
        encounterRoom.Value = -1;
        RefreshDoorStates();
    }

    [ContextMenu("Rebuild Route")]
    public void RebuildRoute()
    {
        if (Application.isPlaying && !HasServerAuthority) return;
        if (Application.isPlaying && ActiveEncounterIndex >= 0) return;
        if (!Application.isPlaying) BuildCatalog();
        foreach (var room in roomCatalog) room.GetSpawnManager()?.ResetEncounter();
        if (HasServerAuthority) rewardInterval.Value = Mathf.Max(1, RewardRoomInterval);
        List<RoomObject> rooms = new List<RoomObject>(roomCatalog);
        Dictionary<RoomObject, List<RoomObject>> adjacency = BuildAdjacency(rooms);

        selectedRooms.Clear();

        if (rooms.Count == 0 || RoomCount < 1)
        {
            MainRoom = null;
            UpdatePlayerSpawnPoints();
            if (IsSpawned) clearance.Value = 0; else previewClearingIndex = 0;
            PublishRoute();
            RefreshDoorStates();
            return;
        }

        MainRoom = rooms[Random.Range(0, rooms.Count)];
        if (IsSpawned) clearance.Value = 0; else previewClearingIndex = 0;

        List<RoomObject> currentPath = new List<RoomObject> { MainRoom };
        HashSet<RoomObject> visited = new HashSet<RoomObject> { MainRoom };
        SearchLongestRoute(MainRoom, adjacency, currentPath, visited);
        UpdatePlayerSpawnPoints();
        PublishRoute();
        DeactivateRoomsOutsideRoute(rooms);
        RefreshDoorStates();
    }

    private void UpdatePlayerSpawnPoints()
    {
        Transform container = null;
        if (MainRoom != null)
        {
            container = MainRoom.transform.Find("spawners");
            // Existing scene rooms keep RoomObject and spawners as siblings.
            if (container == null && MainRoom.transform.parent != null &&
                MainRoom.transform.parent.GetComponentsInChildren<RoomObject>(true).Length == 1)
                container = MainRoom.transform.parent.Find("spawners");
        }

        var points = new List<Transform>();
        if (container != null)
            foreach (Transform child in container) points.Add(child);

        if (MainRoom != null && points.Count == 0)
            Debug.LogWarning($"{MainRoom.name}: the main room needs a spawners child with spawn markers.", MainRoom);

        // Both GameManager variants exist in the project. Clients only apply the
        // server-selected main room; they never generate or select a separate route.
        var demoManager = GonzoDemo.GameManager.Instance != null
            ? GonzoDemo.GameManager.Instance : FindFirstObjectByType<GonzoDemo.GameManager>(FindObjectsInactive.Include);
        var coreManager = Blocks.Gameplay.Core.GameManager.Instance != null
            ? Blocks.Gameplay.Core.GameManager.Instance : FindFirstObjectByType<Blocks.Gameplay.Core.GameManager>(FindObjectsInactive.Include);
        if (demoManager != null) demoManager.SetSpawnPoints(points);
        if (coreManager != null) coreManager.SetSpawnPoints(points);
    }

    private void DeactivateRoomsOutsideRoute(List<RoomObject> rooms)
    {
        if (!Application.isPlaying)
            return;

        HashSet<RoomObject> selectedRoomSet = new HashSet<RoomObject>(selectedRooms);
        Dictionary<GameObject, bool> parentAvailability = new Dictionary<GameObject, bool>();
        foreach (RoomObject room in rooms)
        {
            if (room == null)
                continue;

            Transform parent = room.transform.parent;
            GameObject roomParent = parent != null ? parent.gameObject : room.gameObject;
            bool isSelected = selectedRoomSet.Contains(room);
            if (!parentAvailability.TryGetValue(roomParent, out bool hasSelectedRoom) ||
                (!hasSelectedRoom && isSelected))
            {
                parentAvailability[roomParent] = isSelected;
            }
        }

        foreach (KeyValuePair<GameObject, bool> parent in parentAvailability)
        {
            // Keep network scene objects (including this manager) alive for late joins.
            if (parent.Key != null && parent.Key.GetComponentInChildren<NetworkObject>(true) == null)
                parent.Key.SetActive(parent.Value);
        }
    }

    public void SetClearingIndex(int index)
    {
        if (Application.isPlaying && (!HasServerAuthority || ActiveEncounterIndex >= 0)) return;
        int value = selectedRooms.Count == 0
            ? 0
            : Mathf.Clamp(index, 0, selectedRooms.Count - 1);
        if (IsSpawned) clearance.Value = value; else previewClearingIndex = value;
        RefreshDoorStates();
    }

    public void AdvanceClearingIndex()
    {
        SetClearingIndex(ClearingIndex + 1);
    }

    public void RewindClearingIndex()
    {
        SetClearingIndex(ClearingIndex - 1);
    }

    public void RefreshDoorStates()
    {
        if (Application.isPlaying && !IsSpawned) return;
        if (!Application.isPlaying) BuildCatalog();
        RoomObject[] allRooms = roomCatalog.ToArray();
        foreach (RoomObject room in allRooms)
        {
            if (room != null)
                room.RoomIndex = -1;
        }

        Dictionary<DoorObject, int> selectedDoorIndexes = new Dictionary<DoorObject, int>();
        HashSet<RoomObject> selectedRoomSet = new HashSet<RoomObject>(selectedRooms);
        selectedDoors.Clear();
        for (int i = 0; i < selectedRooms.Count; i++)
        {
            RoomObject room = selectedRooms[i];
            if (room == null)
                continue;

            room.RoomIndex = i;
            room.GetSpawnManager()?.Bind(this, room, i > 0 && i % Mathf.Max(1, IsSpawned ? rewardInterval.Value : RewardRoomInterval) == 0);
            if (room.Doors == null)
                continue;

            foreach (DoorObject door in room.Doors)
            {
                if (door != null && !selectedDoorIndexes.ContainsKey(door))
                {
                    selectedDoorIndexes.Add(door, i);
                    selectedDoors.Add(door);
                }
            }
        }

        HashSet<DoorObject> doorsToUnselectedRooms = new HashSet<DoorObject>();
        foreach (RoomObject room in selectedRooms)
        {
            if (room == null)
                continue;

            if (room.Doors == null)
                continue;

            foreach (DoorObject door in room.Doors)
            {
                if (door == null)
                    continue;

                HashSet<RoomObject> destinationRooms = new HashSet<RoomObject>();
                AddDoorDestinations(door, allRooms, destinationRooms);
                if (destinationRooms.Count > 0)
                {
                    bool hasSelectedDestination = false;
                    foreach (RoomObject destinationRoom in destinationRooms)
                        hasSelectedDestination |= selectedRoomSet.Contains(destinationRoom);

                    if (!hasSelectedDestination)
                        doorsToUnselectedRooms.Add(door);

                    continue;
                }

                foreach (RoomObject adjacentRoom in allRooms)
                {
                    if (adjacentRoom != null && !selectedRoomSet.Contains(adjacentRoom) &&
                        adjacentRoom.Doors != null && adjacentRoom.Doors.Contains(door))
                    {
                        doorsToUnselectedRooms.Add(door);
                        break;
                    }
                }
            }
        }

        DoorObject[] allDoors = FindObjectsByType<DoorObject>(FindObjectsInactive.Include);
        foreach (DoorObject door in allDoors)
        {
            if (door == null || door.gameObject.scene != gameObject.scene)
                continue;

            bool routeDoor = selectedDoorIndexes.TryGetValue(door, out int doorIndex) && !doorsToUnselectedRooms.Contains(door);
            if (routeDoor)
            {
                var destinations = new HashSet<RoomObject>();
                AddDoorDestinations(door, allRooms, destinations);
                foreach (var destination in destinations)
                    if (destination != null && destination.RoomIndex >= 0) doorIndex = Mathf.Min(doorIndex, destination.RoomIndex);
            }
            // Route edges ahead of the next room stay closed; a live encounter seals the dungeon.
            door.SetLocked(!routeDoor || ActiveEncounterIndex >= 0 || doorIndex > ClearingIndex);

            if (!selectedDoorIndexes.TryGetValue(door, out int roomIndex) ||
                doorsToUnselectedRooms.Contains(door))
            {
                door.SetDebugColor(UnselectedDoorColor);
            }
            else if (ClearingIndex == roomIndex)
            {
                door.SetDebugColor(CurrentDoorColor);
            }
            else if (ClearingIndex < roomIndex)
            {
                door.SetDebugColor(UpcomingDoorColor);
            }
            else
            {
                door.SetDebugColor(ClearedDoorColor);
            }
        }
    }

    private Dictionary<RoomObject, List<RoomObject>> BuildAdjacency(List<RoomObject> rooms)
    {
        Dictionary<RoomObject, List<RoomObject>> adjacency = new Dictionary<RoomObject, List<RoomObject>>();
        foreach (RoomObject room in rooms)
        {
            if (room != null && !adjacency.ContainsKey(room))
                adjacency.Add(room, new List<RoomObject>());
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomObject firstRoom = rooms[i];
            if (firstRoom == null || !adjacency.ContainsKey(firstRoom))
                continue;

            for (int j = i + 1; j < rooms.Count; j++)
            {
                RoomObject secondRoom = rooms[j];
                if (secondRoom == null || !adjacency.ContainsKey(secondRoom) ||
                    !AreAdjacent(firstRoom, secondRoom))
                    continue;

                adjacency[firstRoom].Add(secondRoom);
                adjacency[secondRoom].Add(firstRoom);
            }
        }

        return adjacency;
    }

    private static bool AreAdjacent(RoomObject firstRoom, RoomObject secondRoom)
    {
        bool listedAsConnected =
            (firstRoom.ConnectedRooms != null && firstRoom.ConnectedRooms.Contains(secondRoom)) ||
            (secondRoom.ConnectedRooms != null && secondRoom.ConnectedRooms.Contains(firstRoom));
        if (listedAsConnected)
            return true;

        if (firstRoom.Doors != null)
        {
            foreach (DoorObject firstDoor in firstRoom.Doors)
            {
                if (firstDoor == null)
                    continue;

                if (DoorConnectsToRoom(firstDoor, secondRoom))
                    return true;

                if (secondRoom.Doors != null && secondRoom.Doors.Contains(firstDoor))
                    return true;
            }
        }

        if (secondRoom.Doors != null)
        {
            foreach (DoorObject secondDoor in secondRoom.Doors)
            {
                if (secondDoor != null && DoorConnectsToRoom(secondDoor, firstRoom))
                    return true;
            }
        }

        return false;
    }

    private static bool DoorConnectsToRoom(DoorObject door, RoomObject room)
    {
        if (door.ConnectedRoom == room)
            return true;

        if (door.ConnectedRooms != null && door.ConnectedRooms.Contains(room))
            return true;

        if (DoorConnectsToDoorInRoom(door.ConnectedDoor, room))
            return true;

        if (door.ConnectedDoors != null)
        {
            foreach (DoorObject connectedDoor in door.ConnectedDoors)
            {
                if (DoorConnectsToDoorInRoom(connectedDoor, room))
                    return true;
            }
        }

        return false;
    }

    private static bool DoorConnectsToDoorInRoom(DoorObject door, RoomObject room)
    {
        if (door == null)
            return false;

        if (door.GetComponentInParent<RoomObject>() == room)
            return true;

        return room.Doors != null && room.Doors.Contains(door);
    }

    private static void AddDoorDestinations(
        DoorObject door,
        RoomObject[] allRooms,
        HashSet<RoomObject> destinations)
    {
        if (door.ConnectedRoom != null)
            destinations.Add(door.ConnectedRoom);

        if (door.ConnectedRooms != null)
        {
            foreach (RoomObject room in door.ConnectedRooms)
            {
                if (room != null)
                    destinations.Add(room);
            }
        }

        AddDoorDestination(door.ConnectedDoor, allRooms, destinations);
        if (door.ConnectedDoors != null)
        {
            foreach (DoorObject connectedDoor in door.ConnectedDoors)
                AddDoorDestination(connectedDoor, allRooms, destinations);
        }
    }

    private static void AddDoorDestination(
        DoorObject connectedDoor,
        RoomObject[] allRooms,
        HashSet<RoomObject> destinations)
    {
        if (connectedDoor == null)
            return;

        RoomObject destination = connectedDoor.GetComponentInParent<RoomObject>();
        if (destination != null)
        {
            destinations.Add(destination);
            return;
        }

        foreach (RoomObject room in allRooms)
        {
            if (room != null && room.Doors != null && room.Doors.Contains(connectedDoor))
            {
                destinations.Add(room);
                return;
            }
        }
    }

    private void SearchLongestRoute(
        RoomObject currentRoom,
        Dictionary<RoomObject, List<RoomObject>> adjacency,
        List<RoomObject> currentPath,
        HashSet<RoomObject> visited)
    {
        if (selectedRooms.Count >= RoomCount)
            return;

        if (currentPath.Count > selectedRooms.Count)
            selectedRooms = new List<RoomObject>(currentPath);

        if (selectedRooms.Count >= RoomCount)
            return;

        if (currentPath.Count >= RoomCount || !adjacency.TryGetValue(currentRoom, out List<RoomObject> neighbors))
            return;

        List<RoomObject> shuffledNeighbors = new List<RoomObject>(neighbors);
        for (int i = shuffledNeighbors.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            RoomObject temp = shuffledNeighbors[i];
            shuffledNeighbors[i] = shuffledNeighbors[swapIndex];
            shuffledNeighbors[swapIndex] = temp;
        }

        foreach (RoomObject nextRoom in shuffledNeighbors)
        {
            if (visited.Contains(nextRoom) || ReconnectsToEarlierRoute(nextRoom, currentRoom, adjacency, visited))
                continue;

            visited.Add(nextRoom);
            currentPath.Add(nextRoom);
            SearchLongestRoute(nextRoom, adjacency, currentPath, visited);
            currentPath.RemoveAt(currentPath.Count - 1);
            visited.Remove(nextRoom);
        }
    }

    private static bool ReconnectsToEarlierRoute(
        RoomObject candidate,
        RoomObject currentRoom,
        Dictionary<RoomObject, List<RoomObject>> adjacency,
        HashSet<RoomObject> visited)
    {
        if (!adjacency.TryGetValue(candidate, out List<RoomObject> neighbors))
            return false;

        foreach (RoomObject neighbor in neighbors)
        {
            if (neighbor != currentRoom && visited.Contains(neighbor))
                return true;
        }

        return false;
    }
}
