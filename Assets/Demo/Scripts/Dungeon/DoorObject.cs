using UnityEngine;
using System.Collections.Generic;

public class DoorObject : MonoBehaviour
{
    [HideInInspector]
    public RoomObject ConnectedRoom;

    [HideInInspector]
    public DoorObject ConnectedDoor;

    [Tooltip("Rooms reachable through this door. A junction door can lead to two route branches.")]
    public List<RoomObject> ConnectedRooms = new List<RoomObject>();

    [Tooltip("Matching door objects on the connected room branches.")]
    public List<DoorObject> ConnectedDoors = new List<DoorObject>();

    [Tooltip("Solid colliders that block passage while the door is locked. Defaults to child non-trigger colliders.")]
    [SerializeField] private Collider[] blockingColliders;
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string lockedParameter = "Locked";
    public bool IsLocked { get; private set; }

    // Called from the replicated dungeon state on every peer. No gameplay events run here.
    public void SetLocked(bool locked)
    {
        IsLocked = locked;
        if (blockingColliders == null || blockingColliders.Length == 0)
            blockingColliders = System.Array.FindAll(GetComponentsInChildren<Collider>(true), item => !item.isTrigger);
        foreach (var blocker in blockingColliders) if (blocker != null) blocker.enabled = locked;
        if (doorAnimator != null && !string.IsNullOrEmpty(lockedParameter)) doorAnimator.SetBool(lockedParameter, locked);
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Renderer[] cachedRenderers;
    private MaterialPropertyBlock propertyBlock;

    public void AddConnection(DoorObject connectedDoor, RoomObject connectedRoom)
    {
        if (connectedRoom != null)
        {
            if (ConnectedRooms == null)
                ConnectedRooms = new List<RoomObject>();
            if (!ConnectedRooms.Contains(connectedRoom))
                ConnectedRooms.Add(connectedRoom);
            if (ConnectedRoom == null)
                ConnectedRoom = connectedRoom;
        }

        if (connectedDoor != null)
        {
            if (ConnectedDoors == null)
                ConnectedDoors = new List<DoorObject>();
            if (!ConnectedDoors.Contains(connectedDoor))
                ConnectedDoors.Add(connectedDoor);
            if (ConnectedDoor == null)
                ConnectedDoor = connectedDoor;
        }
    }

    public void SetDebugColor(Color color)
    {
        if (cachedRenderers == null)
            cachedRenderers = GetComponentsInChildren<Renderer>(true);

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        foreach (Renderer targetRenderer in cachedRenderers)
        {
            if (targetRenderer == null)
                continue;

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
