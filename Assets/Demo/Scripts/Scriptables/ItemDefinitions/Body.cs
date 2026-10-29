using UnityEngine;
namespace GonzoDemo
{
	[CreateAssetMenu(
	fileName = "Body",
	menuName = "Content/Items/Body",
	order = 1
)]
	public class Body : ItemDefinition
	{
		public GameObject model;
		public Material material;
	}
}