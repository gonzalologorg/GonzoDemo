using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RoomObject))]
public class RoomObjectEditor : Editor
{
    private bool selectingDoors = false;
    private bool selectingRooms = false;
    private bool assigningDoorDestination = false;
    private DoorObject pendingDestinationDoor;
    private int assignedDestinationCount;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button(selectingDoors
            ? "Stop Selecting Doors"
            : "Select Doors"))
        {
            selectingDoors = !selectingDoors;
            selectingRooms = false;
            assigningDoorDestination = false;
            pendingDestinationDoor = null;
            assignedDestinationCount = 0;

            SceneView.RepaintAll();
        }

        if (GUILayout.Button(selectingRooms
            ? "Stop Selecting Rooms"
            : "Select Connected Rooms"))
        {
            selectingRooms = !selectingRooms;
            selectingDoors = false;
            assigningDoorDestination = false;
            pendingDestinationDoor = null;
            assignedDestinationCount = 0;

            SceneView.RepaintAll();
        }

        if (GUILayout.Button(assigningDoorDestination
            ? "Stop Assigning Door Destination"
            : "Assign Door Connections"))
        {
            assigningDoorDestination = !assigningDoorDestination;
            selectingDoors = false;
            selectingRooms = false;
            pendingDestinationDoor = null;
            assignedDestinationCount = 0;

            SceneView.RepaintAll();
        }

        if (assigningDoorDestination)
        {
            EditorGUILayout.HelpBox(
                "Click a door in this room, then click up to two destination doors. Click the source door again to finish after one destination.",
                MessageType.Info);
        }
    }

    private void OnSceneGUI()
    {
        RoomObject room = (RoomObject)target;
        DrawRoomConnections(room);

        if (!selectingDoors && !selectingRooms && !assigningDoorDestination)
            return;

        Event e = Event.current;

        // Evitar que Unity cambie la selección normal.
        HandleUtility.AddDefaultControl(
            GUIUtility.GetControlID(FocusType.Passive)
        );

        if (assigningDoorDestination && pendingDestinationDoor != null)
            Handles.Label(
                pendingDestinationDoor.transform.position,
                "Select connected door (" + assignedDestinationCount + "/2)");

        if (e.type != EventType.MouseDown || e.button != 0 || e.alt)
            return;

        GameObject clicked = HandleUtility.PickGameObject(
            e.mousePosition,
            false
        );

        if (clicked == null)
            return;

        if (assigningDoorDestination)
        {
            if (pendingDestinationDoor == null)
            {
                DoorObject door = clicked.GetComponentInParent<DoorObject>();
                if (door != null)
                {
                    assignedDestinationCount = CountDestinations(door);
                    if (assignedDestinationCount >= 2)
                    {
                        Debug.LogWarning("This door already has two destination rooms.", door);
                        e.Use();
                        return;
                    }

                    Undo.RecordObject(room, "Add Door");
                    if (!room.Doors.Contains(door))
                        room.Doors.Add(door);
                    EditorUtility.SetDirty(room);
                    pendingDestinationDoor = door;
                    SceneView.RepaintAll();
                }
            }
            else
            {
                DoorObject connectedDoor = clicked.GetComponentInParent<DoorObject>();
                if (connectedDoor == pendingDestinationDoor)
                {
                    pendingDestinationDoor = null;
                    assignedDestinationCount = 0;
                    SceneView.RepaintAll();
                    e.Use();
                    return;
                }

                RoomObject connected = FindRoomForDoor(connectedDoor);

                if (connectedDoor != null && connected != null && connected != room &&
                    connectedDoor != pendingDestinationDoor)
                {
                    Undo.RecordObject(pendingDestinationDoor, "Set Door Destination");
                    pendingDestinationDoor.AddConnection(connectedDoor, connected);
                    EditorUtility.SetDirty(pendingDestinationDoor);

                    Undo.RecordObject(connectedDoor, "Set Door Destination");
                    connectedDoor.AddConnection(pendingDestinationDoor, room);
                    EditorUtility.SetDirty(connectedDoor);

                    Undo.RecordObject(room, "Add Connected Room");
                    if (!room.ConnectedRooms.Contains(connected))
                        room.ConnectedRooms.Add(connected);
                    if (!room.Doors.Contains(pendingDestinationDoor))
                        room.Doors.Add(pendingDestinationDoor);
                    EditorUtility.SetDirty(room);

                    Undo.RecordObject(connected, "Add Connected Room");
                    if (!connected.ConnectedRooms.Contains(room))
                        connected.ConnectedRooms.Add(room);
                    if (!connected.Doors.Contains(connectedDoor))
                        connected.Doors.Add(connectedDoor);
                    EditorUtility.SetDirty(connected);

                    assignedDestinationCount = CountDestinations(pendingDestinationDoor);
                    if (assignedDestinationCount >= 2)
                    {
                        pendingDestinationDoor = null;
                        assignedDestinationCount = 0;
                    }

                    RefreshDungeonManagers();
                    SceneView.RepaintAll();
                }
            }
        }
        else if (selectingDoors)
        {
            DoorObject door = clicked.GetComponentInParent<DoorObject>();

            if (door != null && !room.Doors.Contains(door))
            {
                Undo.RecordObject(room, "Add Door");
                room.Doors.Add(door);
                EditorUtility.SetDirty(room);
                RefreshDungeonManagers();
            }
        }
        else if (selectingRooms)
        {
            RoomObject connected = clicked.GetComponentInParent<RoomObject>();

            if (connected != null &&
                connected != room &&
                !room.ConnectedRooms.Contains(connected))
            {
                Undo.RecordObject(room, "Add Connected Room");
                room.ConnectedRooms.Add(connected);
                EditorUtility.SetDirty(room);
                RefreshDungeonManagers();
            }
        }

        e.Use();
    }

    private static void DrawRoomConnections(RoomObject room)
    {
        if (room == null)
            return;

        using (new Handles.DrawingScope(Color.green))
        {
            if (room.Doors != null)
            {
                foreach (DoorObject door in room.Doors)
                {
                    if (door != null)
                        Handles.DrawLine(room.transform.position, door.transform.position);
                }
            }

            if (room.Doors == null)
                return;

            HashSet<DoorObject> drawnDoors = new HashSet<DoorObject>();
            foreach (DoorObject door in room.Doors)
            {
                if (door == null)
                    continue;

                if (door.ConnectedDoor != null && drawnDoors.Add(door.ConnectedDoor))
                    Handles.DrawLine(door.transform.position, door.ConnectedDoor.transform.position);

                if (door.ConnectedDoors == null)
                    continue;

                foreach (DoorObject connectedDoor in door.ConnectedDoors)
                {
                    if (connectedDoor != null && drawnDoors.Add(connectedDoor))
                        Handles.DrawLine(door.transform.position, connectedDoor.transform.position);
                }
            }
        }
    }

    private static RoomObject FindRoomForDoor(DoorObject door)
    {
        if (door == null)
            return null;

        RoomObject parentRoom = door.GetComponentInParent<RoomObject>();
        if (parentRoom != null)
            return parentRoom;

        foreach (RoomObject room in Object.FindObjectsOfType<RoomObject>(true))
        {
            if (room.Doors != null && room.Doors.Contains(door))
                return room;
        }

        return null;
    }

    private static int CountDestinations(DoorObject door)
    {
        HashSet<RoomObject> destinations = new HashSet<RoomObject>();
        if (door.ConnectedRoom != null)
            destinations.Add(door.ConnectedRoom);
        if (door.ConnectedRooms != null)
        {
            foreach (RoomObject connectedRoom in door.ConnectedRooms)
            {
                if (connectedRoom != null)
                    destinations.Add(connectedRoom);
            }
        }

        if (door.ConnectedDoor != null)
        {
            RoomObject connectedRoom = FindRoomForDoor(door.ConnectedDoor);
            if (connectedRoom != null)
                destinations.Add(connectedRoom);
        }
        if (door.ConnectedDoors != null)
        {
            foreach (DoorObject connectedDoor in door.ConnectedDoors)
            {
                RoomObject connectedRoom = FindRoomForDoor(connectedDoor);
                if (connectedRoom != null)
                    destinations.Add(connectedRoom);
            }
        }

        return destinations.Count;
    }

    private static void RefreshDungeonManagers()
    {
        foreach (DungeonManager manager in Object.FindObjectsOfType<DungeonManager>(true))
            manager.RefreshDoorStates();
    }
}
