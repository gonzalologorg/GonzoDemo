using System;
using Unity.Netcode;
using UnityEngine;

namespace GonzoDemo.Combat
{
    /// <summary>Network payload describing one hit against a gameplay object.</summary>
    [Serializable]
    public struct HitInfo : INetworkSerializable
    {
        public float amount;
        public Vector3 hitPoint;
        public Vector3 hitNormal;
        public ulong attackerId;
        public Vector3 impactForce;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref amount);
            serializer.SerializeValue(ref hitPoint);
            serializer.SerializeValue(ref hitNormal);
            serializer.SerializeValue(ref attackerId);
            serializer.SerializeValue(ref impactForce);
        }
    }
}
