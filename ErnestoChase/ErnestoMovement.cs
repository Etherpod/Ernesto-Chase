using System.Collections.Generic;
using System.ComponentModel.Design;
using UnityEngine;
using static ErnestoChase.TargetDataQueue;

namespace ErnestoChase;

public class ErnestoMovement : MonoBehaviour
{
    public delegate void TeleportRequiredEvent(bool fromSpace);
    public event TeleportRequiredEvent OnTeleportRequired;
    public delegate void ProximityRoarEvent(bool inRange);
    public event ProximityRoarEvent OnProximityRoar;
    public delegate void SpaceWarpEvent();
    public event SpaceWarpEvent OnSpaceWarp;
    public delegate void TakeShortcutEvent();
    public event TakeShortcutEvent OnTakeShortcut;
    public delegate void UpdateVisibilityEvent(bool visible);
    public event UpdateVisibilityEvent OnUpdateVisibility;
    public delegate void FinalWarpEvent();
    public event FinalWarpEvent OnFinalWarp;

    ErnestoState state;
    PlanetManager planetManager;

    private float baseSpeed = 8f;
    private float currentSpeed;

    private float baseSpaceSpeed;
    private float currentSpaceSpeed;

    private Queue<(Vector3 pos, bool isTeleport)> targets = new();
    private Queue<(Vector3 pos, bool isTeleport)> spaceTargets = new();

    private TargetDataQueue storedTargets;
    private bool usingStoredTargets = false;
    private bool failedPlanetCheck = false;
    private readonly int storedTargetsFrameDelay = 10;
    private int frameDelay;

    private float targetSpawnDelay = 0.25f;
    private float spawnDelayTimer;

    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float lastTime;

    private bool lerpingToTarget = false;
    private bool lerpingToPlanet = false;
    private float lerpTime;
    private float lerpStartTime;
    private Vector3 lerpStartPos;
    private Vector3 lerpTarget;

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

    private List<uint> observers = [];
    private bool warpOutOnTargetComplete = false;

    private void Awake()
    {
        state = GetComponent<ErnestoState>();
        planetManager = GetComponent<PlanetManager>();

        float speedMultiplier;
        float speedLerp = state.MovementSpeed;
        if (speedLerp < 0.5f)
        {
            speedMultiplier = Mathf.Lerp(0.2f, 1f, speedLerp * 2);
        }
        else
        {
            speedMultiplier = Mathf.Lerp(1f, 4f, (speedLerp - 0.5f) * 2);
        }

        if (state.QuantumMode)
        {
            speedMultiplier *= 5f;
            ernestoFrozen = false;
        }

        baseSpeed *= speedMultiplier;
        currentSpeed = baseSpeed;

        baseSpaceSpeed = baseSpeed / 10f;
        currentSpaceSpeed = baseSpaceSpeed;

        spawnDelayTimer = targetSpawnDelay;
        frameDelay = 0;

        enabled = false;
    }

    public void Initialize()
    {
        // no parent being set?
        Vector3 playerStartPos = transform.parent.InverseTransformPoint(Locator.GetPlayerTransform().position);
        transform.localPosition = playerStartPos;
        lastPosition = playerStartPos;
        lastRotation = transform.rotation;

        lastPlayerBrambleState = PlayerState.InBrambleDimension();
        lastPlayerDreamState = PlayerState.InDreamWorld();

        if (storedTargets == null)
        {
            storedTargets = new();
        }

        enabled = state.AIEnabled;
    }

    public void SetStoredTargets(TargetDataQueue queue)
    {
        storedTargets = queue;
        usingStoredTargets = true;
    }

    public void AddTargetData(TargetData data)
    {
        storedTargets.AddTarget(data);
    }

    public TargetDataQueue GetStoredTargets()
    {
        return storedTargets;
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        baseSpeed *= multiplier;
    }

