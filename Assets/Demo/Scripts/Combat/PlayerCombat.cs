using System;
using System.Collections.Generic;
using GonzoDemo.AI;
using GonzoDemo.Combat;
using Unity.Netcode;
using UnityEngine;

namespace GonzoDemo
{
    [DisallowMultipleComponent]
    public class PlayerCombat : NetworkBehaviour
    {
        [Header("Loadout")]
        [SerializeField] private List<CombatEquipment> loadout = new List<CombatEquipment>();
        [SerializeField, Min(0)] private int initialEquipmentIndex;

        [Header("Attack tracing")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private LayerMask targetLayers = ~0;
        [SerializeField, Min(0.01f)] private float singleTargetTraceRadius = 0.2f;

        private readonly NetworkVariable<int> selectedLoadoutIndex = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly HashSet<ulong> hitTargets = new HashSet<ulong>();
        private PlayerController playerController;
        private PlayerStats playerStats;
        private int animationIndex;
        private float nextLocalAttackTime;
        private float nextServerAttackTime;
        private bool meleeHitPending;
        private int pendingEquipmentIndex = -1;
        private Vector3 pendingAimDirection;
        private float pendingMeleeExpiresAt;
        private static readonly int attackPlaybackSpeed = Animator.StringToHash("AttackPlaybackSpeed");

        public IReadOnlyList<CombatEquipment> Loadout => loadout;
        public int SelectedEquipmentIndex => selectedLoadoutIndex.Value;
        public CombatEquipment SelectedEquipment => GetEquipment(selectedLoadoutIndex.Value);

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerStats = GetComponent<PlayerStats>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
            if (attackOrigin == null && playerController != null)
                attackOrigin = playerController.HeadTransform;
            if (attackOrigin == null)
                attackOrigin = transform;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                selectedLoadoutIndex.Value = ClampLoadoutIndex(initialEquipmentIndex);
        }

        public void SelectEquipment(int index)
        {
            if (!IsOwner || !IsSpawned || GetEquipment(index) == null)
                return;

            if (IsServer)
                selectedLoadoutIndex.Value = index;
            else
                SelectEquipmentServerRpc(index);
        }

        public void Attack()
        {
            if (!IsOwner || !IsSpawned || Time.time < nextLocalAttackTime ||
                (playerController != null && playerController.IsCastingJutsu))
                return;

            int equipmentIndex = selectedLoadoutIndex.Value;
            CombatEquipment equipment = GetEquipment(equipmentIndex);
            if (equipment == null)
                return;

            float attackInterval = GetAttackInterval(equipment);
            if (float.IsInfinity(attackInterval)) return;
            nextLocalAttackTime = Time.time + attackInterval;
            Vector3 aimDirection = GetLocalAimDirection();
            PlayAttackAnimation(equipment, attackInterval);

            if (IsServer)
                BeginAttackOnServer(equipmentIndex, aimDirection, OwnerClientId);
            else
                RequestAttackServerRpc(equipmentIndex, aimDirection);
        }

        /// <summary>Called by the animation event relay at the contact frame of a melee clip.</summary>
        public void OnAttackAnim()
        {
            if (!IsOwner || !IsSpawned)
                return;

            if (IsServer)
                ResolvePendingMeleeHit(OwnerClientId);
            else
                MeleeHitServerRpc();
        }

        public override void OnNetworkDespawn()
        {
            meleeHitPending = false;
            pendingEquipmentIndex = -1;
        }

        private void Update()
        {
            if (IsServer && meleeHitPending && Time.time > pendingMeleeExpiresAt)
            {
                meleeHitPending = false;
                pendingEquipmentIndex = -1;
            }
        }

        [Rpc(SendTo.Server)]
        private void SelectEquipmentServerRpc(int index, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || GetEquipment(index) == null)
                return;

            selectedLoadoutIndex.Value = index;
        }

        [Rpc(SendTo.Server)]
        private void RequestAttackServerRpc(int equipmentIndex, Vector3 aimDirection, RpcParams rpcParams = default)
        {
            BeginAttackOnServer(equipmentIndex, aimDirection, rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server)]
        private void MeleeHitServerRpc(RpcParams rpcParams = default)
        {
            ResolvePendingMeleeHit(rpcParams.Receive.SenderClientId);
        }

