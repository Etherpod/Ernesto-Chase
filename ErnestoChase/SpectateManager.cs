using System.Collections;
using System.Collections.Generic;
using CSharpFunctionalExtensions;
using OWML.Common;
using OWML.ModHelper;
using UnityEngine;
using static ErnestoChase.ErnestoChase;

namespace ErnestoChase;

public class SpectateManager : MonoBehaviour
{
    private bool spectating = false;
    private bool spectatingErnesto = true;
    public List<SpectatorCamera> ernestoSpectatorCams = [];
    public List<SpectatorCamera> playerSpectatorCams = [];
    private int ernestoCamIndex = 0;
    private int playerCamIndex = 0;
    public SpectatorCamera SpectateTarget { get; private set; }
    public bool IsSpectating { get => spectating; }
    private bool lastSpectateTargetState;
    
    public bool loadedRingWorld;
    public bool loadedDreamWorld;
    private readonly Dictionary<uint, bool> playerRingWorldStates = [];
    private readonly Dictionary<uint, Dictionary<uint, bool>> ernestoRingWorldStates = [];
    private bool wasInsideRingWorld;
    
    private ScreenPrompt _changeSpectateTargetPrompt;
    private ScreenPrompt _changeSpectateTypePrompt;
    private ScreenPrompt _enterSpectateModePrompt;
    private ScreenPrompt _exitSpectateModePrompt;

    private void Awake()
    {
        _changeSpectateTargetPrompt = new(InputLibrary.toolOptionLeft, InputLibrary.toolOptionRight, 
            "Switch Spectate Target" + " <CMD1> <CMD2>", ScreenPrompt.MultiCommandType.CUSTOM_BOTH);
        _changeSpectateTypePrompt = new(InputLibrary.toolOptionUp, InputLibrary.toolOptionDown,
            "Switch Spectate Type" + " <CMD1> <CMD2>", ScreenPrompt.MultiCommandType.CUSTOM_BOTH);
        _enterSpectateModePrompt = new(InputLibrary.map, "Enter Spectator Mode");
        _exitSpectateModePrompt = new(InputLibrary.map, "Exit Spectator Mode");
        
        GlobalMessenger<DeathType>.AddListener("PlayerDeath", OnPlayerDeath);
        Locator.GetCloakFieldController()?.OnPlayerEnter += OnPlayerTriggerCloak;
        Locator.GetCloakFieldController()?.OnPlayerExit += OnPlayerTriggerCloak;
        GlobalMessenger.AddListener("EnterDreamWorld", OnPlayerTriggerDreamWorld);
        GlobalMessenger.AddListener("ExitDreamWorld", OnPlayerTriggerDreamWorld);
    }
    
    private void Start()
    {
        Locator.GetPromptManager().AddScreenPrompt(_changeSpectateTargetPrompt, PromptPosition.UpperRight);
        Locator.GetPromptManager().AddScreenPrompt(_changeSpectateTypePrompt, PromptPosition.UpperRight);
        Locator.GetPromptManager().AddScreenPrompt(_enterSpectateModePrompt, PromptPosition.BottomCenter);
        Locator.GetPromptManager().AddScreenPrompt(_exitSpectateModePrompt, PromptPosition.UpperRight);
                    
        foreach (uint id in Players)
        {
            StartCoroutine(AddCamToRemotePlayer(id));
        }
    }
    
