using UnityEngine;
using System.Collections.Generic;

/// <summary>Forwards child trigger contacts; authority is checked by the encounter driver.</summary>
public class DungeonRoomTrigger : MonoBehaviour
{
    internal DungeonSpawnManager Owner;
    private readonly HashSet<Collider> contacts = new HashSet<Collider>();
    internal bool Contains(Collider other) => isActiveAndEnabled && contacts.Contains(other);
    private void OnTriggerEnter(Collider other) => Observe(other);
    private void OnTriggerStay(Collider other) => Observe(other);
    private void Observe(Collider other)
    {
        contacts.Add(other);
        if (Owner != null) Owner.Observe(other);
    }
    private void OnTriggerExit(Collider other)
    {
        contacts.Remove(other);
        if (Owner != null) Owner.Leave(other);
    }
    private void OnDisable()
    {
        var previous = new List<Collider>(contacts);
        contacts.Clear();
        foreach (var collider in previous) if (Owner != null) Owner.Leave(collider);
    }
}
