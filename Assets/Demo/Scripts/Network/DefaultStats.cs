using UnityEngine;

namespace GonzoDemo
{
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "Content/PlayerStats", order = 1)]
    public class DefaultStats : ScriptableObject
    {
        public float health = 100f;
        public float cooldown = 0f;
        public float damage = 10f;
        public float speed = 5f;
        public float armor = 0f;
        public float specialArmor = 0f;
        public float abilityDamage = 0f;
    }
}