        private void BeginAttackOnServer(int equipmentIndex, Vector3 aimDirection, ulong senderClientId)
        {
            if (!IsServer || senderClientId != OwnerClientId || equipmentIndex != selectedLoadoutIndex.Value ||
                meleeHitPending || Time.time < nextServerAttackTime)
                return;

            CombatEquipment equipment = GetEquipment(equipmentIndex);
            if (equipment == null || (playerController != null && playerController.GetHealth() <= 0))
                return;

            float attackInterval = GetAttackInterval(equipment);
            if (float.IsInfinity(attackInterval)) return;
            nextServerAttackTime = Time.time + attackInterval;
            pendingAimDirection = IsFinite(aimDirection) && aimDirection.sqrMagnitude > 0.001f
                ? aimDirection.normalized
                : transform.forward;

            if (equipment.Type == CombatEquipmentType.Melee)
            {
                pendingEquipmentIndex = equipmentIndex;
                pendingMeleeExpiresAt = Time.time + Mathf.Max(1.5f, attackInterval + 1f);
                meleeHitPending = true;
                return;
            }

            SpawnRangedProjectile(equipment, pendingAimDirection);
        }

        private float GetAttackInterval(CombatEquipment equipment)
        {
            float attacksPerSecond = playerStats != null ? playerStats.GetStat(PlayerStats.StatType.AttackSpeed) : 1f;
            return CalculateAttackInterval(attacksPerSecond, equipment.AttackCooldown);
        }

        public static float CalculateAttackInterval(float attacksPerSecond, float cooldownMultiplier)
        {
            if (attacksPerSecond <= 0f || float.IsNaN(attacksPerSecond) || float.IsInfinity(attacksPerSecond) ||
                float.IsNaN(cooldownMultiplier) || float.IsInfinity(cooldownMultiplier))
                return float.PositiveInfinity;

            return Mathf.Max(0.05f, cooldownMultiplier) / attacksPerSecond;
        }

        private void ResolvePendingMeleeHit(ulong senderClientId)
        {
            if (!IsServer || senderClientId != OwnerClientId || !meleeHitPending ||
                Time.time > pendingMeleeExpiresAt)
                return;

            meleeHitPending = false;
            CombatEquipment equipment = GetEquipment(pendingEquipmentIndex);
            pendingEquipmentIndex = -1;
            if (equipment == null || equipment.Type != CombatEquipmentType.Melee)
                return;

            PerformMeleeTrace(equipment, pendingAimDirection);
        }

        private void PerformMeleeTrace(CombatEquipment equipment, Vector3 direction)
        {
            Vector3 origin = GetAttackOrigin();
            float radius = equipment.Radius > 0f ? equipment.Radius : singleTargetTraceRadius;
            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                radius,
                direction,
                equipment.AttackRange,
                targetLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            hitTargets.Clear();

            foreach (RaycastHit hit in hits)
            {
                NetworkObject target = hit.collider.GetComponentInParent<NetworkObject>();
                if (target == NetworkObject)
                    continue;

                if (target == null || !target.IsSpawned || !IsDamageable(hit.collider))
                    break;

                if (hitTargets.Add(target.NetworkObjectId))
                {
                    ApplyDamageToTarget(target, hit.collider, gameObject, gameObject,
                        equipment.Damage, direction, hit.point);
                }

                if (equipment.Radius <= 0f)
                    break;
            }
        }

        private void SpawnRangedProjectile(CombatEquipment equipment, Vector3 direction)
        {
            if (equipment.ProjectilePrefab == null)
            {
                Debug.LogError($"{name}: ranged CombatEquipment '{equipment.name}' has no projectile prefab.", this);
                return;
            }

            NetworkObject projectileNetworkObject = equipment.ProjectilePrefab.GetComponent<NetworkObject>();
            if (projectileNetworkObject == null || NetworkManager == null ||
                !NetworkManager.NetworkConfig.Prefabs.Contains(equipment.ProjectilePrefab.gameObject))
            {
                Debug.LogError($"{name}: register the ranged projectile prefab in NetworkPrefabs and add a NetworkObject.", this);
                return;
            }

            NetworkObject target = FindTargetAlongAim(direction, equipment.AttackRange);
            if (target == null)
                return;

            Vector3 origin = GetAttackOrigin();
            CombatHomingProjectile projectile = Instantiate(
                equipment.ProjectilePrefab,
                origin,
                Quaternion.LookRotation(direction, Vector3.up));
            projectile.Initialize(NetworkObject, target, equipment.Damage, equipment.Radius,
                equipment.ProjectileSpeed, targetLayers);
            projectile.NetworkObject.Spawn();
        }