	private void Update()
    {
        if (EntitlementsManager.IsDlcOwned() != EntitlementsManager.AsyncOwnershipStatus.NotOwned)
        {
            bool insideRingWorld = Locator.GetRingWorldController()?._playerInsideRingWorld ?? false;
            if (insideRingWorld != wasInsideRingWorld)
            {
                wasInsideRingWorld = insideRingWorld;
                foreach (var id in Players)
                {
                    QSBCompat.SendRingWorldUpdate(id, wasInsideRingWorld);
                }
            }
        }
        
        if (Players.Length > 0 && QSBAPI.GetPlayerDead(QSBAPI.GetLocalPlayerID()))
        {
            _exitSpectateModePrompt.SetVisibility(spectating);
            _enterSpectateModePrompt.SetVisibility(!spectating);

            if (OWInput.IsNewlyPressed(InputLibrary.map))
            {
                if (!spectating)
                {
                    spectating = true;
                    
                    SpectatorCamera targetCam = GetCurrentSpectatorCamera();
                    if (!targetCam)
                    {
                        spectating = false;
                        return;
                    }
                    
                    Locator.GetMapController().ExitMapView();

                    var mixer = Locator.GetAudioMixer();
                    mixer._deathMixed = false;
                    mixer._nonEndTimesVolume.FadeTo(1, 0.5f);
                    mixer._endTimesVolume.FadeTo(1, 0.5f);
                    mixer.UnmixMap();

                    ReticleController.Hide();
                    
                    SwitchToSpectatorCam(targetCam);

                    _changeSpectateTargetPrompt.SetVisibility(true);
                    _changeSpectateTypePrompt.SetVisibility(true);

                    spectating = true;
                }
                else
                {
                    spectating = false;
                    ReticleController.Show();
                    Locator.GetPlayerCameraController()._audioListener.enabled = true;
                    Locator.GetMapController().EnterMapView(SpectateTarget.transform);
                }
            }

            if (spectating)
            {
                if (!SpectateTarget || SpectateTarget.CanSpectate() != lastSpectateTargetState)
                {
                    RefreshSpectateTarget();
                    return;
                }
                
                if (OWInput.IsNewlyPressed(InputLibrary.toolOptionUp) || OWInput.IsNewlyPressed(InputLibrary.toolOptionDown))
                {
                    spectatingErnesto = !spectatingErnesto;
                    
                    SpectatorCamera targetCam = GetCurrentSpectatorCamera();
                    if (!targetCam) return;
                    
                    SwitchToSpectatorCam(targetCam);
                }

                bool leftPressed = OWInput.IsNewlyPressed(InputLibrary.toolOptionLeft);
                if (leftPressed || OWInput.IsNewlyPressed(InputLibrary.toolOptionRight))
                {
                    if (spectatingErnesto)
                    {
                        var newIndex = GetSpectatorCamIndex(ernestoSpectatorCams, ernestoCamIndex, leftPressed);
                        if (newIndex.HasValue && newIndex.Value != ernestoCamIndex)
                        {
                            //ErnestoChase.WriteDebugMessage("Switch to cam " + newIndex);
                            ernestoCamIndex = newIndex.Value;
                            SwitchToSpectatorCam(ernestoSpectatorCams[ernestoCamIndex]);
                        }
                    }
                    else
                    {
                        var newIndex = GetSpectatorCamIndex(playerSpectatorCams, playerCamIndex, leftPressed);
                        if (newIndex.HasValue)
                        {
                            playerCamIndex = newIndex.Value;
                            SwitchToSpectatorCam(playerSpectatorCams[playerCamIndex]);
                        }
                    }
                }
            }
        }
    }

    private Maybe<int> GetSpectatorCamIndex(List<SpectatorCamera> cams, int currentIndex, bool cycleLeft)
    {
        ErnestoChase.WriteDebugMessage("Cycle left: " + cycleLeft);
        ErnestoChase.WriteDebugMessage("Start index: " + currentIndex);
        int delta = cycleLeft ? -1 : 1;

        for (int i = 0; i <= cams.Count; i++)
        {
            currentIndex = (currentIndex + delta + cams.Count) % cams.Count;
            ErnestoChase.WriteDebugMessage("Check index " + currentIndex);
            if (cams[currentIndex].CanSpectate())
            {
                ErnestoChase.WriteDebugMessage(currentIndex + " can spectate!");
                return currentIndex;
            }
        }

        ErnestoChase.WriteDebugMessage("No Ernestos found");
        return Maybe.None;
    }

    private Maybe<int> GetSpectatorCamIndex(List<SpectatorCamera> cams, int currentIndex)
    {
        for (int i = 0; i <= cams.Count; i++)
        {
            currentIndex = (currentIndex - 1 + cams.Count) % cams.Count;
            ErnestoChase.WriteDebugMessage("Check index " + currentIndex);
            if (cams[currentIndex].CanSpectate())
            {
                ErnestoChase.WriteDebugMessage(currentIndex + " can spectate!");
                return currentIndex;
            }
        }

        ErnestoChase.WriteDebugMessage("No Ernestos found");
        return Maybe.None;
    }

