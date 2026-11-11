using UnityEngine;

namespace GonzoDemo
{
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "Content/PlayerStats", order = 1)]
    public class DefaultStats : ScriptableObject
    {
        public float health = 100f;
        [Min(0f)] public float chakra = 100f;
        public float cooldown = 0f;
        public float damage = 10f;
        [Tooltip("Attacks per second. Zero disables basic attacks.")]
        [Min(0f)] public float attackSpeed = 1f;
        public float speed = 5f;
        public float armor = 0f;
        public float specialArmor = 0f;
        public float abilityDamage = 0f;
    }
}
