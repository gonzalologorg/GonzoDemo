using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

public enum RoomSpawnDifficulty
{
    Easy,
    Common,
    Difficult
}

public class RoomObject : MonoBehaviour
{
    public List<DoorObject> Doors = new List<DoorObject>();
    public List<RoomObject> ConnectedRooms = new List<RoomObject>();
    [FormerlySerializedAs("ClearingIndex")]
    [Tooltip("Position of this room in the DungeonManager route. -1 means it is not selected.")]
    public int RoomIndex = -1;

    [Tooltip("Controls the enemy level range used in this room.")]
    public RoomSpawnDifficulty SpawnDifficulty = RoomSpawnDifficulty.Common;

    [Tooltip("Encounter controller for this room. Can be on a sibling under the room container.")]
    public DungeonSpawnManager SpawnManager;

    public DungeonSpawnManager GetSpawnManager()
    {
        if (SpawnManager != null) return SpawnManager;
        var result = GetComponentInChildren<DungeonSpawnManager>(true);
        if (result != null) return result;
        // Existing rooms store RoomObject and spawners as siblings under one room container.
        var parent = transform.parent;
        if (parent != null && parent.GetComponentsInChildren<RoomObject>(true).Length == 1)
            return parent.GetComponentInChildren<DungeonSpawnManager>(true);
        return null;
    }
}