    private SpectatorCamera GetCurrentSpectatorCamera()
    {
        if (!spectating) return null;
        
        SpectatorCamera targetCam = null;
                    
        if (spectatingErnesto && ernestoSpectatorCams.Count > 0)
        {
            var index = GetSpectatorCamIndex(ernestoSpectatorCams, ernestoCamIndex);
            if (index.HasValue)
            {
                targetCam = ernestoSpectatorCams[index.Value];
            }
        }
                    
        if (!targetCam && playerSpectatorCams.Count > 0)
        {
            spectatingErnesto = false;
            var index = GetSpectatorCamIndex(playerSpectatorCams, playerCamIndex);
            if (index.HasValue)
            {
                targetCam = playerSpectatorCams[index.Value];
            }
        }

        return targetCam;
    }

    public void RefreshSpectateTarget()
    {
        SpectatorCamera targetCam = GetCurrentSpectatorCamera();
        if (!targetCam || targetCam == SpectateTarget) return;
                    
        SwitchToSpectatorCam(targetCam);
    }
    
    public void SwitchToSpectatorCam(SpectatorCamera camera)
    {
        if (!camera.CanSpectate())
        {
            Instance.ModHelper.Console.WriteLine("Tried to switch to a target that can't be spectated!", MessageType.Error);
            return;
        }
        
        ErnestoChase.WriteDebugMessage("Switching spectator camera");
        Locator.GetPlayerCamera().enabled = false;
        Locator.GetPlayerCameraController()._audioListener.enabled = false;

        if (SpectateTarget != null)
        {
            SpectateTarget.Camera.enabled = false;
            SpectateTarget.AudioListener.enabled = false;
            SpectateTarget.Detector.gameObject.SetActive(false);
        }
        
        GlobalMessenger<OWCamera>.FireEvent("SwitchActiveCamera", camera.Camera);

        SpectateTarget = camera;
        lastSpectateTargetState = true;
        camera.Camera.enabled = true;
        camera.AudioListener.enabled = true;
        camera.Detector.gameObject.SetActive(true);

        if (camera.IsErnestoCam)
        {
            var remoteID = camera.GetComponent<ErnestoState>().RemoteID;
            var localID = camera.GetComponent<ErnestoState>().LocalID;

            ernestoRingWorldStates.TryAdd(remoteID, []);
            ernestoRingWorldStates[remoteID].TryAdd(localID, false);
            
            RefreshDreamWorld(remoteID);
            RefreshRingWorld(remoteID, localID, ernestoRingWorldStates[remoteID][localID], false);
        }
        else
        {
            playerRingWorldStates.TryAdd(camera.PlayerID, false);
            
            RefreshDreamWorld(camera.PlayerID);
            RefreshRingWorld(camera.PlayerID, playerRingWorldStates[camera.PlayerID]);
        }
    }
    
    public void RefreshRingWorld(uint remoteID, bool inside)
    {
        RefreshRingWorld(remoteID, 0, inside, true);
    }

