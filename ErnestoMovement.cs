using System.Collections.Generic;
using UnityEngine;

namespace ErnestoChase;

public class ErnestoMovement : MonoBehaviour
{
    public delegate void TeleportRequiredEvent();
    public event TeleportRequiredEvent OnTeleportRequired;
    public delegate void ProximityRoarEvent(bool inRange);
    public event ProximityRoarEvent OnProximityRoar;

    ErnestoState state;
    PlanetManager planetManager;

    private float baseSpeed = 8f;
    private float currentSpeed;

    private float baseSpaceSpeed;
    private float currentSpaceSpeed;

    private Queue<(Vector3 pos, bool isTeleport)> targets = new();
    private Queue<(Vector3 pos, bool isTeleport)> spaceTargets = new();
    private GameObject targetPrefab;

    private float targetSpawnDelay = 0.25f;
    private float spawnDelayTimer;

    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float lastTime;

    private bool canMove = false;
    private bool standingStill = false;

    private Dictionary<Vector3, bool> brambleSpeedMarkers = [];
    private bool lastPlayerBrambleState;
    private Dictionary<Vector3, bool> dreamWorldSpeedMarkers = [];
    private bool lastPlayerDreamState;

    private float spaceTimedStartDistance;
    private bool hasTakenShortcut = true;
    private bool proximityRoar = true;
    private bool ernestoFrozen = false;

    private void Awake()
    {
        state = GetComponent<ErnestoState>();
        planetManager = GetComponent<PlanetManager>();
        targetPrefab = ErnestoChase.LoadPrefab("Assets/ErnestoChase/ErnestoTarget.prefab");

        float speedMultiplier;
        if (ErnestoChase.Instance.QuantumMode)
        {
            speedMultiplier = 5f;
        }
        else
        {
            float speedLerp = Mathf.InverseLerp(1, 10, ErnestoChase.Instance.MovementSpeed);
            if (speedLerp < 0.5f)
            {
                speedMultiplier = Mathf.Lerp(0.2f, 1f, speedLerp * 2);
            }
            else
            {
                speedMultiplier = Mathf.Lerp(1f, 4f, (speedLerp - 0.5f) * 2);
            }
        }

        baseSpeed *= speedMultiplier;
        currentSpeed = baseSpeed;

        baseSpaceSpeed = baseSpeed / 10f;
        currentSpaceSpeed = baseSpaceSpeed;

        spawnDelayTimer = targetSpawnDelay;
    }


    public void Initialize()
    {
        Vector3 playerStartPos = transform.parent.InverseTransformPoint(Locator.GetPlayerTransform().position);
        transform.localPosition = playerStartPos;
        lastPosition = playerStartPos;
        lastRotation = transform.rotation;

        lastPlayerBrambleState = PlayerState.InBrambleDimension();
        lastPlayerDreamState = PlayerState.InDreamWorld();
    }

    private void Update()
    {
        if (spawnDelayTimer > 0f)
        {
            spawnDelayTimer -= Time.deltaTime;
        }

        else if (planetManager.IsOnPlanet() && !planetManager.RecentlyTeleported())
        {
            spawnDelayTimer = targetSpawnDelay;

            Transform targetParent = planetManager.GetTargetParent();
            var arrayTargets = targets.ToArray();
            var previousTarget = arrayTargets[arrayTargets.Length - 1].pos;
            Vector3 playerPos = targetParent.InverseTransformPoint(Locator.GetPlayerTransform().position);

            if (targets.Count == 0 || (playerPos - previousTarget).sqrMagnitude > 0.5f * 0.5f)
            {
                if (standingStill)
                {
                    standingStill = false;
                }
                SpawnTarget(targetParent, Locator.GetPlayerTransform().position);
            }
            else
            {
                standingStill = true;
            }
        }
    }

    public void UpdateMovement(bool playerOnPlanet)
    {
        UpdateErnestoVisibility();

        if (canMove && !ernestoFrozen)
        {
            UpdateMovementSpeed(playerOnPlanet);
            Move();
        }
    }

