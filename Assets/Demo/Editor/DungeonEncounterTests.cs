using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DungeonEncounterTests
{
    private Scene previousScene;
    private Scene testScene;
    private DungeonManager manager;
    private readonly List<RoomObject> rooms = new List<RoomObject>();
    private readonly List<DoorObject> outward = new List<DoorObject>();
    private readonly List<DoorObject> inward = new List<DoorObject>();

    [SetUp]
    public void SetUp()
    {
        previousScene = SceneManager.GetActiveScene();
        testScene = SceneManager.CreateScene("DungeonEncounterTests");
        SceneManager.SetActiveScene(testScene);
        manager = new GameObject("Manager").AddComponent<DungeonManager>();
        for (int i = 0; i < 4; i++)
        {
            var container = new GameObject("Room " + i);
            var roomObject = new GameObject("RoomObject");
            roomObject.transform.SetParent(container.transform);
            var room = roomObject.AddComponent<RoomObject>();
            room.SpawnManager = container.AddComponent<DungeonSpawnManager>();
            rooms.Add(room);
        }
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            var first = CreateDoor(rooms[i]);
            var second = CreateDoor(rooms[i + 1]);
            first.AddConnection(second, rooms[i + 1]);
            second.AddConnection(first, rooms[i]);
            outward.Add(first);
            inward.Add(second);
        }
        // Explicit route avoids random generation in presentation/progression regressions.
        var serialized = new SerializedObject(manager);
        var route = serialized.FindProperty("selectedRooms");
        route.arraySize = rooms.Count;
        for (int i = 0; i < rooms.Count; i++) route.GetArrayElementAtIndex(i).objectReferenceValue = rooms[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        manager.RefreshDoorStates();
    }

    private static DoorObject CreateDoor(RoomObject room)
    {
        var obj = new GameObject("Door");
        obj.transform.SetParent(room.transform);
        obj.AddComponent<BoxCollider>();
        var door = obj.AddComponent<DoorObject>();
        room.Doors.Add(door);
        return door;
    }

    [TearDown]
    public void TearDown()
    {
        if (previousScene.IsValid()) SceneManager.SetActiveScene(previousScene);
        EditorSceneManager.CloseScene(testScene, true);
        rooms.Clear(); outward.Clear(); inward.Clear();
    }

    [Test]
    public void UniversalListsRemainEligibleAlongsideNearestDifficultyAtEveryLevel()
    {
        var universal = ScriptableObject.CreateInstance<SpawnListAsset>();
        var normal = ScriptableObject.CreateInstance<SpawnListAsset>();
        var reward = new GameObject("Test reward");
        reward.AddComponent<Unity.Netcode.NetworkObject>();
        var randomState = Random.state;
        try
        {
            universal.Difficulty = 0;
            normal.Difficulty = 5;
            universal.Spawn.Add(new SpawnListAsset.Entry { Prefab = reward });
            normal.Spawn.Add(new SpawnListAsset.Entry { Prefab = reward });
            var serialized = new SerializedObject(manager);
            var lists = serialized.FindProperty("mapSpawnLists");
            lists.arraySize = 2;
            lists.GetArrayElementAtIndex(0).objectReferenceValue = universal;
            lists.GetArrayElementAtIndex(1).objectReferenceValue = normal;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var spawner = rooms[0].SpawnManager;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var resolve = typeof(DungeonSpawnManager).GetMethod("TryResolveSpawnList", flags);
            var difficulty = typeof(DungeonSpawnManager).GetField("encounterDifficulty", flags);
            Random.InitState(1234);
            for (int level = 1; level <= 10; level++)
            {
                difficulty.SetValue(spawner, level);
                var selected = new HashSet<SpawnListAsset>();
                for (int attempt = 0; attempt < 64; attempt++)
                {
                    object[] args = { true, null, null, null };
                    Assert.That(resolve.Invoke(spawner, args), Is.EqualTo(true));
                    selected.Add((SpawnListAsset)args[2]);
                }
                Assert.That(selected, Does.Contain(universal), "Level " + level);
                Assert.That(selected, Does.Contain(normal), "Level " + level);
            }
        }
        finally
        {
            Random.state = randomState;
            Object.DestroyImmediate(universal);
            Object.DestroyImmediate(normal);
            Object.DestroyImmediate(reward);
        }
    }

    [Test]
    public void BothSidesOfNextRoomOpenButFutureRoomsStayBlocked()
    {
        Assert.That(outward[0].IsLocked, Is.False);
        Assert.That(inward[0].IsLocked, Is.False);
        Assert.That(outward[1].IsLocked, Is.True);
        Assert.That(inward[1].IsLocked, Is.True);
        manager.AdvanceClearingIndex();
        Assert.That(outward[1].IsLocked, Is.False);
        Assert.That(inward[1].IsLocked, Is.False);
        Assert.That(outward[2].IsLocked, Is.True);
        Assert.That(outward[1].GetComponent<Collider>().enabled, Is.False);
    }

    [Test]
    public void ThirdRoomIsRewardAndMainRoomNeverIs()
    {
        Assert.That(rooms[0].SpawnManager.IsRewardRoom, Is.False);
        Assert.That(rooms[1].SpawnManager.IsRewardRoom, Is.False);
        Assert.That(rooms[2].SpawnManager.IsRewardRoom, Is.False);
        Assert.That(rooms[3].SpawnManager.IsRewardRoom, Is.True);
    }

    [Test]
    public void EncounterCommandsWithoutServerAuthorityCannotChangeProgress()
    {
        Assert.That(manager.TryBeginEncounter(rooms[1].SpawnManager), Is.False);
        manager.CompleteEncounter(rooms[1].SpawnManager);
        manager.CancelEncounter(rooms[1].SpawnManager);
        Assert.That(manager.ClearingIndex, Is.Zero);
        Assert.That(manager.ActiveEncounterIndex, Is.EqualTo(-1));
    }

    [Test]
    public void RoomFindsControllerOnItsOwnContainer()
    {
        var expected = rooms[1].SpawnManager;
        rooms[1].SpawnManager = null;
        Assert.That(rooms[1].GetSpawnManager(), Is.SameAs(expected));
        Assert.That(rooms[1].GetSpawnManager(), Is.Not.SameAs(rooms[0].SpawnManager));
    }
}
