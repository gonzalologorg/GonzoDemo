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

        public Action OnImpact;

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
			GetComponent<Rigidbody>().Sleep();
			GetComponent<Rigidbody>().useGravity = false;

			NetworkObject.Despawn(false);
			consumed = true;

			if (BaseEffect.GetComponent<ParticleSystem>() != null)
				BaseEffect.GetComponent<ParticleSystem>().Stop();

			ImpactEffect.SetActive(true);
			if (ImpactEffect.GetComponent<ParticleSystem>() != null)
				ImpactEffect.GetComponent<ParticleSystem>().Play();

			Destroy(gameObject, 1f);
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

            Remove();
		}
    }
}