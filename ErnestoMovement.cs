using System.Collections.Generic;
using UnityEngine;

namespace ErnestoChase;

public class ErnestoMovement : MonoBehaviour
{
    public delegate void TeleportRequiredEvent(bool fromSpace);
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

    private bool canMove = true;
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

        enabled = false;
    }


    public void Initialize()
    {
        Vector3 playerStartPos = transform.parent.InverseTransformPoint(Locator.GetPlayerTransform().position);
        transform.localPosition = playerStartPos;
        lastPosition = playerStartPos;
        lastRotation = transform.rotation;

        lastPlayerBrambleState = PlayerState.InBrambleDimension();
        lastPlayerDreamState = PlayerState.InDreamWorld();

        enabled = true;
    }

    private void Update()
    {
        if (planetManager.GetTargetParent() == null) return;

        if (spawnDelayTimer > 0f)
        {
            spawnDelayTimer -= Time.deltaTime;
        }
        else if (planetManager.IsOnPlanet() && !planetManager.HasRecentlyTeleported())
        {
            spawnDelayTimer = targetSpawnDelay;

            Transform targetParent = planetManager.GetTargetParent();
            var arrayTargets = targets.ToArray();
            Vector3 playerPos = targetParent.InverseTransformPoint(Locator.GetPlayerTransform().position);

            if (targets.Count == 0 || (playerPos - arrayTargets[arrayTargets.Length - 1].pos).sqrMagnitude > 0.5f * 0.5f)
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
        UpdateMovementSpeed(playerOnPlanet);

        if (canMove && !ernestoFrozen)
        {
            Move(playerOnPlanet);
        }
    }

    public void SetTravelMode(bool isSpace)
    {
        if (isSpace)
        {
            targets.Clear();
            standingStill = false;
            spawnDelayTimer = targetSpawnDelay;
            currentSpaceSpeed = baseSpaceSpeed;
            spaceTimedStartDistance = Vector3.Distance(Locator.GetPlayerTransform().position, transform.position);
        }
        else
        {
            spawnDelayTimer = targetSpawnDelay;
            if (!planetManager.HasRecentlyTeleported())
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

    private void SpawnSpaceTarget(Vector3 localPosition, bool isTeleport = false)
    {
        spaceTargets.Enqueue((localPosition, isTeleport));
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

    private void Move(bool playerOnPlanet)
    {
        if ((targets.Count > 0 || standingStill) && state.FollowedPlayerToPlanet)
        {
            Vector3 targetPos = targets.Count > 0 ? targets.Peek().pos : Locator.GetPlayerTransform().position;
            float dist = (targetPos - lastPosition).magnitude;

            float positionLerp = Mathf.InverseLerp(lastTime, lastTime + (dist / currentSpeed), Time.time);

            if (dist < 0.01f)
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
            else if (targets.Count > 0 && !planetManager.HasRecentlyTeleported())
            {
                if (targets.Peek().isTeleport)
                {
                    OnTeleportRequired?.Invoke(false);
                }
                else
                {
                    AdvanceTarget();
                }
            }
        }
        else
        {
            float speedLerp = Mathf.InverseLerp(1, 10, ErnestoChase.Instance.SpaceSpeed);

            if (playerOnPlanet && targets.Count > 0 && !state.FollowedPlayerToPlanet && !IsNextSpaceTargetTeleport())
            {
                FollowPlayerToPlanet();

                currentSpaceSpeed += Time.deltaTime * 5f * (speedLerp + 0.5f);
                return;
            }

            if (IsNextSpaceTargetTeleport() && !planetManager.HasRecentlyTeleported())
            {
                if (planetManager.GetErnestoBody().GetVelocity() != Vector3.zero)
                {
                    planetManager.GetErnestoBody().SetVelocity(Vector3.zero);
                }

                Vector3 spaceTargetPos = planetManager.GetStaticParent().TransformPoint(spaceTargets.Peek().pos);
                transform.position = Vector3.MoveTowards(transform.position, spaceTargetPos, currentSpaceSpeed / 50f);

                if ((spaceTargetPos - transform.position).sqrMagnitude < 5f * 5f)
                {
                    spaceTargets.Dequeue();
                    OnTeleportRequired?.Invoke(true);
                    return;
                }

                currentSpaceSpeed += Time.deltaTime * 5f * (speedLerp + 0.5f);
                return;
            }

            transform.LookAt(Locator.GetPlayerTransform(), Locator.GetPlayerTransform().up);

            if (ErnestoChase.Instance.SpaceAccelerationType == "Cumulative")
            {
                planetManager.GetErnestoBody().AddForce(transform.forward * currentSpaceSpeed);
                currentSpaceSpeed += Time.deltaTime * 5f * (speedLerp + 0.5f);
            }
            else if (ErnestoChase.Instance.SpaceAccelerationType == "Linear")
            {
                planetManager.GetErnestoBody().SetVelocity((transform.forward * currentSpaceSpeed) + Locator.GetPlayerBody().GetVelocity());
                currentSpaceSpeed += Time.deltaTime * 5f * speedLerp;
            }
            else
            {
                planetManager.GetErnestoBody().SetVelocity((transform.forward * currentSpaceSpeed) + Locator.GetPlayerBody().GetVelocity());
                currentSpaceSpeed = spaceTimedStartDistance / ErnestoChase.Instance.SpaceTimer;
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

    private void FollowPlayerToPlanet()
    {
        if (state.KillVolumeEnabled)
        {
            state.KillVolumeEnabled = false;
        }

        transform.localPosition = Vector3.MoveTowards(transform.localPosition, targets.Peek().pos, currentSpaceSpeed / 50f);

        if ((targets.Peek().pos - transform.localPosition).sqrMagnitude < 5f * 5f)
        {
            state.FollowedPlayerToPlanet = true;
            state.KillVolumeEnabled = true;
            AdvanceTarget();
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
        if (!fromSpace && !toSpace)
        {
            AdvanceTarget();
            transform.localPosition = targets.Peek().pos;
            AdvanceTarget();
        }
        else if (fromSpace && !toSpace)
        {
            spaceTargets.Clear();
            transform.localPosition = targets.Peek().pos;
            AdvanceTarget();
        }
        else if (!fromSpace && toSpace)
        {
            targets.Clear();
            standingStill = false;
            transform.position = planetManager.GetStaticParent().TransformPoint(spaceTargets.Peek().pos);
            spaceTargets.Dequeue();
            currentSpaceSpeed = baseSpaceSpeed;
            spawnDelayTimer = targetSpawnDelay;
        }
        else if (fromSpace && toSpace)
        {
            transform.localPosition = spaceTargets.Peek().pos;
            spaceTargets.Dequeue();
        }
    }

    public void OnPlayerWarpStarted(bool isSpace)
    {
        if (isSpace)
        {
            SpawnTarget(planetManager.GetTargetParent(), planetManager.GetPlayerParent().TransformPoint(state.LastPlayerPos), true);
        }
        else
        {
            SpawnSpaceTarget(planetManager.GetStaticParent().InverseTransformPoint(Locator.GetPlayerTransform().position), true);
        }
    }

    public void OnPlayerWarpComplete(bool isSpace)
    {
        if (isSpace)
        {
            SpawnSpaceTarget(planetManager.GetStaticParent().InverseTransformPoint(Locator.GetPlayerTransform().position), false);
        }
        else
        {
            OWRigidbody planet = planetManager.GetCurrentPlanetBody();
            if (planet != null)
            {
                ErnestoChase.WriteDebugMessage("Add destination target from warp complete at " + planetManager.GetTargetParent().InverseTransformPoint(Locator.GetPlayerTransform().position));
                SpawnTarget(planet.transform, Locator.GetPlayerTransform().position, false);
            }
            else
            {
                // This might run if the player teleports to a planet and the planet is destroyed before Ernesto gets there
                ErnestoChase.WriteDebugMessage("\"Where is the planet?\"\n   - OnPlayerWarpComplete(), 2025");
            }
        }
    }

    public void SetMovementEnabled(bool enabled)
    {
        canMove = enabled;
    }

    private void AdvanceTarget()
    {
        lastPosition = transform.localPosition;
        lastRotation = transform.rotation;
        lastTime = Time.time;
        targets.Dequeue();
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
