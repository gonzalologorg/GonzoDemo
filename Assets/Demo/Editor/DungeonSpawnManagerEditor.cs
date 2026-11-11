using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DungeonSpawnManager))]
public class DungeonSpawnManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "isRewardRoom");
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("isRewardRoom"));
        serializedObject.ApplyModifiedProperties();
        var manager = (DungeonSpawnManager)target;
        EditorGUILayout.HelpBox("Waves: one entry per wave. DungeonManager selects a compatible SpawnList assigned to this map, " +
            "or loads from Resources/Spawners when the map list is empty. " +
            "If no SpawnList matches, PoolIndex selects a weighted per-room pool (starting at 0). " +
            "Difficulty below 1 weakens enemies. DungeonManager marks rooms 3, 6, 9... as reward rooms. " +
            "A custom enemy or reward SpawnList can be assigned per room. All encounter events run only on the server.", MessageType.Info);
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Running (server)", manager.IsRunning.ToString());
            EditorGUILayout.LabelField("Wave / alive (server)", $"{manager.CurrentWave} / {manager.LivingMinions}");
            Repaint();
        }
    }
}
