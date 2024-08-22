using Mono.Cecil.Cil;
using OWML.ModHelper.Events;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XGamingRuntime;

namespace ErnestoChase;

public class ErnestoController : MonoBehaviour
{
    [SerializeField]
    private OWTriggerVolume killVolume;
    [SerializeField]
    private OWAudioSource loopingAudio;
    [SerializeField]
    private OWAudioSource oneShotAudio;
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private GameObject ernestoMesh;
    [SerializeField]
    private SingularityWarpEffect blackHolePrefab;
    [SerializeField]
    private SingularityWarpEffect whiteHolePrefab;
    [SerializeField]
    private Light anglerLight;

    private float speed = 8f;
    private float currentSpeed;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float lastTime;
    //private Queue<GameObject> targets = new();
    private Queue<(Vector3, bool)> targets = new();
    private Queue<(Vector3, bool)> spaceTargets = new();
    private GameObject targetPrefab;
    private float targetSpawnDelay = 0.25f;
    private float spawnDelayTimer;
    private bool lastPlayerAtmosphereState = false;
    private float baseSpaceSpeed;
    private float currentSpaceSpeed;
    private GameObject currentPlanet;
    private ForceVolume currentGravity;
    private List<GameObject> teleportPlanets = new();
    private bool reachedAtmoEnterPos = true;
    private Vector3 lastPlayerPos;
    private OWRigidbody rigidbody;
    private Transform staticTransformParent;
    private Vector3 startVelocity;
    private Queue<Vector3> teleportVelocities = new();
    private bool hasCheckedDetector = false;
    private bool waitingOnTeleport = false;
    private bool teleportedIntoSpace = false;
    private bool ernestoReleased = false;
    private bool startedDeathSequence = false;
    private SingularityWarpEffect blackHole;
    private SingularityWarpEffect whiteHole;
    private bool movementPaused = false;
    private bool playerCollided = false;
    private float baseLightRange;
    private float baseMeshScale;
    private Dictionary<Vector3, (bool, bool)> brambleSpeedMarkers = [];
    private bool lastPlayerBrambleState;
    private Dictionary<Vector3, (bool, bool)> dreamWorldSpeedMarkers = [];
    private bool lastPlayerDreamState;

    private void Start()
    {
        killVolume.OnEntry += OnEntry;
        killVolume.OnExit += OnExit;

        ErnestoChase.Instance.ModHelper.Console.WriteLine("In game");
        targetPrefab = ErnestoChase.LoadPrefab("Assets/ErnestoChase/ErnestoTarget.prefab");
        rigidbody = ErnestoChase.Instance.ernestoBody;
        animator = GetComponentInChildren<Animator>();
        loopingAudio = GetComponentInChildren<OWAudioSource>();
        GameObject targetsParent = ErnestoChase.LoadPrefab("Assets/ErnestoChase/SpaceTargetsParent.prefab");
        staticTransformParent = Instantiate(targetsParent).transform;
        spawnDelayTimer = targetSpawnDelay;
        ErnestoChase.WriteDebugMessage(Mathf.InverseLerp(1, 10, ErnestoChase.Instance.MovementSpeed));
        float num = Mathf.InverseLerp(1, 10, ErnestoChase.Instance.MovementSpeed);
        float num2;
        if (num < 0.5f)
        {
            num2 = Mathf.Lerp(0.2f, 1f, num * 2);
        }
        else
        {
            num2 = Mathf.Lerp(1f, 4f, (num - 0.5f) * 2);
        }
        speed *= num2;
        baseSpaceSpeed = speed / 10;
        currentSpaceSpeed = baseSpaceSpeed;
        currentSpeed = speed;
        AssetBundleUtilities.ReplaceShaders(blackHolePrefab.gameObject);
        AssetBundleUtilities.ReplaceShaders(whiteHolePrefab.gameObject);
        blackHolePrefab._warpedObjectGeometry = ernestoMesh;
        whiteHolePrefab._warpedObjectGeometry = ernestoMesh;
        blackHole = Instantiate(blackHolePrefab);
        whiteHole = Instantiate(whiteHolePrefab);
        baseMeshScale = ernestoMesh.transform.localScale.magnitude;
        ernestoMesh.transform.localScale = Vector3.zero;
        baseLightRange = anglerLight.range;
        anglerLight.range = baseLightRange * (ernestoMesh.transform.localScale.magnitude / baseMeshScale);
    }