    public void RefreshRingWorld(uint remoteID, uint localID, bool inside, bool isPlayer)
    {
        if (!spectating || isPlayer == SpectateTarget.IsErnestoCam || 
            EntitlementsManager.IsDlcOwned() == EntitlementsManager.AsyncOwnershipStatus.NotOwned) return;

        if (SpectateTarget.IsErnestoCam)
        {
            var state = SpectateTarget.GetComponent<ErnestoState>();
            if (state.RemoteID != remoteID || state.LocalID != localID)
            {
                return;
            }
        }
        else
        {
            if (SpectateTarget.PlayerID != remoteID) return;
        }
        
        bool unload = false;
        if (SpectateTarget.IsErnestoCam &&
            SpectateTarget.transform.parent.gameObject != Locator.GetRingWorldController().gameObject)
        {
            unload = true;
        }
        else if (!SpectateTarget.IsErnestoCam && remoteID > 0 && !QSBInteraction.GetPlayerInCloak(remoteID))
        {
            unload = true;
        }

        if (unload)
        {
            ErnestoChase.WriteDebugMessage("unload ring world");
            if (loadedRingWorld)
            {
                foreach (var proxy in FindObjectsOfType<CloakingFieldProxy>())
                {
                    proxy.OnPlayerExitCloakingField();
                }
                
                Locator.GetRingWorldController().transform
                    .Find("Sector_RingInterior").GetComponent<Sector>().RemoveOccupant(SpectateTarget.Detector);

                loadedRingWorld = false;
            }

            return;
        }
        
        ErnestoChase.WriteDebugMessage("load ring world");
        
        if (!loadedRingWorld)
        {
            foreach (var proxy in FindObjectsOfType<CloakingFieldProxy>())
            {
                proxy.OnPlayerEnterCloakingField();
            }

            loadedRingWorld = true;
        }
        
        if (inside)
        {
            ErnestoChase.WriteDebugMessage("UPDATE INTERIOR");
            Locator.GetRingWorldController().transform
                .Find("Sector_RingInterior").GetComponent<Sector>().AddOccupant(SpectateTarget.Detector);
        }
        else
        {
            Locator.GetRingWorldController().transform
                .Find("Sector_RingInterior").GetComponent<Sector>().RemoveOccupant(SpectateTarget.Detector);
        }
    }

    public void UpdateRingWorldState(uint remoteID, bool state)
    {
        playerRingWorldStates[remoteID] = state;
        RefreshRingWorld(remoteID, state);
    }

    public void UpdateRingWorldState(uint remoteID, uint localID, bool state)
    {
        ErnestoChase.WriteDebugMessage("\nupdating state: " + state);
        ernestoRingWorldStates[remoteID][localID] = state;
        RefreshRingWorld(remoteID, localID, state, false);
    }
    
    public void RefreshDreamWorld(uint remoteID)
    {
        if (!spectating || EntitlementsManager.IsDlcOwned() == EntitlementsManager.AsyncOwnershipStatus.NotOwned) return;
        
        bool unload = false;
        if (SpectateTarget.IsErnestoCam &&
            SpectateTarget.transform.parent.gameObject != Locator.GetDreamWorldController().gameObject)
        {
            unload = true;
        }
        else if (!SpectateTarget.IsErnestoCam && remoteID > 0 && !QSBInteraction.GetPlayerInDream(remoteID))
        {
            unload = true;
        }
        
        if (unload)
        {
            ErnestoChase.WriteDebugMessage("Unload DW");
            if (loadedDreamWorld)
            {
                UnloadDreamWorld();
            }

            return;
        }

        ErnestoChase.WriteDebugMessage("LOAD DW HAHHA");
        
        if (!loadedDreamWorld)
        {
            LoadDreamWorld();
        }
    }

    private void LoadDreamWorld()
    {
        ErnestoChase.WriteDebugMessage("gabagool");
        
        var dw = Locator.GetDreamWorldController();
        
        SpectateTarget.Camera.cullingMask &= ~(1 << LayerMask.NameToLayer("Sun"));
        dw._prevPlayerCameraFarPlaneDist = SpectateTarget.Camera.farClipPlane;
        SpectateTarget.Camera.farClipPlane = 4000f;
        SpectateTarget.Camera.mainCamera.backgroundColor = dw._tempSkyboxColor;
        SpectateTarget.Camera.planetaryFog.enabled = false;
        SpectateTarget.Camera.postProcessingSettings.ambientOcclusionAvailable = false;
        SpectateTarget.Camera.postProcessingSettings.screenSpaceReflectionAvailable = true;

        var rend = SpectateTarget.Camera.GetComponent<HeightmapAmbientLightRenderer>();
        if (rend != null)
        {
            rend.enabled = true;
        }
        
        dw._dreamWorldSector.GetTriggerVolume().AddObjectToVolume(SpectateTarget.Detector.gameObject);
        SunLightController.RegisterSunOverrider(dw, 1000);
        if (dw._proxyShadowLight)
        {
            dw._proxyShadowLight.enabled = false;
        }
        
        Locator.GetAudioMixer().MixDreamWorld();
        foreach (var proxy in FindObjectsOfType<CloakingFieldProxy>())
        {
            proxy.OnEnterDreamWorld();
        }
        
        loadedDreamWorld = true;
    }

