using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

public class RoomObject : MonoBehaviour
{
    public List<DoorObject> Doors = new List<DoorObject>();
    public List<RoomObject> ConnectedRooms = new List<RoomObject>();
    [FormerlySerializedAs("ClearingIndex")]
    [Tooltip("Position of this room in the DungeonManager route. -1 means it is not selected.")]
    public int RoomIndex = -1;
}
