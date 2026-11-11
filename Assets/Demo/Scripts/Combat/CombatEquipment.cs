using System.Collections.Generic;
using UnityEngine;

namespace GonzoDemo
{
    public enum CombatEquipmentType
    {
        Melee,
        Range
    }

    [CreateAssetMenu(fileName = "CombatEquipment", menuName = "Combat/Equipment")]
    public class CombatEquipment : ScriptableObject
    {
        [Min(0)] public float Damage = 10f;
        public CombatEquipmentType Type = CombatEquipmentType.Melee;
        [Tooltip("Animator state clips played in sequence. Melee clips need an OnAttackAnim event at contact time.")]
        public List<AnimationClip> Animations = new List<AnimationClip>();
        [Tooltip("Maximum melee tracer distance or ranged target-lock distance.")]
        [Min(0.1f)] public float AttackRange = 1.5f;
        [Tooltip("Zero hits one target. A positive value enables area hit registration; for ranged equipment it is the impact radius.")]
        [Min(0f)] public float Radius;
        [Min(0.05f)] public float AttackCooldown = 0.5f;
        [Header("Range equipment")]
        public CombatHomingProjectile ProjectilePrefab;
        [Min(0.1f)] public float ProjectileSpeed = 15f;
    }
}
