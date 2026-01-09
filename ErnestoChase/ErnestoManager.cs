using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ErnestoChase;

public class ErnestoManager : MonoBehaviour
{
    public delegate void CaughtPlayerEvent(ErnestoManager ernesto);
    public event CaughtPlayerEvent OnCaughtPlayer;

    [SerializeField]
    private OWTriggerVolume killVolume;

    private ErnestoState state;
    private PlanetManager planetManager;
    private ErnestoMovement ernestoMovement;
    private ErnestoEffects ernestoEffects;

    private float releaseDelay = 15f;
    private bool initialized = false;
    private bool startedDeathSequence = false;
    private bool playerCollided = false;
    private bool missionComplete = false;

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
        planetManager.OnPlayerWarpStarted += ernestoMovement.OnPlayerWarpStarted;
        planetManager.OnPlayerWarpComplete += ernestoMovement.OnPlayerWarpComplete;

        ernestoMovement.OnTeleportRequired += planetManager.OnTeleportRequired;
        ernestoMovement.OnProximityRoar += ernestoEffects.OnProximityRoar;
        ernestoMovement.OnSpaceWarp += OnSpaceWarp;
        ernestoMovement.OnTakeShortcut += ernestoEffects.OnTakeShortcut;
        ernestoMovement.OnUpdateVisibility += ernestoEffects.OnUpdateVisibility;
        ernestoMovement.OnFinalWarp += OnFinalWarp;

        ernestoEffects.OnExitWhiteHole += OnExitWhiteHole;
        ernestoEffects.OnEnterBlackHole += OnEnterBlackHole;

        ErnestoChase.Instance.OnPlayerWarped += planetManager.OnPlayerWarped;
        GlobalMessenger<DeathType>.AddListener("PlayerDeath", OnPlayerDeath);

        if (ErnestoChase.QSBAPI != null)
        {
            ErnestoChase.QSBAPI.OnPlayerLeave().AddListener(OnPlayerLeave);
        }

        releaseDelay = state.StartDelay;

        if (!state.ErnestoCam)
        {
            GetComponentInChildren<ErnestoCamera>().gameObject.SetActive(false);
        }
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

        if (state.KillVolumeEnabled && playerCollided && !state.CaughtPlayer)
        {
            if (state.RemoteID > 0)
            {
                if (!startedDeathSequence)
                {
                    Locator.GetDeathManager().KillPlayer(DeathType.Digestion);
                    startedDeathSequence = true;
                }
                return;
            }
            state.CaughtPlayer = true;
        }

        if (state.CaughtPlayer)
        {
            if (!startedDeathSequence)
            {
                transform.parent = null;
                transform.position = Locator.GetPlayerTransform().position;
                planetManager.OnCaughtPlayer();
                ernestoEffects.OnCaughtPlayer();
                OnCaughtPlayer?.Invoke(this);
                PatchnestoClass.OnCaughtPlayer(this);
                GlobalMessenger.FireEvent("CaughtPlayer");
                Locator.GetDeathManager().KillPlayer(DeathType.Digestion);
                startedDeathSequence = true;
            }
            return;
        }

        if (state.ErnestoReleased && state.AIEnabled)
        {
            if (planetManager.UpdatePlayerPlanetState(!ernestoMovement.IsNextSpaceTargetTeleport()))
            {
                state.LastPlayerPos = planetManager.GetPlayerParent().InverseTransformPoint(state.LastPlayerPos);
            }
            ernestoMovement.UpdateMovement(planetManager.IsOnPlanet());
        }

        state.LastPlayerPos = planetManager.GetPlayerParent().InverseTransformPoint(Locator.GetPlayerTransform().position);
    }

    public void IncrementSpawnDelay(float index)
    {
        releaseDelay += 2f * index;
        //ernestoMovement.SetSpeedMultiplier(multiplier / 10f + 1f);
    }

    public void SetStoredTargets(TargetDataQueue queue)
    {
        ernestoMovement.SetStoredTargets(queue);
    }

    public TargetDataQueue GetStoredTargets()
    {
        return ernestoMovement.GetStoredTargets();
    }

    public bool CanSpectate()
    {
        if (missionComplete) return false;
        
        if (state.RemoteID > 0)
        {
            return !ErnestoChase.QSBAPI.GetPlayerDead(state.RemoteID);
        }
        else
        {
            return !state.CaughtPlayer;
        }
    }

    private void OnFinalWarp()
    {
        ernestoEffects.OnFinalWarp();
        missionComplete = true;
        // switch cams
    }

    private void OnPlayerDeath(DeathType deathType)
    {
        killVolume.gameObject.SetActive(false);
        if (state.RemoteID == 0)
        {
            ernestoMovement.OnPlayerDeath();
        }
    }

    private void OnPlayerLeave(uint id)
    {
        if (id == state.RemoteID)
        {
            OnPlayerDeathRemote();
        }
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
        // Order is important
        planetManager.OnEnterBlackHole(fromSpace, toSpace);
        ernestoMovement.OnEnterBlackHole(fromSpace, toSpace);
    }

    public void OnPlayerDeathRemote()
    {
        ernestoMovement.OnPlayerDeathRemote();
    }

    private void OnSpaceWarp()
    {
        ernestoEffects.SetTravelMode(true);
    }

    private IEnumerator ErnestoReleaseDelay()
    {
        yield return new WaitForSeconds(releaseDelay);
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
            state.KillVolumeEnabled = true;
        }
        else
        {
            ernestoMovement.SetMovementEnabled(true);
        }
    }

    private void OnEntry(GameObject hitObj)
    {
        if (hitObj.CompareTag("PlayerDetector") 
            && (state.RemoteID == 0 || !ErnestoChase.Instance.ErnestoMorph))
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

    private void OnDestroy()
    {
        killVolume.OnEntry -= OnEntry;
        killVolume.OnExit -= OnExit;

        planetManager.OnUpdateTravelMode -= OnUpdateTravelMode;
        planetManager.OnTeleportStarted -= OnTeleportStarted;
        planetManager.OnPlayerWarpStarted -= ernestoMovement.OnPlayerWarpStarted;
        planetManager.OnPlayerWarpComplete -= ernestoMovement.OnPlayerWarpComplete;

        ernestoMovement.OnTeleportRequired -= planetManager.OnTeleportRequired;
        ernestoMovement.OnProximityRoar -= ernestoEffects.OnProximityRoar;
        ernestoMovement.OnSpaceWarp -= OnSpaceWarp;
        ernestoMovement.OnTakeShortcut -= ernestoEffects.OnTakeShortcut;
        ernestoMovement.OnUpdateVisibility -= ernestoEffects.OnUpdateVisibility;
        ernestoMovement.OnFinalWarp -= OnFinalWarp;

        ernestoEffects.OnExitWhiteHole -= OnExitWhiteHole;
        ernestoEffects.OnEnterBlackHole -= OnEnterBlackHole;

        ErnestoChase.Instance.OnPlayerWarped -= planetManager.OnPlayerWarped;
        GlobalMessenger<DeathType>.RemoveListener("PlayerDeath", OnPlayerDeath);
        
        if (ErnestoChase.QSBAPI != null)
        {
            ErnestoChase.QSBAPI.OnPlayerLeave().RemoveListener(OnPlayerLeave);
        }
    }
}
