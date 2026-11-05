using UnityEngine;

namespace GonzoDemo
{
    public enum DamageType
    {
        Physical,
        Magical,
        Unmitigated
    }

    /// <summary>Describes a damage event before it is resolved against a victim.</summary>
    public readonly struct DamageInfo
    {
        public float Damage { get; }
        public GameObject Attacker { get; }
        public GameObject Inflictor { get; }
        public DamageType Type { get; }
        public Vector3 Force { get; }
        public Vector3 Position { get; }
        public Collider HitCollider { get; }

        public DamageInfo(
            float damage,
            GameObject attacker,
            GameObject inflictor,
            DamageType type,
            Vector3 force,
            Vector3 position,
            Collider hitCollider)
        {
            Damage = damage;
            Attacker = attacker;
            Inflictor = inflictor;
            Type = type;
            Force = force;
            Position = position;
            HitCollider = hitCollider;
        }
    }
}