    private void FixedUpdate()
    {
        // Wait until player detector has gravtiy volumes
        if (LoadManager.GetCurrentScene() != OWScene.SolarSystem || !ErnestoChase.Instance.playerDetectorReady || !TimeLoop.IsTimeFlowing()) return;

        // Check if player is on planet
        if (!hasCheckedDetector)
        {
            OWRigidbody body = GetCurrentPlanetBody();
            if (body)
            {
                // Parent Ernesto to planet
                currentPlanet = body.gameObject;
                transform.parent = currentPlanet.transform;
            }
            else
            {
                // Prepare space travel
                transform.parent = rigidbody.transform;
            }

            ErnestoChase.WriteDebugMessage(transform.parent);

            Vector3 newPos = transform.parent.InverseTransformPoint(Locator.GetPlayerTransform().position);
            transform.localPosition = newPos;
            lastPosition = newPos;
            lastRotation = transform.rotation;
            lastPlayerAtmosphereState = body;
            lastPlayerBrambleState = PlayerState.InBrambleDimension();
            StartCoroutine(ErnestoReleaseDelay());
            hasCheckedDetector = true;
        }

        anglerLight.range = baseLightRange * (ernestoMesh.transform.localScale.magnitude / baseMeshScale);

        if (playerCollided && !ErnestoChase.Instance.caughtPlayer && ernestoReleased)
        {
            ErnestoChase.Instance.caughtPlayer = true;
        }

        if (ErnestoChase.Instance.caughtPlayer)
        {
            if (!startedDeathSequence)
            {
                startedDeathSequence = true;
                transform.parent = null;
                transform.position = Locator.GetPlayerTransform().position;
                loopingAudio.FadeOut(2f);
                animator.SetTrigger("Stop");
                Locator.GetDeathManager().KillPlayer(DeathType.Digestion);
            }
            return;
        }

        // Check if player is on a planet
        bool playerOnPlanet = GetCurrentPlanetBody();

        /*Transform positionParent = playerOnPlanet ? (teleportPlanets.Count > 0 
            ? teleportPlanets[teleportPlanets.Count - 1].transform : currentPlanet.transform) : staticTransformParent;*/
        Transform positionParent;

        if (playerOnPlanet)
        {
            if (teleportPlanets.Count > 0)
            {
                GameObject planet = teleportPlanets[teleportPlanets.Count - 1];
                positionParent = planet ? planet.transform : staticTransformParent;
            }
            else
            {
                positionParent = currentPlanet.transform;
            }
        }
        else
        {
            positionParent = staticTransformParent;
        }

        if (ernestoReleased && !movementPaused)
        {
            UpdateMovement(playerOnPlanet, positionParent);
        }

        // Update spawn delay timer
        if (spawnDelayTimer > 0f)
        {
            spawnDelayTimer -= Time.deltaTime;
        }
        // Delay target spawning until player is in an atompshere
        //// TODO: Delay if targets overlap
        else if (playerOnPlanet && !waitingOnTeleport && positionParent.TransformPoint(lastPlayerPos) != Locator.GetPlayerTransform().position)
        {
            spawnDelayTimer = targetSpawnDelay;
            Transform parent = teleportPlanets.Count > 0 ? teleportPlanets[teleportPlanets.Count - 1].transform : currentPlanet.transform;
            //ErnestoChase.WriteDebugMessage("Spawned on: " + parent.name);
            SpawnTarget(parent, Locator.GetPlayerTransform().position, false);
        }

        // Update last player position
        //ErnestoChase.WriteDebugMessage(positionParent.name);
        lastPlayerPos = positionParent.InverseTransformPoint(Locator.GetPlayerTransform().position);
    }

