using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ErnestoChase;

public class ErnestoManager : MonoBehaviour
{
    [SerializeField]
    private OWTriggerVolume killVolume;

    private ErnestoState state;
    private PlanetManager planetManager;
    private ErnestoMovement ernestoMovement;
    private ErnestoEffects ernestoEffects;

    private Vector3 lastPlayerPos;

    private bool initialized = false;
    private bool startedDeathSequence = false;
    private bool playerCollided = false;
    private bool colliderEnabled = false;

    private void Awake()
    {
        state = GetComponent<ErnestoState>();
        planetManager = GetComponent<PlanetManager>();
        ernestoMovement = GetComponent<ErnestoMovement>();
        ernestoEffects = GetComponent<ErnestoEffects>();

        killVolume.OnEntry += OnEntry;
        killVolume.OnExit += OnExit;

        planetManager.OnUpdateTravelMode += OnUpdateTravelMode;
        planetManager.OnTeleportStarted += OnTeleportStarted;

        ernestoMovement.OnTeleportRequired += planetManager.OnTeleportRequired;
        ernestoMovement.OnProximityRoar += ernestoEffects.OnProximityRoar;

        ernestoEffects.OnExitWhiteHole += OnExitWhiteHole;
        ernestoEffects.OnEnterBlackHole += OnEnterBlackHole;
    }

    private void FixedUpdate()
    {
        if (LoadManager.GetCurrentScene() != OWScene.SolarSystem || !ErnestoChase.Instance.playerDetectorReady || !TimeLoop.IsTimeFlowing()) return;

        if (!initialized)
        {
            planetManager.Initialize();
            ernestoMovement.Initialize();
            StartCoroutine(ErnestoReleaseDelay());
            initialized = true;
        }

        if (colliderEnabled && playerCollided && !ErnestoChase.Instance.caughtPlayer)
        {
            ErnestoChase.Instance.caughtPlayer = true;
        }

        if (ErnestoChase.Instance.caughtPlayer)
        {
            if (!startedDeathSequence)
            {
                transform.parent = null;
                transform.position = Locator.GetPlayerTransform().position;
                planetManager.OnCaughtPlayer();
                ernestoEffects.OnCaughtPlayer();
                Locator.GetDeathManager().KillPlayer(DeathType.Digestion);
                startedDeathSequence = true;
            }
            return;
        }

        if (state.ErnestoReleased)
        {
            if (planetManager.UpdatePlayerPlanetState(!ernestoMovement.IsNextSpaceTargetTeleport()))
            {
                lastPlayerPos = planetManager.GetPlayerParent().InverseTransformPoint(lastPlayerPos);
            }
            ernestoMovement.UpdateMovement(planetManager.IsOnPlanet());
        }

        lastPlayerPos = planetManager.GetPlayerParent().InverseTransformPoint(Locator.GetPlayerTransform().position);
    }

    public void OnUpdateTravelMode(bool isSpace)
    {
        ernestoMovement.SetTravelMode(isSpace);
        ernestoEffects.SetTravelMode(isSpace);
    }

    public void OnTeleportStarted(bool fromSpace, bool toSpace)
    {
        ernestoMovement.SetMovementEnabled(false);
        ernestoEffects.OnTeleportStarted(fromSpace, toSpace);
    }

    public void OnEnterBlackHole(bool fromSpace, bool toSpace)
    {
        ernestoMovement.OnEnterBlackHole(fromSpace, toSpace);
        planetManager.OnEnterBlackHole(fromSpace, toSpace);
    }

    private IEnumerator ErnestoReleaseDelay()
    {
        yield return new WaitForSeconds(ErnestoChase.Instance.StartDelay);
        if (planetManager.IsOnPlanet())
        {
            ernestoMovement.OnErnestoRelease();
        }
        ernestoEffects.CreateWhiteHole();
    }

    private void OnExitWhiteHole()
    {
        if (!state.ErnestoReleased)
        {
            state.ErnestoReleased = true;
            colliderEnabled = true;
        }
        else
        {
            ernestoMovement.SetMovementEnabled(enabled);
        }
    }

    private void OnEntry(GameObject hitObj)
    {
        if (hitObj.CompareTag("PlayerDetector"))
        {
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
}