    private void FixedUpdate()
    {
        if (planetManager.GetTargetParent() == null || usingStoredTargets) return;

        if (spawnDelayTimer > 0f)
        {
            spawnDelayTimer -= Time.fixedDeltaTime;
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
        if (usingStoredTargets)
        {
            FollowStoredTargets();

            if (state.RemoteID > 0)
            {
                UpdateErnestoVisibility();
            }

            return;
        }
        else
        {
            if (frameDelay > 0)
            {
                frameDelay--;
            }
            else
            {
                frameDelay = storedTargetsFrameDelay;
                TargetData data = GenerateTargetData();
                storedTargets.AddTarget(data);
                //ErnestoChase.WriteDebugMessage("Send parent: " + data.parent);

                if (ErnestoChase.InMultiplayer)
                {
                    foreach (var id in ErnestoChase.Players)
                    {
                        QSBCompat.SendTargetData(id, state.LocalID, data);
                    }
                }
            }
        }

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
            lerpingToTarget = false;
            lerpingToPlanet = false;
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
        if (usingStoredTargets && state.RemoteID == 0)
        {
            storedTargets.PeekCurrentTarget(out var data);
            state.TimeOffset = data.time - Time.fixedTime;
        }
        else if (targets.Count == 0)
        {
            spawnDelayTimer = targetSpawnDelay;
            planetManager.UpdatePlayerPlanetState(true);
            SpawnTarget(planetManager.GetTargetParent(), Locator.GetPlayerTransform().position);
        }
    }

    public void OnPlayerDeath()
    {
        ErnestoChase.WriteDebugMessage("Let's get outta here");
        if (targets.Count > 0 && !state.CaughtPlayer)
        {
            ErnestoChase.WriteDebugMessage("Warp at end of path");
            var targetArray = targets.ToArray();
            var lastTarget = targetArray[targetArray.Length - 1];
            lastTarget.isTeleport = true;
            targetArray[targetArray.Length - 1] = lastTarget;
            warpOutOnTargetComplete = true;
        }
        else
        {
            ErnestoChase.WriteDebugMessage("Instant warp");
            targets.Clear();
            spaceTargets.Clear();
            canMove = false;
            enabled = false;
            OnFinalWarp?.Invoke();
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

    private TargetData GenerateTargetData()
    {
        Transform parent;
        if (transform.parent == planetManager.GetErnestoBody().transform)
        {
            parent = planetManager.GetStaticParent();
        }
        else
        {
            parent = transform.parent;
        }

        return new TargetData(parent.name, parent.InverseTransformPoint(transform.position), 
            planetManager.GetStaticParent().InverseTransformPoint(transform.position), 
            Time.fixedTime);
    }

    private void UpdateErnestoVisibility()
    {
        // Add event to ErnestoEffects that toggles audio/animation for everyone when visibility changes

        if (!state.QuantumMode)
        {
            return;
        }

        bool ernestoInView = false;
        if (!ErnestoChase.InMultiplayer || !ErnestoChase.QSBAPI.GetPlayerDead(ErnestoChase.QSBAPI.GetLocalPlayerID()))
        {
            Bounds meshBounds = GetComponentInChildren<SkinnedMeshRenderer>().bounds;
            var camera = ErnestoChase.Instance.ErnestoMorph && state.RemoteID == 0
                ? Locator.GetPlayerBody()
                    .GetComponentInParent<ControllableErnesto>()
                    .transform.Find("ScaleRoot/ErnestoCam")
                    .GetComponent<OWCamera>()
                : Locator.GetPlayerCamera();
            Plane[] camPlanes = camera.GetFrustumPlanes();
            float dot = Vector3.Dot(camera.transform.forward,
                transform.position - camera.transform.position);
            ernestoInView = dot > 0 && GeometryUtility.TestPlanesAABB(camPlanes, meshBounds);
        }

        if (ernestoInView && !observers.Contains(0))
        {
            observers.Add(0);
        }
        else if (!ernestoInView && observers.Contains(0))
        {
            observers.Remove(0);
        }

        if ((observers.Count != 0) != ernestoFrozen)
        {
            ernestoFrozen = observers.Count != 0;

            if (state.RemoteID > 0)
            {
                QSBCompat.SendVisibilityState(state.RemoteID, state.LocalID, ernestoFrozen);
            }

            OnUpdateVisibility?.Invoke(ernestoFrozen);
        }
    }

    public void UpdateVisibilityRemote(uint from, bool visible)
    {
        if (visible && !observers.Contains(from))
        {
            ErnestoChase.WriteDebugMessage("See remote!!!!!!!!!!!!!!!");
            observers.Add(from);
        }
        else if (!visible && observers.Contains(from))
        {
            ErnestoChase.WriteDebugMessage("Not see remote........");
            observers.Remove(from);
        }

        UpdateErnestoVisibility();
    }

    private void Move(bool playerOnPlanet)
    {
        if ((targets.Count > 0 || standingStill) && state.FollowedPlayerToPlanet)
        {
            GroundMovement();
        }
        else
        {
            if (playerOnPlanet && targets.Count > 0 && !state.FollowedPlayerToPlanet && !IsNextSpaceTargetTeleport())
            {
                FollowPlayerToPlanet();
                return;
            }

            if (IsNextSpaceTargetTeleport() && !planetManager.HasRecentlyTeleported())
            {
                if (!lerpingToTarget)
                {
                    lerpingToPlanet = false;

                    lerpStartTime = Time.time;
                    lerpStartPos = transform.position;
                    lerpTarget = planetManager.GetStaticParent().TransformPoint(spaceTargets.Peek().pos);
                    float distLerp = Mathf.InverseLerp(50f * 50f, 2000f * 2000f, (lerpTarget - lerpStartPos).sqrMagnitude);
                    lerpTime = Mathf.Lerp(5f, 15f, distLerp);
                    lerpingToTarget = true;
                }

                if (planetManager.GetErnestoBody().GetVelocity() != Vector3.zero)
                {
                    planetManager.GetErnestoBody().SetVelocity(Vector3.zero);
                }

                float timeLerp = Mathf.InverseLerp(lerpStartTime, lerpStartTime + lerpTime, Time.time);
                transform.position = Vector3.Slerp(lerpStartPos, lerpTarget, timeLerp);

                if (timeLerp == 1)
                {
                    spaceTargets.Dequeue();
                    lerpingToTarget = false;
                    OnTeleportRequired?.Invoke(true);
                    return;
                }

                return;
            }

            SpaceMovement();
        }
    }

    private void GroundMovement()
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
            transform.rotation = Quaternion.Slerp(lastRotation, nextRotation, positionLerp);

            TryShortcut();
            if (state.StealthMode)
            {
                TryProximityRoar();
            }
        }
        else if (targets.Count > 0 && !planetManager.HasRecentlyTeleported())
        {
            if (targets.Peek().isTeleport)
            {
                if (warpOutOnTargetComplete)
                {
                    ErnestoChase.WriteDebugMessage("Try final teleport, count is: " + targets.ToArray().Length);
                }
                if (warpOutOnTargetComplete && targets.ToArray().Length == 1)
                {
                    targets.Clear();
                    spaceTargets.Clear();
                    canMove = false;
                    enabled = false;
                    OnFinalWarp?.Invoke();
                }
                else
                {
                    OnTeleportRequired?.Invoke(false);
                }
            }
            else
            {
                AdvanceTarget();
            }
        }
    }

    private void TryShortcut()
    {
        float speedLerp = state.MovementSpeed;
        Vector3 toPlayer = Locator.GetPlayerTransform().position - transform.position;

        if (!hasTakenShortcut && targets.Count > 20
            && (Locator.GetPlayerTransform().position - transform.position).sqrMagnitude < Mathf.Lerp(15f * 15f, 25f * 25f, speedLerp)
            && !Physics.Raycast(transform.position, toPlayer, toPlayer.magnitude,
            LayerMask.NameToLayer("Default") | LayerMask.NameToLayer("ShipInterior")
            | LayerMask.NameToLayer("IgnoreSun") | LayerMask.NameToLayer("IgnoreOrbRaycast")))
        {
            targets.Clear();
            SpawnTarget(planetManager.GetCurrentPlanet().transform, Locator.GetPlayerTransform().position);
            OnTakeShortcut?.Invoke();
            hasTakenShortcut = true;
        }
        else if (hasTakenShortcut && (Locator.GetPlayerTransform().position - transform.position).sqrMagnitude > 30f * 30f)
        {
            hasTakenShortcut = false;
        }
    }

    private void SpaceMovement()
    {
        transform.LookAt(Locator.GetPlayerTransform(), Locator.GetPlayerTransform().up);

        float speedLerp = state.SpaceSpeed;

        if (state.SpaceAccelerationType == "Cumulative")
        {
            planetManager.GetErnestoBody().AddForce(transform.forward * currentSpaceSpeed);
            currentSpaceSpeed += Time.fixedDeltaTime * 5f * (speedLerp + 0.5f);
        }
        else if (state.SpaceAccelerationType == "Linear")
        {
            planetManager.GetErnestoBody().SetVelocity((transform.forward * currentSpaceSpeed) + Locator.GetPlayerBody().GetVelocity());
            currentSpaceSpeed += Time.fixedDeltaTime * 5f * speedLerp;
        }
        else
        {
            planetManager.GetErnestoBody().SetVelocity((transform.forward * currentSpaceSpeed) + Locator.GetPlayerBody().GetVelocity());
            currentSpaceSpeed = spaceTimedStartDistance / state.SpaceTimer;
        }
    }

    private void FollowStoredTargets()
    {
        if (storedTargets.Count == 0) return;

        TargetData targetData;

        if (frameDelay <= 0)
        {
            frameDelay = storedTargetsFrameDelay;

            int num = 0;

            while (storedTargets.Count > 2
                && storedTargets.PeekCurrentTarget(out var nextData)
                && nextData.time < Time.fixedTime + state.TimeOffset)
            {
                storedTargets.PopCurrentTarget(out _);
                num++;
            }

            storedTargets.PopCurrentTarget(out _);
            storedTargets.PeekCurrentTarget(out var data);
            targetData = data;
            //ErnestoChase.WriteDebugMessage("   Receive: " + targetData.parent);

            if (transform.parent.name != targetData.parent)
            {
                var parent = GameObject.Find(targetData.parent);
                if (parent != null)
                {
                    //ErnestoChase.WriteDebugMessage("\n\n\n\n\n\n" + parent + "\n\n\n\n\n\n");
                    transform.parent = parent.transform;
                    transform.localPosition = targetData.localPosition;
                    failedPlanetCheck = false;
                }
                else if (!failedPlanetCheck)
                {
                    failedPlanetCheck = true;
                    transform.parent = planetManager.GetStaticParent();
                }
            }

            lastPosition = transform.localPosition;
            lastRotation = transform.rotation;
            lastTime = Time.fixedTime;
        }
        else
        {
            storedTargets.PeekCurrentTarget(out TargetData data);
            targetData = data;
            frameDelay--;
        }

        Vector3 targetPos = failedPlanetCheck ? targetData.worldPosition : targetData.localPosition;
        Quaternion targetRotation = Quaternion.LookRotation(gameObject.GetAttachedOWRigidbody().transform.TransformPoint(targetPos) - transform.position,
                -planetManager.GetCurrentAlignGravity(true).CalculateForceAccelerationAtPoint(transform.position));

        float timeLerp = 1f - (frameDelay / (float)storedTargetsFrameDelay);
        transform.localPosition = Vector3.Lerp(lastPosition, targetPos, timeLerp);

        if ((targetPos - lastPosition).sqrMagnitude < 0.01f)
        {
            transform.rotation = lastRotation;
        }
        else
        {
            transform.rotation = Quaternion.Slerp(lastRotation, targetRotation, timeLerp);
        }
    }

    private void TryProximityRoar()
    {
        float speedLerp = state.MovementSpeed;
        float proximityCutoff = Mathf.Lerp(20f * 20f, 40f * 40f, speedLerp);

        if (!proximityRoar && targets.Count < 20
            && (Locator.GetPlayerTransform().position - transform.position).sqrMagnitude 
            < proximityCutoff)
        {
            OnProximityRoar?.Invoke(true);
            proximityRoar = true;
        }
        else if (proximityRoar 
            && (Locator.GetPlayerTransform().position - transform.position).sqrMagnitude
            > proximityCutoff * 3.5f)
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

        if (!lerpingToPlanet)
        {
            lerpingToTarget = false;

            lerpStartTime = Time.time;
            lerpStartPos = transform.localPosition;
            lerpTarget = targets.Peek().pos;
            float distLerp = Mathf.InverseLerp(50f * 50f, 2000f * 2000f, (lerpTarget - lerpStartPos).sqrMagnitude);
            lerpTime = Mathf.Lerp(5f, 15f, distLerp);

            lerpingToPlanet = true;
        }

        float timeLerp = Mathf.InverseLerp(lerpStartTime, lerpStartTime + lerpTime, Time.time);
        transform.localPosition = Vector3.Slerp(lerpStartPos, targets.Peek().pos, timeLerp);

        if (timeLerp == 1)
        {
            state.FollowedPlayerToPlanet = true;
            state.KillVolumeEnabled = true;
            AdvanceTarget();
            lerpingToPlanet = false;
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
                currentSpeed *= state.BrambleSpeedMultiplier;
            }
            else
            {
                currentSpeed /= state.BrambleSpeedMultiplier;
            }
            brambleSpeedMarkers.Remove(targetPos);
        }

        if (dreamWorldSpeedMarkers.ContainsKey(targetPos))
        {
            if (dreamWorldSpeedMarkers[targetPos])
            {
                currentSpeed *= state.DreamWorldSpeedMultiplier;
            }
            else
            {
                currentSpeed /= state.DreamWorldSpeedMultiplier;
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
            transform.position = planetManager.GetStaticParent().TransformPoint(spaceTargets.Peek().pos);
            spaceTargets.Dequeue();
            currentSpaceSpeed = baseSpaceSpeed;
            spawnDelayTimer = targetSpawnDelay;
            spaceTimedStartDistance = Vector3.Distance(Locator.GetPlayerTransform().position, transform.position);
            OnSpaceWarp?.Invoke();
        }
    }

    public void OnPlayerWarpStarted(bool isSpace)
    {
        if (isSpace)
        {
            SpawnSpaceTarget(planetManager.GetStaticParent().InverseTransformPoint(Locator.GetPlayerTransform().position), true);
        }
        else
        {
            SpawnTarget(planetManager.GetTargetParent(), planetManager.GetPlayerParent().TransformPoint(state.LastPlayerPos), true);
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
                SpawnTarget(planet.transform, Locator.GetPlayerTransform().position, false);
            }
            else
            {
                // This might run if the player teleports to a planet and the planet is destroyed before Ernesto gets there
                // nevermind probably not
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
