using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BreathBase : NetworkBehaviour
{
    public NetworkVariable<float> BreathProgress = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    [Min(0.01f)] public float lerpSpeed = 1f;
    [Min(0f)] public float maxBreathDistance = 5f;
    [Min(0f)] public float lifeTime = 5f;

    public Transform StartBone;
    public Transform EndBone;
    public SphereCollider Sphere;
    public Transform HeadTransform;

    private Transform headTransform;
    private Transform casterRoot;
    private GonzoDemo.PlayerController casterPlayerController;
    private NetworkObject casterNetworkObject;
    private GonzoDemo.JutsuController jutsuController;
    private Vector3 endBoneFullScale;
    private Quaternion smoothedEndRotation;
    private Coroutine breathRoutine;
    private bool hasEndBoneScale;
    private bool hasSmoothedEndRotation;

    public override void OnNetworkSpawn()
    {
        CacheReferences();

        if (IsOwner)
        {
            BreathProgress.Value = 0f;
            StartBreath();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (breathRoutine != null)
        {
            StopCoroutine(breathRoutine);
            breathRoutine = null;
        }
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void Update()
    {
        if (!IsSpawned || StartBone == null || EndBone == null)
            return;

        if (headTransform == null || jutsuController == null)
            CacheReferences();

        UpdateBeamPose();
    }

    /// <summary>Starts the fill, hold, and fade cycle. Called automatically by the owner on spawn.</summary>
    public void StartBreath()
    {
        if (!IsSpawned || !IsOwner)
            return;

        CacheReferences();
        if (breathRoutine != null)
            StopCoroutine(breathRoutine);

        ResetBeamToOrigin();
        if (Sphere != null)
            Sphere.enabled = true;

        breathRoutine = StartCoroutine(RunBreath());
    }

    private void ResetBeamToOrigin()
    {
        if (StartBone == null || EndBone == null)
            return;

        Vector3 origin = headTransform != null ? headTransform.position : StartBone.position;
        Quaternion rotation = jutsuController != null
            ? Quaternion.Euler(jutsuController.GetAimAngle())
            : (headTransform != null ? headTransform.rotation : StartBone.rotation);
        Quaternion nominalRotation = rotation * Quaternion.Euler(0f, 90f, 0f);
        StartBone.position = origin;
        EndBone.SetPositionAndRotation(origin, nominalRotation);
        smoothedEndRotation = nominalRotation;
        hasSmoothedEndRotation = true;
        if (hasEndBoneScale)
            EndBone.localScale = Vector3.zero;
    }

    /// <summary>Connects a separately spawned beam instance to its casting player.</summary>
    public void SetCaster(GonzoDemo.PlayerController caster)
    {
        if (caster == null)
            return;

        HeadTransform = caster.HeadTransform;
        headTransform = HeadTransform;
        casterPlayerController = caster;
        casterNetworkObject = caster.GetComponent<NetworkObject>();
        casterRoot = caster.transform.root;
        jutsuController = caster.GetComponent<GonzoDemo.JutsuController>();
    }

    private IEnumerator RunBreath()
    {
        BreathProgress.Value = 0f;
        while (BreathProgress.Value < 1f)
        {
            BreathProgress.Value = Mathf.MoveTowards(
                BreathProgress.Value,
                1f,
                Mathf.Max(0.01f, lerpSpeed) * Time.deltaTime);
            yield return null;
        }

        BreathProgress.Value = 1f;
        if (lifeTime > 0f)
            yield return new WaitForSeconds(lifeTime);

        while (BreathProgress.Value > 0f)
        {
            BreathProgress.Value = Mathf.MoveTowards(
                BreathProgress.Value,
                0f,
                Mathf.Max(0.01f, lerpSpeed) * Time.deltaTime);
            yield return null;
        }

        BreathProgress.Value = 0f;
        if (Sphere != null)
            Sphere.enabled = false;

        breathRoutine = null;
    }

    private void UpdateBeamPose()
    {
        float progress = Mathf.Clamp01(BreathProgress.Value);
        Vector3 origin = headTransform != null ? headTransform.position : StartBone.position;
        Quaternion aimRotation = jutsuController != null
            ? Quaternion.Euler(jutsuController.GetAimAngle())
            : (headTransform != null ? headTransform.rotation : StartBone.rotation);
        Vector3 direction = aimRotation * Vector3.forward;

        // Keep the ray direction camera-aligned while rotating the model 90 degrees around its local yaw.
        StartBone.position = origin;
        StartBone.rotation = aimRotation * Quaternion.Euler(0f, 90f, 0f);

        float desiredDistance = Mathf.Max(0f, maxBreathDistance) * progress;
        if (TryTrace(origin, direction, desiredDistance, out RaycastHit hit))
            desiredDistance = hit.distance;

        Vector3 idealEndPosition = origin + direction * desiredDistance;
        float followFactor = 1f - Mathf.Exp(-Mathf.Max(0.01f, lerpSpeed) * Time.deltaTime);
        EndBone.position = Vector3.Lerp(
            EndBone.position,
            idealEndPosition,
            followFactor);

        Quaternion nominalRotation = aimRotation * Quaternion.Euler(0f, 90f, 0f);
        if (!hasSmoothedEndRotation)
        {
            smoothedEndRotation = EndBone.rotation;
            hasSmoothedEndRotation = true;
        }

        smoothedEndRotation = Quaternion.Slerp(smoothedEndRotation, nominalRotation, followFactor);
        // Set the smoothed world target; Unity derives the compensating local rotation under the moving parent.
        EndBone.rotation = smoothedEndRotation;

        if (!hasEndBoneScale)
        {
            endBoneFullScale = EndBone.localScale;
            hasEndBoneScale = true;
        }
        EndBone.localScale = endBoneFullScale * progress;

        if (Sphere != null)
        {
            Sphere.transform.position = EndBone.position;
            Sphere.enabled = progress > 0f && (IsServer || !IsSpawned);
        }
    }

    private bool TryTrace(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearestHit)
    {
        nearestHit = default;
        if (distance <= 0f || direction.sqrMagnitude < 0.0001f)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction.normalized,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.MaxValue;
        bool foundHit = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || IsCasterCollider(hit.collider))
                continue;

            if (hit.distance >= nearestDistance)
                continue;

            nearestDistance = hit.distance;
            nearestHit = hit;
            foundHit = true;
        }

        return foundHit;
    }

    private bool IsCasterCollider(Collider col)
    {
        if (col == null)
            return false;

        if (casterRoot != null && col.transform.root == casterRoot)
            return true;

        if (casterPlayerController != null &&
            col.GetComponentInParent<GonzoDemo.PlayerController>() == casterPlayerController)
            return true;

        return casterNetworkObject != null &&
               col.GetComponentInParent<NetworkObject>() == casterNetworkObject;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleTriggerImpact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        HandleTriggerImpact(other);
    }

    private void HandleTriggerImpact(Collider other)
    {
        if (!IsSpawned || !IsServer || other == null || other == Sphere)
            return;

        if (IsCasterCollider(other))
            return;

        bool isEntity = other.GetComponentInParent<NetworkObject>() != null ||
                        other.GetComponentInParent<GonzoDemo.PlayerController>() != null ||
                        other.GetComponentInParent<CharacterController>() != null ||
                        other.GetComponentInParent<Rigidbody>() != null;
        if (!isEntity)
            return;

        OnBreathImpact(other);
    }

    private void CacheReferences()
    {
        if (jutsuController == null)
            jutsuController = GetComponentInParent<GonzoDemo.JutsuController>();

        GonzoDemo.PlayerController playerController = jutsuController != null
            ? jutsuController.GetComponent<GonzoDemo.PlayerController>()
            : GetComponentInParent<GonzoDemo.PlayerController>();

        if (playerController != null)
        {
            casterPlayerController = playerController;
            casterNetworkObject = playerController.GetComponent<NetworkObject>();
            if (HeadTransform == null)
                HeadTransform = playerController.HeadTransform;
            if (headTransform == null)
                headTransform = HeadTransform;
            casterRoot = playerController.transform.root;
        }
        else
        {
            casterRoot = transform.root;
        }

        if (EndBone != null && !hasEndBoneScale)
        {
            endBoneFullScale = EndBone.localScale;
            hasEndBoneScale = true;
        }
    }

    public virtual void OnBreathImpact(Collider col)
    {
    }
}
