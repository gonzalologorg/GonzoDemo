using UnityEngine;
namespace GonzoDemo
{
	[CreateAssetMenu(
	fileName = "Weapon",
	menuName = "Content/Items/Weapon",
	order = 1
)]
	public class Weapon : ItemDefinition
	{
		public GameObject model;
		public RuntimeAnimatorController animGroup;

		public float damage;
		public float attackSpeed;
	}
}