    private void UnloadDreamWorld()
    {
        var dw = Locator.GetDreamWorldController();
        
        SunLightController.UnregisterSunOverrider(dw);
        if (dw._proxyShadowLight)
        {
            dw._proxyShadowLight.enabled = true;
        }

        int count = SpectateTarget.Detector._sectorList.Count;
        for (int i = 0; i < count; i++)
        {
            SpectateTarget.Detector._sectorList[0].GetTriggerVolume()
                .RemoveObjectFromVolume(SpectateTarget.Detector.gameObject);
        }
        
        Locator.GetAudioMixer().UnmixDreamWorld();

        var rend = SpectateTarget.Camera.GetComponent<HeightmapAmbientLightRenderer>();
        if (rend != null)
        {
            rend.enabled = false;
        }
        
        SpectateTarget.Camera.cullingMask |= 1 << LayerMask.NameToLayer("Sun");
        SpectateTarget.Camera.farClipPlane = dw._prevPlayerCameraFarPlaneDist;
        dw._prevPlayerCameraFarPlaneDist = 0f;
        SpectateTarget.Camera.mainCamera.backgroundColor = Color.black;
        SpectateTarget.Camera.planetaryFog.enabled = true;
        SpectateTarget.Camera.postProcessingSettings.screenSpaceReflectionAvailable = false;
        SpectateTarget.Camera.postProcessingSettings.ambientOcclusionAvailable = true;
        
        foreach (var proxy in FindObjectsOfType<CloakingFieldProxy>())
        {
            proxy.OnExitDreamWorld();
        }
        
        loadedDreamWorld = false;
    }
    
    public IEnumerator AddCamToRemotePlayer(uint playerID)
    {
        yield return new WaitUntil(() => QSBAPI.GetPlayerReady(playerID));

        if (playerID == QSBAPI.GetLocalPlayerID()) yield break;

        ErnestoChase.WriteDebugMessage("add cam to " + playerID);
        GameObject body = QSBAPI.GetPlayerBody(playerID);
        ErnestoChase.WriteDebugMessage("body: " + body);
        GameObject remoteCam = LoadPrefab("Assets/ErnestoChase/PlayerRemoteSpectatorCam.prefab");
        GameObject remoteCamObj = Instantiate(remoteCam, body.transform.Find("REMOTE_PlayerCamera"));
        var cam = remoteCamObj.GetComponent<SpectatorCamera>();
        cam.SetPlayerID(playerID);
        playerSpectatorCams.Add(cam);
    }
    
    private void OnPlayerDeath(DeathType deathType)
    {
        if (spectating)
        {
            spectating = false;
            SpectateTarget?.Camera.enabled = false;
            Locator.GetPlayerCamera().enabled = true;
            GlobalMessenger<OWCamera>.FireEvent("SwitchActiveCamera", Locator.GetPlayerCamera());
        }
    }
    
    private void OnPlayerTriggerCloak()
    {
        foreach (var id in Players)
        {
            QSBCompat.SendRingWorldRefresh(id);
        }
    }

    private void OnPlayerTriggerDreamWorld()
    {
        foreach (var id in Players)
        {
            QSBCompat.SendDreamWorldRefresh(id);
        }
    }
    
    private void OnDestroy()
    {
        Locator.GetPromptManager().RemoveScreenPrompt(_changeSpectateTargetPrompt);
        Locator.GetPromptManager().RemoveScreenPrompt(_changeSpectateTypePrompt);
        Locator.GetPromptManager().RemoveScreenPrompt(_enterSpectateModePrompt);
        Locator.GetPromptManager().RemoveScreenPrompt(_exitSpectateModePrompt);
        
        GlobalMessenger<DeathType>.RemoveListener("PlayerDeath", OnPlayerDeath);
        Locator.GetCloakFieldController()?.OnPlayerEnter -= OnPlayerTriggerCloak;
        Locator.GetCloakFieldController()?.OnPlayerExit -= OnPlayerTriggerCloak;
        GlobalMessenger.RemoveListener("EnterDreamWorld", OnPlayerTriggerDreamWorld);
        GlobalMessenger.RemoveListener("ExitDreamWorld", OnPlayerTriggerDreamWorld);
    }
}