using System.Collections.Generic;
using UnityEngine;

public class DungeonManager : MonoBehaviour
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
    public int ClearingIndex = 0;

    private static readonly Color UnselectedDoorColor = Color.red;
    private static readonly Color CurrentDoorColor = Color.blue;
    private static readonly Color UpcomingDoorColor = Color.green;
    private static readonly Color ClearedDoorColor = Color.yellow;

    private void Start()
    {
        RebuildRoute();
    }

    [ContextMenu("Rebuild Route")]
    public void RebuildRoute()
    {
        RoomObject[] sceneRooms = FindObjectsOfType<RoomObject>(true);
        List<RoomObject> rooms = new List<RoomObject>(sceneRooms);
        Dictionary<RoomObject, List<RoomObject>> adjacency = BuildAdjacency(rooms);

        selectedRooms.Clear();

        if (rooms.Count == 0 || RoomCount < 1)
        {
            MainRoom = null;
            ClearingIndex = 0;
            RefreshDoorStates();
            return;
        }

        MainRoom = rooms[Random.Range(0, rooms.Count)];
        ClearingIndex = 0;

        List<RoomObject> currentPath = new List<RoomObject> { MainRoom };
        HashSet<RoomObject> visited = new HashSet<RoomObject> { MainRoom };
        SearchLongestRoute(MainRoom, adjacency, currentPath, visited);
        DeactivateRoomsOutsideRoute(rooms);
        RefreshDoorStates();
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
            if (parent.Key != null)
                parent.Key.SetActive(parent.Value);
        }
    }

    public void SetClearingIndex(int index)
    {
        ClearingIndex = selectedRooms.Count == 0
            ? 0
            : Mathf.Clamp(index, 0, selectedRooms.Count - 1);
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
        RoomObject[] allRooms = FindObjectsOfType<RoomObject>(true);
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

        DoorObject[] allDoors = FindObjectsOfType<DoorObject>(true);
        foreach (DoorObject door in allDoors)
        {
            if (door == null)
                continue;

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
