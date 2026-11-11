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
        EditorGUILayout.HelpBox("Waves: one entry per wave. PoolIndex selects a weighted pool (starting at 0). " +
            "Difficulty below 1 weakens enemies. DungeonManager marks rooms 3, 6, 9... as reward rooms. " +
            "All encounter events run only on the server.", MessageType.Info);
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Running (server)", manager.IsRunning.ToString());
            EditorGUILayout.LabelField("Wave / alive (server)", $"{manager.CurrentWave} / {manager.LivingMinions}");
            Repaint();
        }
    }
}
