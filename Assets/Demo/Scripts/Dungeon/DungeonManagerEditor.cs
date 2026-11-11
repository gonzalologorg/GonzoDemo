using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DungeonManager))]
public class DungeonManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DungeonManager manager = (DungeonManager)target;
        if (manager.GetComponent<Unity.Netcode.NetworkObject>() == null)
        {
            EditorGUILayout.HelpBox("This existing scene object needs a NetworkObject to run on the server and replicate dungeon state.", MessageType.Error);
            if (!Application.isPlaying && GUILayout.Button("Add required NetworkObject"))
                Undo.AddComponent<Unity.Netcode.NetworkObject>(manager.gameObject);
        }
        bool changed = DrawDefaultInspector();
        if (changed)
        {
            manager.SetClearingIndex(manager.ClearingIndex);
            EditorUtility.SetDirty(manager);
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(Application.isPlaying && (!manager.HasServerAuthority || manager.ActiveEncounterIndex >= 0)))
        if (GUILayout.Button("Generate Random Route"))
        {
            Undo.RecordObject(manager, "Rebuild Dungeon Route");
            manager.RebuildRoute();
            EditorUtility.SetDirty(manager);
            SceneView.RepaintAll();
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Runtime Clearing Index",
                manager.ClearingIndex.ToString());

            using (new EditorGUI.DisabledScope(manager.SelectedRooms.Count == 0 || !manager.HasServerAuthority || manager.ActiveEncounterIndex >= 0))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Previous Room"))
                    manager.RewindClearingIndex();
                if (GUILayout.Button("Next Room"))
                    manager.AdvanceClearingIndex();
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    private void OnSceneGUI()
    {
        DungeonManager manager = (DungeonManager)target;
        if (manager.SelectedRooms == null || manager.SelectedRooms.Count < 2)
            return;

        Vector3[] routePoints = new Vector3[manager.SelectedRooms.Count];
        for (int i = 0; i < manager.SelectedRooms.Count; i++)
        {
            RoomObject room = manager.SelectedRooms[i];
            if (room == null)
                return;

            routePoints[i] = room.transform.position;
        }

        using (new Handles.DrawingScope(Color.green))
        {
            Handles.DrawAAPolyLine(4f, routePoints);
            for (int i = 0; i < routePoints.Length; i++)
                Handles.Label(routePoints[i], (i + 1).ToString());
        }
    }
}
