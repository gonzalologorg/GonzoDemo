using System;
using System.Collections.Generic;

using UnityEngine;
namespace GonzoDemo
{

	[CreateAssetMenu(fileName = "JutsuDefinition", menuName = "Content/Jutsu")]
	public class JutsuDefinition : ScriptableObject
	{
		public enum ERank { D, C, B, A, S }
		public enum ENature { DOTON, SUITON, KATON, RAITON, FUTTON }

		[Header("Jutsu Info")]
		[SerializeField]
		public string JutsuName;
		[SerializeField]
		public string Description;
		[SerializeField]
		public Sprite JutsuIcon;

		[Header("Jutsu Balance")]
		[SerializeField]
		public Dictionary<int, Dictionary<string, float>> Stats;
		[SerializeField]
		public List<int> Cost;
		[SerializeField]
		public List<int> Cooldown;

		[Header("Behaviours")]
		public List<JutsuBehaviour> Behaviours;
	}
}