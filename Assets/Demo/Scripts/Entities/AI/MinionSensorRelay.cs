using UnityEngine;

namespace GonzoDemo.AI
{
    public class MinionSensorRelay : MonoBehaviour
    {
        internal MinionSensor Sensor;
        private void OnTriggerEnter(Collider other) => Sensor.Enter(other);
        private void OnTriggerExit(Collider other) => Sensor.Exit(other);
    }
}