        private NetworkObject FindTargetAlongAim(Vector3 direction, float range)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                GetAttackOrigin(),
                singleTargetTraceRadius,
                direction,
                range,
                targetLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                NetworkObject target = hit.collider.GetComponentInParent<NetworkObject>();
                if (target == NetworkObject)
                    continue;
                if (target != null && target.IsSpawned && IsDamageable(hit.collider))
                    return target;
                return null;
            }

            return null;
        }

        public static float CalculateAttackPlaybackSpeed(float clipLength, float attackInterval)
        {
            // Normalize the entire clip to the attack interval, regardless of its authored duration.
            return clipLength > 0f && attackInterval > 0f ? clipLength / attackInterval : 1f;
        }

        private void PlayAttackAnimation(CombatEquipment equipment, float attackInterval)
        {
            if (animator == null)
                return;

            if (equipment.Animations != null && equipment.Animations.Count > 0)
            {
                for (int i = 0; i < equipment.Animations.Count; i++)
                {
                    int index = (animationIndex + i) % equipment.Animations.Count;
                    AnimationClip clip = equipment.Animations[index];
                    if (clip == null)
                        continue;

                    int stateHash = Animator.StringToHash("Base Layer." + clip.name);
                    if (!animator.HasState(0, stateHash))
                        stateHash = Animator.StringToHash(clip.name);
                    if (!animator.HasState(0, stateHash))
                    {
                        Debug.LogWarning($"{name}: Animator has no state named '{clip.name}'.", this);
                        continue;
                    }

                    animationIndex = index + 1;
                    SetAttackPlaybackSpeed(CalculateAttackPlaybackSpeed(clip.length, attackInterval));
                    animator.CrossFade(stateHash, 0.05f, 0, 0f);
                    return;
                }
            }

            int attackTrigger = Animator.StringToHash("DoAttack");
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == attackTrigger && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    SetAttackPlaybackSpeed(CalculateAttackPlaybackSpeed(1f, attackInterval));
                    animator.SetTrigger(attackTrigger);
                    return;
                }
            }
        }

        private void SetAttackPlaybackSpeed(float speed)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == attackPlaybackSpeed && parameter.type == AnimatorControllerParameterType.Float)
                {
                    animator.SetFloat(attackPlaybackSpeed, speed);
                    return;
                }
            }
        }

        private Vector3 GetLocalAimDirection()
        {
            Camera mainCamera = Camera.main;
            return mainCamera != null ? mainCamera.transform.forward.normalized : transform.forward;
        }

        private Vector3 GetAttackOrigin()
        {
            return attackOrigin != null ? attackOrigin.position : transform.position + Vector3.up;
        }

        private CombatEquipment GetEquipment(int index)
        {
            return loadout != null && index >= 0 && index < loadout.Count ? loadout[index] : null;
        }

        private int ClampLoadoutIndex(int index)
        {
            if (loadout == null || loadout.Count == 0)
                return 0;
            return Mathf.Clamp(index, 0, loadout.Count - 1);
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static bool IsDamageable(Collider collider)
        {
            return collider.GetComponentInParent<Minion>() != null ||
                collider.GetComponentInParent<IHittable>() != null ||
                collider.GetComponentInParent<PlayerStats>() != null;
        }

        internal static bool ApplyDamageToTarget(
            NetworkObject target,
            Collider hitCollider,
            GameObject attacker,
            GameObject inflictor,
            float damage,
            Vector3 direction,
            Vector3 hitPoint)
        {
            if (target == null || !target.IsSpawned || attacker == null ||
                float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
                return false;

            Vector3 force = direction.sqrMagnitude > 0f ? direction.normalized * damage : Vector3.zero;
            var damageInfo = new DamageInfo(damage, attacker, inflictor, DamageType.Physical,
                force, hitPoint, hitCollider);

            Minion minion = target.GetComponent<Minion>();
            if (minion != null)
                return minion.ReceiveDamage(damageInfo) > 0f;

            IHittable hittable = hitCollider != null
                ? hitCollider.GetComponentInParent<IHittable>()
                : target.GetComponent<IHittable>();
            if (hittable != null)
            {
                NetworkObject attackerNetworkObject = attacker.GetComponentInParent<NetworkObject>();
                hittable.OnHit(new HitInfo
                {
                    amount = damage,
                    hitPoint = hitPoint,
                    hitNormal = direction.sqrMagnitude > 0f ? -direction.normalized : Vector3.up,
                    attackerId = attackerNetworkObject != null ? attackerNetworkObject.OwnerClientId : 0,
                    impactForce = force
                });
                return true;
            }

            PlayerStats player = target.GetComponent<PlayerStats>();
            return player != null && DamageResolver.Apply(player, damageInfo) > 0f;
        }
    }
}
