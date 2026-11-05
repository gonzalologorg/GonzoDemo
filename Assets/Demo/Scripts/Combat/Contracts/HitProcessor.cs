using Unity.Netcode;
using UnityEngine;

namespace GonzoDemo.Combat
{
    /// <summary>Routes hit events to the network authority before applying their effects.</summary>
    public abstract class HitProcessor : NetworkBehaviour, IHittable
    {
        public void OnHit(HitInfo info)
        {
            if (!IsSpawned)
            {
                Debug.LogWarning($"Ignoring hit on '{name}' because its NetworkObject is not spawned.", this);
                return;
            }

            if (IsServer)
                HandleHit(info);
            else
                SubmitHitRpc(info);
        }

        [Rpc(SendTo.Server)]
        private void SubmitHitRpc(HitInfo info)
        {
            HandleHit(info);
        }

        protected abstract void HandleHit(HitInfo info);
    }
}