    public void SetTravelMode(bool isSpace)
    {
        if (isSpace)
        {
            targets.Clear();
            standingStill = false;
            spawnDelayTimer = 0f;
            currentSpaceSpeed = baseSpaceSpeed;
            spaceTimedStartDistance = Vector3.Distance(Locator.GetPlayerTransform().position, transform.position);
        }
        else
        {
            spawnDelayTimer = targetSpawnDelay;
            if (!planetManager.RecentlyTeleported())
            {
                SpawnTarget(planetManager.GetTargetParent(), Locator.GetPlayerTransform().position);
            }
        }
    }

    public void OnErnestoRelease()
    {
        if (targets.Count == 0)
        {
            spawnDelayTimer = targetSpawnDelay;
            SpawnTarget(planetManager.GetTargetParent(), Locator.GetPlayerTransform().position);
        }
    }

    private void SpawnTarget(Transform parent, Vector3 worldPosition, bool isTeleport = false)
    {
        targets.Enqueue((parent.InverseTransformPoint(worldPosition), isTeleport));
    }

    private void UpdateErnestoVisibility()
    {
        if (!ErnestoChase.Instance.QuantumMode)
        {
            return;
        }

        Bounds meshBounds = GetComponentInChildren<SkinnedMeshRenderer>().bounds;
        Plane[] camPlanes = Locator.GetPlayerCamera().GetFrustumPlanes();
        float dot = Vector3.Dot(Locator.GetPlayerCamera().transform.forward,
            transform.position - Locator.GetPlayerCamera().transform.position);
        bool ernestoInView = GeometryUtility.TestPlanesAABB(camPlanes, meshBounds) && dot > 0;

        ernestoFrozen = ernestoInView;
    }

    private void Move()
    {
        if ((targets.Count > 0 || standingStill) && planetManager.HasEnteredPlanet())
        {
            Vector3 targetPos = targets.Count > 0 ? targets.Peek().pos : Locator.GetPlayerTransform().position;
            float distSqr = (targetPos - lastPosition).sqrMagnitude;

            float positionLerp = Mathf.InverseLerp(lastTime, lastTime + (distSqr / currentSpeed), Time.time);

            if (distSqr < 0.01f * 0.01f)
            {
                positionLerp = 1f;
            }

            if (positionLerp < 1f)
            {
                transform.localPosition = Vector3.Lerp(lastPosition, targetPos, positionLerp);

                Quaternion nextRotation = Quaternion.LookRotation(gameObject.GetAttachedOWRigidbody().transform.TransformPoint(targetPos) - transform.position,
                    -planetManager.GetCurrentGravity().CalculateForceAccelerationAtPoint(transform.position));
                transform.rotation = Quaternion.Lerp(lastRotation, nextRotation, positionLerp);

                TryShortcut();
                if (ErnestoChase.Instance.StealthMode)
                {
                    TryProximityRoar();
                }
            }
            else if (targets.Count > 0 && !planetManager.RecentlyTeleported())
            {
                if (targets.Peek().isTeleport)
                {
                    OnTeleportRequired?.Invoke();
                }
            }
        }
    }

    private void TryShortcut()
    {
        float speedLerp = Mathf.InverseLerp(1f, 10f, ErnestoChase.Instance.MovementSpeed);
        Vector3 toPlayer = Locator.GetPlayerTransform().position - transform.position;

        if (!hasTakenShortcut && targets.Count > 20
            && (Locator.GetPlayerTransform().position - transform.position).sqrMagnitude < Mathf.Lerp(15f * 15f, 25f * 25f, speedLerp)
            && !Physics.Raycast(transform.position, toPlayer, toPlayer.magnitude,
            LayerMask.NameToLayer("Default") | LayerMask.NameToLayer("ShipInterior")
            | LayerMask.NameToLayer("IgnoreSun") | LayerMask.NameToLayer("IgnoreOrbRaycast")))
        {
            targets.Clear();
            SpawnTarget(planetManager.GetCurrentPlanet().transform, Locator.GetPlayerTransform().position);
            hasTakenShortcut = true;
        }
        else if (hasTakenShortcut && (Locator.GetPlayerTransform().position - transform.position).sqrMagnitude > 30f * 30f)
        {
            hasTakenShortcut = false;
        }
    }

