using GonzoDemo;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(JutsuController))]
public class JutsuControllerEditor : Editor
{
	private ReorderableList jutsuSlots;

	private void OnEnable()
	{
		SerializedProperty entries = serializedObject.FindProperty("startingJutsus");
		jutsuSlots = new ReorderableList(serializedObject, entries, true, true, true, true)
		{
			drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Jutsu Slots (slot → definition)"),
			elementHeight = EditorGUIUtility.singleLineHeight * 3f + 10f,
			drawElementCallback = DrawJutsuSlot,
			onAddCallback = AddJutsuSlot
		};
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();
		DrawPropertiesExcluding(serializedObject, "m_Script", "startingJutsus");
		EditorGUILayout.Space();
		jutsuSlots.DoLayoutList();
		serializedObject.ApplyModifiedProperties();
	}

	private void DrawJutsuSlot(Rect rect, int index, bool isActive, bool isFocused)
	{
		SerializedProperty entry = jutsuSlots.serializedProperty.GetArrayElementAtIndex(index);
		SerializedProperty slot = entry.FindPropertyRelative("slot");
		SerializedProperty definition = entry.FindPropertyRelative("definition");
		SerializedProperty level = entry.FindPropertyRelative("level");

		float lineHeight = EditorGUIUtility.singleLineHeight;
		Rect slotRect = new Rect(rect.x, rect.y + 2f, rect.width, lineHeight);
		Rect definitionRect = new Rect(rect.x, slotRect.yMax + 2f, rect.width, lineHeight);
		Rect levelRect = new Rect(rect.x, definitionRect.yMax + 2f, rect.width, lineHeight);

		EditorGUI.PropertyField(slotRect, slot, new GUIContent("Slot"));
		EditorGUI.PropertyField(definitionRect, definition, new GUIContent("Definition"));
		EditorGUI.PropertyField(levelRect, level, new GUIContent("Level"));
	}

	private void AddJutsuSlot(ReorderableList list)
	{
		int index = list.serializedProperty.arraySize;
		list.serializedProperty.arraySize++;
		SerializedProperty entry = list.serializedProperty.GetArrayElementAtIndex(index);
		entry.FindPropertyRelative("slot").intValue = FindFirstUnusedSlot();
		entry.FindPropertyRelative("definition").objectReferenceValue = null;
		entry.FindPropertyRelative("level").intValue = 1;
		list.index = index;
	}

	private int FindFirstUnusedSlot()
	{
		SerializedProperty entries = jutsuSlots.serializedProperty;
		for (int candidate = 0; ; candidate++)
		{
			bool used = false;
			for (int i = 0; i < entries.arraySize; i++)
			{
				if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("slot").intValue == candidate)
				{
					used = true;
					break;
				}
			}

			if (!used)
				return candidate;
		}
	}
}