    private void UpdateMovement(bool playerOnPlanet, Transform positionParent)
    {
        // Check if planet state has changed since last frame
        if (lastPlayerAtmosphereState != playerOnPlanet)
        {
            lastPlayerPos = positionParent.InverseTransformPoint(lastPlayerPos);

            if (!waitingOnTeleport)
            {
                lastPlayerAtmosphereState = playerOnPlanet;
            }

            // Transition to space travel if player has entered space
            if (!playerOnPlanet && !waitingOnTeleport && !teleportedIntoSpace)
            {
                ErnestoChase.WriteDebugMessage("Exited atmosphere");
                targets.Clear();
                teleportPlanets.Clear();
                currentSpaceSpeed = baseSpaceSpeed;
                reachedAtmoEnterPos = false;
                spawnDelayTimer = 0f;
                staticTransformParent.GetComponent<OWRigidbody>().SetVelocity(Vector3.zero);
                rigidbody.SetPosition(transform.position);
                rigidbody.SetVelocity(transform.parent.GetComponent<OWRigidbody>()._currentVelocity);
                startVelocity = rigidbody._currentVelocity;
                transform.parent = rigidbody.transform;
            }
            // Transition to planetary travel if player has entered atmosphere
            else if (playerOnPlanet)
            {
                ErnestoChase.WriteDebugMessage("Re-entered atmosphere");
                if (!(spaceTargets.Count > 0 && spaceTargets.Peek().Item2))
                {
                    ErnestoChase.WriteDebugMessage("Teleport planets list: " + teleportPlanets.Count);
                    rigidbody.SetVelocity(Vector3.zero);
                    currentPlanet = GetCurrentPlanetBody().gameObject;
                    transform.parent = currentPlanet.transform;
                }
                spawnDelayTimer = targetSpawnDelay;
                if (!waitingOnTeleport)
                {
                    SpawnTarget(currentPlanet.transform, Locator.GetPlayerTransform().position, false);
                }
            }
        }

        UpdateMovementSpeed(playerOnPlanet);

        // Planetary travel
        if (targets.Count > 0 && reachedAtmoEnterPos)
        {
            Vector3 targetPos = targets.Peek().Item1;
            float dist = (targetPos - lastPosition).magnitude;

            /*if (transform.parent == positionParent)
            {
                if (PlayerState.InBrambleDimension())
                {
                    newSpeed *= ErnestoChase.Instance.BrambleSpeedMultiplier;
                }
                if (PlayerState.InDreamWorld())
                {
                    newSpeed *= ErnestoChase.Instance.DreamWorldSpeedMultiplier;
                }
            }*/

            float num = Mathf.InverseLerp(lastTime, lastTime + (dist / currentSpeed), Time.time);

            // If targets are overlapping num will stay at zero, prevent that
            if (dist < 0.01f)
            {
                num = 1f;
            }
            //ErnestoChase.WriteDebugMessage(num);

            // Transitioning between targets
            if (num < 1f)
            {
                // Move Ernesto
                transform.localPosition = Vector3.Lerp(lastPosition, targetPos, num);
                Quaternion nextRotation = Quaternion.LookRotation(currentPlanet.transform.TransformPoint(targets.Peek().Item1) - transform.position,
                    -currentGravity.CalculateForceAccelerationAtPoint(transform.position));
                transform.rotation = Quaternion.Lerp(lastRotation, nextRotation, num);
            }
            // Don't run this code if player detector is updating after teleport
            // Otherwise Ernesto will teleport before it finishes
            else if (!waitingOnTeleport)
            {
                // Check if last target reached is a teleport origin
                if (targets.Peek().Item2)
                {
                    // Transition to space travel
                    if (teleportPlanets[0] == null)
                    {
                        teleportedIntoSpace = false;
                        movementPaused = true;
                        ErnestoChase.WriteDebugMessage("Exited atmosphere in teleport");

                        blackHole.transform.parent = transform.parent;
                        blackHole.transform.localPosition = transform.localPosition;
                        blackHole.WarpObjectOut(2f);
                        blackHole.singularityController.OnCollapse += OnPlanetToSpaceBlackHoleDestroyed;
                        loopingAudio.FadeOut(1f);

                    }
                    // Update parenting and move Ernesto to new planet
                    else
                    {
                        movementPaused = true;
                        ErnestoChase.WriteDebugMessage("Teleported into black hole");
                        ErnestoChase.WriteDebugMessage("Last body: " + transform.parent);

                        blackHole.transform.parent = transform.parent;
                        blackHole.transform.localPosition = transform.localPosition;
                        blackHole.WarpObjectOut(2f);
                        blackHole.singularityController.OnCollapse += OnPlanetToPlanetBlackHoleDestroyed;
                        loopingAudio.FadeOut(1f);
                    }
                }
                // Regular planetary travel
                else
                {
                    AdvanceTarget();
                }
            }
        }
        // Space travel
        else
        {
            // Run if player entered an atmosphere and Ernesto is still in space
            if (playerOnPlanet && targets.Count > 0 && !reachedAtmoEnterPos && !(spaceTargets.Count > 0 && spaceTargets.Peek().Item2))
            {
                // Move to player's atmosphere entry point
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, targets.Peek().Item1, currentSpaceSpeed / 50f);
                if (Vector3.Distance(transform.localPosition, targets.Peek().Item1) < 5f)
                {
                    reachedAtmoEnterPos = true;
                    AdvanceTarget();
                }
            }
            // Targeting player in space
            else
            {
                // Player teleported in space
                if (spaceTargets.Count > 0 && spaceTargets.Peek().Item2 && !waitingOnTeleport)
                {
                    /*ErnestoChase.WriteDebugMessage("New space target: " 
                        + Vector3.Distance(transform.localPosition, staticTransformParent.TransformPoint(spaceTargets.Peek().Item1)));*/
                    //ErnestoChase.WriteDebugMessage(spaceTargets.Peek().Item1);
                    // Move to teleport origin and teleport
                    /*GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.transform.parent = staticTransformParent;
                    obj.transform.position = staticTransformParent.TransformPoint(spaceTargets.Peek().Item1);
                    obj.transform.localScale = Vector3.one * 100f;*/

                    transform.localPosition = Vector3.MoveTowards(transform.localPosition, rigidbody.transform
                        .InverseTransformPoint(staticTransformParent.TransformPoint(spaceTargets.Peek().Item1)), currentSpaceSpeed / 50f);
                    //ErnestoChase.WriteDebugMessage(Vector3.Distance(transform.localPosition, rigidbody.transform.InverseTransformPoint(staticTransformParent.TransformPoint(spaceTargets.Peek().Item1))));
                    if (Vector3.Distance(transform.localPosition, rigidbody.transform.InverseTransformPoint(staticTransformParent.TransformPoint(spaceTargets.Peek().Item1))) < 5f)
                    {
                        movementPaused = true;
                        spaceTargets.Dequeue();

                        ErnestoChase.WriteDebugMessage("Teleported to next target from space");
                        if (teleportPlanets[0] != null)
                        {
                            ErnestoChase.WriteDebugMessage("Last body: " + transform.parent);

                            blackHole.transform.parent = transform.parent;
                            blackHole.transform.localPosition = transform.localPosition;
                            blackHole.WarpObjectOut(2f);
                            blackHole.singularityController.OnCollapse += OnSpaceToPlanetBlackHoleDestroyed;
                            loopingAudio.FadeOut(1f);
                        }
                        else
                        {
                            blackHole.transform.parent = transform.parent;
                            blackHole.transform.localPosition = transform.localPosition;
                            blackHole.WarpObjectOut(2f);
                            blackHole.singularityController.OnCollapse += OnSpaceToSpaceBlackHoleDestroyed;
                            loopingAudio.FadeOut(1f);
                        }
                    }
                }
                // Regular space travel
                else
                {
                    transform.LookAt(Locator.GetPlayerTransform());
                    rigidbody.SetVelocity(rigidbody.transform.TransformDirection(transform.forward * currentSpaceSpeed) + startVelocity);
                }
            }

            //ErnestoChase.WriteDebugMessage(rigidbody.GetVelocity());
            // Increase space travel speed to outrun player
            currentSpaceSpeed += Time.deltaTime * 5f * (Mathf.InverseLerp(1, 10, ErnestoChase.Instance.SpaceSpeed) + 0.5f);
        }
    }

    private void UpdateMovementSpeed(bool playerOnPlanet)
    {
        bool inBramble = PlayerState.InBrambleDimension();
        if (inBramble != lastPlayerBrambleState)
        {
            lastPlayerBrambleState = inBramble;
            (Vector3, bool)[] targetsArray = [.. (playerOnPlanet ? targets : spaceTargets)];
            if (targetsArray.Length > 0)
            {
                if (inBramble)
                {
                    ErnestoChase.WriteDebugMessage("Added speed up marker");
                    brambleSpeedMarkers.Add(targetsArray[targetsArray.Length - 1].Item1, (true, false));
                }
                else
                {
                    ErnestoChase.WriteDebugMessage("Added slow down marker");
                    brambleSpeedMarkers.Add(targetsArray[targetsArray.Length - 1].Item1, (false, true));
                }
            }
        }

        bool inDreamWorld = PlayerState.InDreamWorld();
        if (inDreamWorld != lastPlayerDreamState)
        {
            lastPlayerDreamState = inDreamWorld;
            (Vector3, bool)[] targetsArray = [.. (playerOnPlanet ? targets : spaceTargets)];
            if (targetsArray.Length > 0)
            {
                if (inDreamWorld)
                {
                    dreamWorldSpeedMarkers.Add(targetsArray[targetsArray.Length - 1].Item1, (true, false));
                }
                else
                {
                    dreamWorldSpeedMarkers.Add(targetsArray[targetsArray.Length - 1].Item1, (false, true));
                }
            }
        }

        Vector3 targetPos;

        if (playerOnPlanet && targets.Count > 0)
        {
            targetPos = targets.Peek().Item1;
        }
        else if (!playerOnPlanet && spaceTargets.Count > 0)
        {
            targetPos = spaceTargets.Peek().Item1;
        }
        else
        {
            return;
        }

        if (brambleSpeedMarkers.ContainsKey(targetPos))
        {
            if (brambleSpeedMarkers[targetPos].Item1)
            {
                ErnestoChase.WriteDebugMessage("Reached speed up marker!!!!");
                currentSpeed *= ErnestoChase.Instance.BrambleSpeedMultiplier;
            }
            else if (brambleSpeedMarkers[targetPos].Item2)
            {
                ErnestoChase.WriteDebugMessage("Reached slow down marker!!!!");
                currentSpeed /= ErnestoChase.Instance.BrambleSpeedMultiplier;
            }
            brambleSpeedMarkers.Remove(targetPos);
        }

        if (dreamWorldSpeedMarkers.ContainsKey(targetPos))
        {
            if (dreamWorldSpeedMarkers[targetPos].Item1)
            {
                currentSpeed *= ErnestoChase.Instance.DreamWorldSpeedMultiplier;
            }
            else if (dreamWorldSpeedMarkers[targetPos].Item2)
            {
                currentSpeed /= ErnestoChase.Instance.DreamWorldSpeedMultiplier;
            }
            dreamWorldSpeedMarkers.Remove(targetPos);
        }
    }

    private OWRigidbody GetCurrentPlanetBody()
    {
        OWRigidbody body = null;

        if (PlayerState.InBrambleDimension())
        {
            return staticTransformParent.GetComponent<OWRigidbody>();
        }
        
        AlignmentForceDetector detector = Locator.GetPlayerForceDetector();
        if (detector._trackedLayers.Count > 0)
        {
            foreach (int num in detector._trackedLayers.Keys)
            {
                if (detector._trackedLayers[num].volumes.Count == 0) continue;

                foreach (PriorityVolume priorityVolume in detector._trackedLayers[num].volumes)
                {
                    ForceVolume volume = priorityVolume is ForceVolume forceVolume ? forceVolume : null;
                    if (volume && volume.GetAffectsAlignment(Locator.GetPlayerBody()))
                    {
                        OWRigidbody[] parentBodies = volume.GetComponentsInParent<OWRigidbody>();
                        foreach (OWRigidbody parentBody in parentBodies)
                        {
                            if (parentBody.IsKinematic())
                            {
                                body = parentBody;
                                currentGravity = volume;
                                break;
                            }
                        }
                    }
                }
            }
        }

        return body;
    }

    // Move onto the next planetary target
    private void AdvanceTarget()
    {
        lastPosition = transform.localPosition;
        lastRotation = transform.rotation;
        lastTime = Time.time;
        targets.Dequeue();
    }
    
    // Spawn target on a planet
    private void SpawnTarget(Transform parent, Vector3 worldPosition, bool teleport)
    {
        targets.Enqueue((parent.InverseTransformPoint(worldPosition), teleport));
    }

    // Spawn target in space
    // Should only be for teleporting
    private void SpawnSpaceTarget(Vector3 localPosition, bool teleport)
    {
        spaceTargets.Enqueue((localPosition, teleport));
    }

    public void OnWarpPlayer()
    {
        ErnestoChase.WriteDebugMessage("Run pre-tp code");
        waitingOnTeleport = true;

        bool playerOnPlanet = GetCurrentPlanetBody();

        Transform positionParent = playerOnPlanet ? (teleportPlanets.Count > 0
            ? teleportPlanets[teleportPlanets.Count - 1].transform : currentPlanet.transform) : staticTransformParent;

        // Spawn the teleport origin on a planet or in space
        //OWRigidbody body = GetPlayerAtmosphereBody();
        if (lastPlayerAtmosphereState)
        {
            Transform parent = teleportPlanets.Count > 0 ? teleportPlanets[teleportPlanets.Count - 1].transform : currentPlanet.transform;
            ErnestoChase.WriteDebugMessage("Teleport origin parent: " + parent.name);
            SpawnTarget(parent, positionParent.TransformPoint(lastPlayerPos), true);
        }
        else
        {
            ErnestoChase.WriteDebugMessage("Space teleport origin spawned");
            SpawnSpaceTarget(staticTransformParent.InverseTransformPoint(positionParent.TransformPoint(lastPlayerPos)), true);
        }

        // Delay this code to give time for the detector to update fluid volumes
        ErnestoChase.Instance.ModHelper.Events.Unity.FireInNUpdates(() =>
        {
            waitingOnTeleport = false;
            OWRigidbody body = GetCurrentPlanetBody();
            if (body)
            {
                // Player teleported to a body
                // Use teleportPlanets to give time for Ernesto to finish previous pathing
                ErnestoChase.WriteDebugMessage("Next body: " + body.name);
                teleportPlanets.Add(body.gameObject);
                SpawnTarget(body.transform, Locator.GetPlayerTransform().position, false);
            }
            else
            {
                // Player teleported into space or into ship in space
                teleportedIntoSpace = true;
                teleportPlanets.Add(null);
                teleportVelocities.Enqueue(Locator.GetPlayerBody().GetVelocity());
                ErnestoChase.WriteDebugMessage("Next body: Space");
                SpawnSpaceTarget(staticTransformParent.InverseTransformPoint(Locator.GetPlayerTransform().position), false);
            }
        }, 10);
    }

    private IEnumerator ErnestoReleaseDelay()
    {
        yield return new WaitForSeconds(ErnestoChase.Instance.StartDelay);
        OWRigidbody body = GetCurrentPlanetBody();
        if (targets.Count == 0 && body)
        {
            spawnDelayTimer = targetSpawnDelay;
            Transform parent = teleportPlanets.Count > 0 ? teleportPlanets[teleportPlanets.Count - 1].transform : currentPlanet.transform;
            ErnestoChase.WriteDebugMessage("Spawned on " + parent.name);
            SpawnTarget(parent, Locator.GetPlayerTransform().position, false);
        }
        whiteHole.transform.parent = transform.parent;
        whiteHole.transform.localPosition = transform.localPosition;
        whiteHole.WarpObjectIn(2f);
        whiteHole.singularityController.OnCreation += OnWhiteHoleCreated;
    }

    private void OnPlanetToPlanetBlackHoleDestroyed()
    {
        blackHole.singularityController.OnCollapse -= OnPlanetToPlanetBlackHoleDestroyed;

        transform.parent = teleportPlanets[0].transform;
        currentPlanet = teleportPlanets[0];
        teleportPlanets.RemoveAt(0);
        ErnestoChase.WriteDebugMessage("New body: " + transform.parent);
        AdvanceTarget();
        ErnestoChase.WriteDebugMessage("Teleport destination: " + targets.Peek().Item1);
        transform.localPosition = targets.Peek().Item1;
        AdvanceTarget();

        whiteHole.transform.parent = transform.parent;
        whiteHole.transform.localPosition = transform.localPosition;
        whiteHole.WarpObjectIn(2f);
        whiteHole.singularityController.OnCreation += OnWhiteHoleCreated;
        loopingAudio.FadeIn(1f);
    }

    private void OnSpaceToPlanetBlackHoleDestroyed()
    {
        blackHole.singularityController.OnCollapse -= OnSpaceToPlanetBlackHoleDestroyed;

        spaceTargets.Clear();
        rigidbody.SetVelocity(Vector3.zero);
        transform.parent = teleportPlanets[0].transform;
        currentPlanet = teleportPlanets[0];
        teleportPlanets.RemoveAt(0);
        reachedAtmoEnterPos = true;
        ErnestoChase.WriteDebugMessage("New body: " + transform.parent);
        ErnestoChase.WriteDebugMessage("Start pos: " + targets.Peek().Item1);
        transform.localPosition = targets.Peek().Item1;
        AdvanceTarget();
        ErnestoChase.WriteDebugMessage("Next pos: " + targets.Peek().Item1);

        whiteHole.transform.parent = transform.parent;
        whiteHole.transform.localPosition = transform.localPosition;
        whiteHole.WarpObjectIn(2f);
        whiteHole.singularityController.OnCreation += OnWhiteHoleCreated;
        loopingAudio.FadeIn(1f);
    }

    private void OnPlanetToSpaceBlackHoleDestroyed()
    {
        blackHole.singularityController.OnCollapse -= OnPlanetToSpaceBlackHoleDestroyed;

        targets.Clear();
        ErnestoChase.WriteDebugMessage("teleport planets size pre: " + teleportPlanets.Count);
        teleportPlanets.RemoveAt(0);
        transform.position = staticTransformParent.TransformPoint(spaceTargets.Peek().Item1);
        spaceTargets.Dequeue();
        ErnestoChase.WriteDebugMessage(spaceTargets.Count);
        currentSpaceSpeed = baseSpaceSpeed;
        reachedAtmoEnterPos = false;
        spawnDelayTimer = 0f;
        rigidbody.SetPosition(transform.position);
        rigidbody.SetVelocity(teleportVelocities.Peek());
        teleportVelocities.Dequeue();
        startVelocity = rigidbody._currentVelocity;
        transform.parent = rigidbody.transform;
        OWRigidbody body = GetCurrentPlanetBody();
        if (body && teleportPlanets.Count == 0)
        {
            currentPlanet = body.gameObject;
        }

        whiteHole.transform.parent = transform.parent;
        whiteHole.transform.localPosition = transform.localPosition;
        whiteHole.WarpObjectIn(2f);
        whiteHole.singularityController.OnCreation += OnWhiteHoleCreated;
        loopingAudio.FadeIn(1f);
    }

    private void OnSpaceToSpaceBlackHoleDestroyed()
    {
        blackHole.singularityController.OnCollapse -= OnSpaceToSpaceBlackHoleDestroyed;

        transform.localPosition = rigidbody.transform.InverseTransformPoint(staticTransformParent.TransformPoint(spaceTargets.Peek().Item1));
        spaceTargets.Dequeue();
        OWRigidbody body = GetCurrentPlanetBody();
        if (body && teleportPlanets.Count == 0)
        {
            currentPlanet = body.gameObject;
        }

        whiteHole.transform.parent = transform.parent;
        whiteHole.transform.localPosition = transform.localPosition;
        whiteHole.WarpObjectIn(2f);
        whiteHole.singularityController.OnCreation += OnWhiteHoleCreated;
        loopingAudio.FadeIn(1f);
    }

    private void OnWhiteHoleCreated()
    {
        whiteHole.singularityController.OnCreation -= OnWhiteHoleCreated;
        if (!ernestoReleased)
        {
            ErnestoChase.WriteDebugMessage("WHITE HOLE SPAWN");
            animator.SetTrigger("Impulse");
            oneShotAudio.PlayOneShot(AudioType.DBAnglerfishDetectTarget, 0.8f);
            loopingAudio.AssignAudioLibraryClip(AudioType.DBAnglerfishChasing_LP);
            loopingAudio.FadeIn(1f);
            ernestoReleased = true;
        }
        else
        {
            ErnestoChase.WriteDebugMessage("Teleported out of white hole");
            movementPaused = false;
        }
    }

    private void OnEntry(GameObject hitObj)
    {
        if (hitObj.CompareTag("PlayerDetector")/* && hasCheckedDetector && ernestoReleased*/)
        {
            ErnestoChase.WriteDebugMessage("HAHA I GOT YOU");
            //ErnestoChase.Instance.caughtPlayer = true;
            playerCollided = true;
        }
    }

    private void OnExit(GameObject hitObj)
    {
        if (hitObj.CompareTag("PlayerDetector"))
        {
            playerCollided = false;
        }
    }

    private void OnDestroy()
    {
        killVolume.OnEntry -= OnEntry;
        killVolume.OnExit += OnExit;
    }
}