    private void TryProximityRoar()
    {
        float speedLerp = Mathf.InverseLerp(1f, 10f, ErnestoChase.Instance.MovementSpeed);
        float proximityCutoff = Mathf.Lerp(15f * 15f, 40f * 40f, speedLerp);

        if (!proximityRoar && targets.Count < 20
            && (Locator.GetPlayerTransform().position - transform.position).sqrMagnitude 
            < proximityCutoff)
        {
            OnProximityRoar?.Invoke(true);
            proximityRoar = true;
        }
        else if (proximityRoar 
            && (Locator.GetPlayerTransform().position - transform.position).sqrMagnitude
            > proximityCutoff * 2f)
        {
            OnProximityRoar?.Invoke(false);
            proximityRoar = false;
        }
    }

    private void UpdateMovementSpeed(bool playerOnPlanet)
    {
        bool inBramble = PlayerState.InBrambleDimension();
        var targetsArray = (playerOnPlanet ? targets : spaceTargets).ToArray();
        if (inBramble != lastPlayerBrambleState)
        {
            lastPlayerBrambleState = inBramble;
            if (targetsArray.Length > 0)
            {
                if (inBramble)
                {
                    // speed up
                    brambleSpeedMarkers.Add(targetsArray[targetsArray.Length - 1].pos, true);
                }
                else
                {
                    // slow down
                    brambleSpeedMarkers.Add(targetsArray[targetsArray.Length - 1].pos, false);
                }
            }
        }

        bool inDreamWorld = PlayerState.InDreamWorld();
        if (inDreamWorld != lastPlayerDreamState)
        {
            lastPlayerDreamState = inDreamWorld;
            if (targetsArray.Length > 0)
            {
                if (inDreamWorld)
                {
                    // speed up
                    dreamWorldSpeedMarkers.Add(targetsArray[targetsArray.Length - 1].pos, true);
                }
                else
                {
                    // slow down
                    dreamWorldSpeedMarkers.Add(targetsArray[targetsArray.Length - 1].pos, false);
                }
            }
        }

        if (targetsArray.Length == 0) return;

        Vector3 targetPos = targetsArray[0].pos;

        if (brambleSpeedMarkers.ContainsKey(targetPos))
        {
            if (brambleSpeedMarkers[targetPos])
            {
                currentSpeed *= ErnestoChase.Instance.BrambleSpeedMultiplier;
            }
            else
            {
                currentSpeed /= ErnestoChase.Instance.BrambleSpeedMultiplier;
            }
            brambleSpeedMarkers.Remove(targetPos);
        }

        if (dreamWorldSpeedMarkers.ContainsKey(targetPos))
        {
            if (dreamWorldSpeedMarkers[targetPos])
            {
                currentSpeed *= ErnestoChase.Instance.DreamWorldSpeedMultiplier;
            }
            else
            {
                currentSpeed /= ErnestoChase.Instance.DreamWorldSpeedMultiplier;
            }
            dreamWorldSpeedMarkers.Remove(targetPos);
        }
    }

    public void OnEnterBlackHole(bool fromSpace, bool toSpace)
    {
        targets.Clear();
        standingStill = false;
        transform.position = planetManager.GetStaticParent().TransformPoint(spaceTargets.Peek().pos);
    }

    public void SetMovementEnabled(bool enabled)
    {
        canMove = enabled;
    }

    public bool IsNextSpaceTargetTeleport()
    {
        if (spaceTargets.Count > 0)
        {
            return spaceTargets.Peek().isTeleport;
        }

        return false;
    }
}
