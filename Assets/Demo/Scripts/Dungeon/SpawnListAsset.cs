using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpawnList", menuName = "Dungeon/Spawn List")]
public class SpawnListAsset : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public GameObject Prefab;
        [Min(0)] public int Weight = 1;
    }

    [Range(1, 10)]
    public int Difficulty = 1;

    public List<Entry> Spawn = new List<Entry>();
}
