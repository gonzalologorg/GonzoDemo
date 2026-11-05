using System;
using Unity.Netcode;
using UnityEditor.Rendering;
using UnityEngine;
namespace GonzoDemo
{
    public class ProjectileBase : NetworkBehaviour
    {
        private readonly NetworkVariable<ulong> casterObjectId = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
		public GameObject BaseEffect;
        public GameObject ImpactEffect;

        public float LifeTime = 5f;
        public float Speed = 15f;
        [Min(0f)] public float Damage = 10f;

        public event Action<Collider> OnHit;
        public Action<Collider> OnImpact;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            GetComponent<Rigidbody>().AddForce(transform.forward * Speed);

        }

        public void Initialize(NetworkObject caster)
        {
			if (!IsServer) return;
			casterObjectId.Value = caster.NetworkObjectId;
		}

        public override void OnNetworkDespawn()
        {
            DisableProjectileCollisions();
            base.OnNetworkDespawn();
        }

        // Update is called once per frame
        void FixedUpdate()
        {
            if (LifeTime != -1f && LifeTime > 0f)
            {
                LifeTime -= Time.fixedDeltaTime;
                if (LifeTime <= 0f)
                {
                    Remove();
                }
            }
        }

        public void Remove()
        {
            if (consumed)
                return;

            consumed = true;
            RemoveProjectileObject();
        }

        private void RemoveProjectileObject()
        {
            DisableProjectileCollisions();

            if (IsServer)
			{
				NetworkObject.Despawn(false);
			}

			if (BaseEffect != null && BaseEffect.GetComponent<ParticleSystem>() != null)
				BaseEffect.GetComponent<ParticleSystem>().Stop();

			if (ImpactEffect != null)
			{
				ImpactEffect.SetActive(true);
				if (ImpactEffect.GetComponent<ParticleSystem>() != null)
					ImpactEffect.GetComponent<ParticleSystem>().Play();
			}

			Destroy(gameObject, 1f);
		}

        private void DisableProjectileCollisions()
        {
            foreach (Collider projectileCollider in GetComponentsInChildren<Collider>(true))
            {
                projectileCollider.enabled = false;
            }

            Rigidbody rigidbody = GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.Sleep();
                rigidbody.useGravity = false;
                rigidbody.detectCollisions = false;
            }
        }

        bool consumed = false;
        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer || consumed) return;

            NetworkObject target = other.GetComponentInParent<NetworkObject>();
            if (target != null && target.NetworkObjectId == casterObjectId.Value)
            {
                return;
            }

            consumed = true;
            OnHit?.Invoke(other);
            OnImpact?.Invoke(other);
            RemoveProjectileObject();
		}
    }
}
