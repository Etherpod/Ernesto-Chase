using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ErnestoChase.ErnestoAI.TargetDataQueue;

namespace ErnestoChase.ErnestoAI;

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
    public delegate void FakeWarpEvent();
    public event FakeWarpEvent TriggerFakeWarpEntry;
    public event FakeWarpEvent TriggerFakeWarpExit;

    ErnestoState state;
    PlanetManager planetManager;
    private SectorDetector sectorDetector;

    private float baseSpeed = 8f;
    private float currentSpeed;
    private float timeAtRelease;
    private float lastTargetCount;
    private readonly float targetCountSmoothTime = 1.5f;

    private float baseSpaceSpeed;
    private float currentSpaceSpeed;

    private Queue<(Vector3 pos, Transform parent, bool isTeleport, int[] sectors)> targets = new();
    private Queue<(Vector3 pos, Transform parent, bool isTeleport, int[] sectors)> spaceTargets = new();

    private TargetDataQueue storedTargets;
    private bool usingStoredTargets = false;
    private bool failedPlanetCheck = false;
    private readonly int storedTargetsFrameDelay = 10;
    private int frameDelay;

    private readonly float targetSpawnDelay = 0.25f;
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
    private bool wasInRingWorld = false;

    private List<uint> observers = [];

    private void Awake()
    {
        state = GetComponent<ErnestoState>();
        planetManager = GetComponent<PlanetManager>();
        sectorDetector = GetComponentInChildren<SectorDetector>(true);

        state.OnDataChanged += OnDataChanged;

        spawnDelayTimer = targetSpawnDelay;
        frameDelay = 0;

        enabled = false;
    }

    private void Start()
    {
        float speedMultiplier = Mathf.Max(0f, state.MovementSpeedMultiplier);

        if (state.QuantumMode)
        {
            speedMultiplier *= 5f;
            ernestoFrozen = false;
        }

        currentSpeed = baseSpeed * speedMultiplier;
        baseSpaceSpeed = baseSpeed / 10f;
        currentSpaceSpeed = baseSpaceSpeed;
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

        enabled = true;
    }

    public void SetStoredTargets(TargetDataQueue queue)
    {
        storedTargets = queue;
        usingStoredTargets = true;
        state.UsingStoredTargets = true;
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

    private void OnDataChanged(uint lastData)
    {
        float speedMultiplier = Mathf.Max(0f, state.MovementSpeedMultiplier);
        
        if (state.QuantumMode)
        {
            speedMultiplier *= 5f;
            UpdateErnestoVisibility(forceUpdate: true);
        }
        else if (ernestoFrozen)
        {
            ernestoFrozen = false;
            OnUpdateVisibility?.Invoke(false);
        }

        currentSpeed = baseSpeed * speedMultiplier;
        baseSpaceSpeed = baseSpeed / 10f;
        currentSpaceSpeed = baseSpaceSpeed;
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

            Transform targetParent = GetTargetParent();
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
            ErnestoChase.WriteDebugMessage("Clear ground targets");
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
            ErnestoChase.WriteDebugMessage("Clear space targets");
            spaceTargets.Clear();
            spawnDelayTimer = targetSpawnDelay;
            if (!planetManager.HasRecentlyTeleported())
            {
                SpawnTarget(GetTargetParent(), Locator.GetPlayerTransform().position);
            }
        }
    }

    public void OnErnestoRelease()
    {
        timeAtRelease = Time.time;
        
        if (usingStoredTargets && state.RemoteID == 0)
        {
            storedTargets.PeekCurrentTarget(out var data);
            state.TimeOffset = data.time - Time.fixedTime;
        }
        else if (targets.Count == 0)
        {
            spawnDelayTimer = targetSpawnDelay;
            planetManager.UpdatePlayerPlanetState();
            SpawnTarget(GetTargetParent(), Locator.GetPlayerTransform().position);
        }

        if (state.QuantumMode)
        {
            UpdateErnestoVisibility(forceUpdate: true);
        }
    }

    public void OnPlayerDeath()
    {
        ErnestoChase.WriteDebugMessage("Let's get outta here");
        targets.Clear();
        spaceTargets.Clear();
        canMove = false;
        enabled = false;

        if (!state.UsingStoredTargets)
        {
            TargetData data = GenerateTargetData(isFinalTarget: true);
            storedTargets.AddTarget(data);

            if (ErnestoChase.InMultiplayer)
            {
                foreach (var id in ErnestoChase.Players)
                {
                    QSBCompat.SendTargetData(id, state.LocalID, data);
                }
            }
        }
        
        OnFinalWarp?.Invoke();

        if (ErnestoChase.InMultiplayer)
        {
            foreach (var id in ErnestoChase.Players)
            {
                QSBCompat.SendErnestoFinalWarp(id, state.LocalID);
            }
        }
    }
    
    public void OnPlayerDeathRemote()
    {
        ErnestoChase.WriteDebugMessage("REMOTE Let's get outta here");
        targets.Clear();
        spaceTargets.Clear();
        canMove = false;
        enabled = false;
        OnFinalWarp?.Invoke();
    }

    private void SpawnTarget(Transform parent, Vector3 worldPosition, bool isTeleport = false)
    {
        List<int> sectorIds = [];
        if (ErnestoChase.InMultiplayer)
        {
            foreach (var sector in Locator.GetPlayerSectorDetector()._sectorList)
            {
                sectorIds.Add(ErnestoChase.QSBInteraction.SectorToID(sector));
            }
        }
        
        targets.Enqueue((parent.InverseTransformPoint(worldPosition), parent, isTeleport, 
            sectorIds.ToArray()));
    }

    private void SpawnSpaceTarget(Vector3 localPosition, bool isTeleport = false)
    {
        List<int> sectorIds = [];
        if (ErnestoChase.InMultiplayer)
        {
            foreach (var sector in Locator.GetPlayerSectorDetector()._sectorList)
            {
                sectorIds.Add(ErnestoChase.QSBInteraction.SectorToID(sector));
            }
        }
        
        spaceTargets.Enqueue((localPosition, planetManager.GetStaticParent(), isTeleport, 
            sectorIds.ToArray()));
    }

    private Transform GetTargetParent(bool ignoreTeleports = false)
    {
        if (ErnestoChase.ActiveIslands.Count > 0)
        {
            return ErnestoChase.ActiveIslands[0].transform;
        }
        
        return ignoreTeleports 
            ? planetManager.GetCurrentPlanetBody().transform 
            : planetManager.GetTargetParent();
    }

    private TargetData GenerateTargetData(bool isTeleportEnter = false, bool isTeleportExit = false,
        bool isFinalTarget = false)
    {
        Transform parent;
        if (transform.parent == planetManager.GetErnestoBody().transform || transform.parent == null)
        {
            parent = planetManager.GetStaticParent();
        }
        else
        {
            parent = transform.parent;
        }

        int[] sectors = null;
        if (targets.Count > 0)
        {
            sectors = targets.Peek().sectors;
        }

        return new TargetData(parent.name, parent.InverseTransformPoint(transform.position), 
            planetManager.GetStaticParent().InverseTransformPoint(transform.position), transform.up,
            Time.fixedTime, isTeleportEnter, isTeleportExit, isFinalTarget, sectors);
    }

    private void UpdateErnestoVisibility(bool forceUpdate = false)
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
                    .GetComponentInParent<PlayerErnesto.ControllableErnesto>()
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

        if ((observers.Count != 0) != ernestoFrozen || forceUpdate)
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
        if (state.FollowedPlayerToPlanet)
        {
            GroundMovement();
        }
        else
        {
            if (playerOnPlanet && targets.Count > 0 && !state.FollowedPlayerToPlanet)
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

                    var data = GenerateTargetData(isTeleportEnter: true);
                    storedTargets.AddTarget(data);
                    ErnestoChase.WriteDebugMessage("Send space teleport: " + data.isTeleportEnter);

                    if (ErnestoChase.InMultiplayer)
                    {
                        foreach (var id in ErnestoChase.Players)
                        {
                            QSBCompat.SendTargetData(id, state.LocalID, data);
                        }
                    }
                    
                    return;
                }

                return;
            }

            SpaceMovement();
        }
    }

    private void GroundMovement()
    {
        Vector3 targetPos;
        if (targets.Count > 0)
        {
            var target = targets.Peek();
            if (target.parent == transform.parent)
            {
                targetPos = target.pos;
            }
            else
            {
                targetPos = transform.parent.InverseTransformPoint(target.parent.TransformPoint(target.pos));
            }
        }
        else
        {
            targetPos = planetManager.GetTargetParent().InverseTransformPoint(Locator.GetPlayerTransform().position);
        }
        
        float dist = (targetPos - lastPosition).magnitude;
        float speed = currentSpeed;
        float time = Time.time - timeAtRelease;
        
        if (state.SpeedAccumulationType == "Linear")
        {
            speed = currentSpeed + time * Mathf.Max(state.SpeedAccumulationRate, 0f);
        }
        else if (state.SpeedAccumulationType == "Squared")
        {
            speed = currentSpeed + (time * time * Mathf.Max(state.SpeedAccumulationRate, 0f));
        }
        else if (state.SpeedAccumulationType == "Exponential")
        {
            speed = currentSpeed + Mathf.Pow(Mathf.Max(state.SpeedAccumulationRate, 0f), time);
        }

        if (state.DistanceSpeedMultiplier != 1f)
        {
            var tempLerp = 1 - Mathf.InverseLerp(lastTime, lastTime + (dist / speed), Time.time);
            var numTargets = lastTargetCount + tempLerp - 1;
            float cutoff = 10f;
            float scalar = 25f;
            var speedLerp = Mathf.LerpUnclamped(1f, state.DistanceSpeedMultiplier, 
                Mathf.Max(0f, (numTargets - cutoff) / (scalar - cutoff)));
            speed *= speedLerp;
        }
        
        //ErnestoChase.WriteDebugMessage(speed);

        if (targets.Count > 1)
        {
            var endTargetRef = targets.ToArray()[1];
            var toLast = targetPos - lastPosition;
            var toNext = targetPos - endTargetRef.pos;
            var dot = Vector3.Dot(toLast.normalized, toNext.normalized);
            var angleSpeedMult = Mathf.Lerp(1f, 0.4f, Mathf.Sqrt((dot + 1f) / 2f));
            speed *= angleSpeedMult;
        }

        float positionLerp = Mathf.InverseLerp(lastTime, lastTime + (dist / speed), Time.time);

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
                OnTeleportRequired?.Invoke(false);
                
                var data = GenerateTargetData(isTeleportEnter: true);
                storedTargets.AddTarget(data);
                ErnestoChase.WriteDebugMessage("Send ground teleport: " + data.isTeleportEnter);

                if (ErnestoChase.InMultiplayer)
                {
                    foreach (var id in ErnestoChase.Players)
                    {
                        QSBCompat.SendTargetData(id, state.LocalID, data);
                    }
                }
            }
            else
            {
                AdvanceTarget();
            }
        }

        lastTargetCount = Mathf.MoveTowards(lastTargetCount, targets.Count, 
            Mathf.Abs(targets.Count - lastTargetCount) * Time.deltaTime / targetCountSmoothTime);
    }

    private void TryShortcut()
    {
        //float speedLerp = state.MovementSpeedMultiplier;
        Vector3 toPlayer = Locator.GetPlayerTransform().position - transform.position;

        if (!hasTakenShortcut && targets.Count > 20
            && (Locator.GetPlayerTransform().position - transform.position).sqrMagnitude < 20f * 20f
            && !Physics.Raycast(transform.position, toPlayer, toPlayer.magnitude - 1f, 
                OWLayerMask.physicalMask))
        {
            targets.Clear();
            lastPosition = transform.localPosition;
            lastRotation = transform.rotation;
            lastTime = Time.time;
            
            SpawnTarget(GetTargetParent(true), Locator.GetPlayerTransform().position);
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

        float speedMult = Mathf.Max(0f, state.SpaceSpeedMultiplier);

        if (state.SpaceAccelerationType == "Physics-Based")
        {
            planetManager.GetErnestoBody().AddForce(transform.forward * currentSpaceSpeed);
            currentSpaceSpeed += Time.fixedDeltaTime * 2f * speedMult;
        }
        else if (state.SpaceAccelerationType == "Linear")
        {
            planetManager.GetErnestoBody().SetVelocity((transform.forward * currentSpaceSpeed) + Locator.GetPlayerBody().GetVelocity());
            currentSpaceSpeed += Time.fixedDeltaTime * 2f * speedMult;
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
        bool sendTarget = false;

        TargetData skippedTeleport = new();
        bool hasSkippedTeleport = false;
        
        if (frameDelay <= 0)
        {
            frameDelay = storedTargetsFrameDelay;

            while (storedTargets.Count > 2
                && storedTargets.PeekCurrentTarget(out var nextData)
                && nextData.time < Time.fixedTime + state.TimeOffset)
            {
                storedTargets.PopCurrentTarget(out var skippedData);
                if (skippedData.isTeleportEnter || skippedData.isTeleportExit ||
                    skippedData.isFinalTarget)
                {
                    ErnestoChase.WriteDebugMessage("I SKIPPED IT OH DEAR OH GOD");
                    skippedTeleport = skippedData;
                    hasSkippedTeleport = true;
                }
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
            
            UpdateSectors();
            sendTarget = true;
        }
        else
        {
            storedTargets.PeekCurrentTarget(out TargetData data);
            targetData = data;
            frameDelay--;
        }

        Vector3 targetPos = failedPlanetCheck ? targetData.worldPosition : targetData.localPosition;
        Quaternion targetRotation = Quaternion.LookRotation(gameObject.GetAttachedOWRigidbody()
                .transform.TransformPoint(targetPos) - transform.position,
                targetData.worldUp);

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

        if (!ProcessStoredTeleportLogic(targetData) && hasSkippedTeleport)
        {
            ProcessStoredTeleportLogic(skippedTeleport);
        }
        
        if (ErnestoChase.InMultiplayer && sendTarget && state.RemoteID == 0)
        {
            foreach (var id in ErnestoChase.Players)
            {
                QSBCompat.SendTargetData(id, state.LocalID, targetData);
            }
        }
    }

    private bool ProcessStoredTeleportLogic(TargetData targetData)
    {
        if (targetData.isTeleportEnter)
        {
            TriggerFakeWarpEntry?.Invoke();
        }
        else if (targetData.isTeleportExit)
        {
            var refSector = Locator.GetRingWorldController()?
                .transform.Find("Sector_RingInterior").GetComponent<Sector>();
            bool inRingWorld = refSector != null && targetData.sectors != null
                && targetData.sectors.ToList().Contains(ErnestoChase.QSBInteraction.SectorToID(refSector));
            
            TriggerFakeWarpExit?.Invoke();
            //ErnestoChase.SpectateManager.RefreshDreamWorld(state.RemoteID);
            //ErnestoChase.SpectateManager.RefreshRingWorld(state.RemoteID, state.LocalID, inRingWorld, false);
        }
        else if (targetData.isFinalTarget)
        {
            OnFinalWarp?.Invoke();
        }
        else
        {
            return false;
        }
        
        ErnestoChase.WriteDebugMessage("... Processing stored teleport logic");

        return true;
    }

    private void TryProximityRoar()
    {
        float speedLerp = state.MovementSpeedMultiplier;
        float proximityCutoff = Mathf.LerpUnclamped(20f, 30f * 30f, speedLerp);

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
            lerpTime = Mathf.Lerp(5f, 15f, distLerp) + Random.Range(0f, 10f);

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

    public void UpdateSectors()
    {
        if (!sectorDetector.gameObject.activeInHierarchy ||
            !storedTargets.PeekCurrentTarget(out var data))
        {
            return;
        }
        
        var newSectors = new List<Sector>();
        foreach (var id in data.sectors)
        {
            var sector = ErnestoChase.QSBInteraction.IDToSector(id);
            if (sector != null)
            {
                newSectors.Add(sector);
            }
        }
        
        bool disableRingWorld = false;
        bool disableDreamWorld = false;

        for (int i = sectorDetector._sectorList.Count - 1; i >= 0; i--)
        {
            if (!newSectors.Contains(sectorDetector._sectorList[i]))
            {
                if (!disableRingWorld && 
                    sectorDetector._sectorList[i].GetComponentInParent<RingWorldController>())
                {
                    disableRingWorld = true;
                }
                if (!disableDreamWorld && 
                    sectorDetector._sectorList[i].GetComponentInParent<DreamWorldController>())
                {
                    disableDreamWorld = true;
                }
                
                sectorDetector.RemoveSector(sectorDetector._sectorList[i]);
            }
        }

        bool loadRingWorld = false;
        bool loadDreamWorld = false;
        
        foreach (var sector in newSectors)
        {
            if (!loadRingWorld && sector.GetComponentInParent<RingWorldController>())
            {
                loadRingWorld = true;
            }
            if (!loadDreamWorld && sector.GetComponentInParent<DreamWorldController>())
            {
                loadDreamWorld = true;
            }
            
            if (!sectorDetector._sectorList.Contains(sector))
            {
                sectorDetector.AddSector(sector);
            }
        }

        if (loadRingWorld)
        {
            ErnestoChase.SpectateManager.LoadRingWorld();
        }
        else if (disableRingWorld)
        {
            ErnestoChase.SpectateManager.UnloadRingWorld();
        }
        
        if (loadDreamWorld)
        {
            ErnestoChase.SpectateManager.LoadDreamWorld();
        }
        else if (disableDreamWorld)
        {
            ErnestoChase.SpectateManager.UnloadDreamWorld();
        }
    }

    public void ClearSectors()
    {
        bool disableRingWorld = false;
        bool disableDreamWorld = false;
        
        for (int i = sectorDetector._sectorList.Count - 1; i >= 0; i--)
        {
            if (!disableRingWorld && 
                sectorDetector._sectorList[i].GetComponentInParent<RingWorldController>())
            {
                disableRingWorld = true;
            }
            else if (!disableDreamWorld && 
                sectorDetector._sectorList[i].GetComponentInParent<DreamWorldController>())
            {
                disableDreamWorld = true;
            }
            
            sectorDetector.RemoveSector(sectorDetector._sectorList[i]);
        }

        if (disableRingWorld)
        {
            ErnestoChase.SpectateManager.UnloadRingWorld();
        }
        else if (disableDreamWorld)
        {
            ErnestoChase.SpectateManager.UnloadDreamWorld();
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
        
        var data = GenerateTargetData(isTeleportExit: true);
        storedTargets.AddTarget(data);

        if (ErnestoChase.InMultiplayer)
        {
            foreach (var id in ErnestoChase.Players)
            {
                QSBCompat.SendTargetData(id, state.LocalID, data);
            }
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
            SpawnTarget(GetTargetParent(), planetManager.GetPlayerParent().TransformPoint(state.LastPlayerPos), true);
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
                SpawnTarget(GetTargetParent(true), Locator.GetPlayerTransform().position, false);
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

    private void OnDestroy()
    {
        state.OnDataChanged -= OnDataChanged;
    }
}
