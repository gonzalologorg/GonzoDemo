using System;
using UnityEngine;
namespace GonzoDemo
{

    [Serializable]
    public class JutsuInstance
    {
        public JutsuDefinition Definition { get; }
        public int Level { get; set; } = 1;
        public double CooldownEndTime { get; set; } = 0;

        public JutsuInstance(JutsuDefinition definition)
        {
            Definition = definition;
        }
    }
}