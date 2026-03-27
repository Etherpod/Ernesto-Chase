using System.Collections;
using System.Collections.Generic;
using CSharpFunctionalExtensions;
using ErnestoChase.ErnestoAI;
using OWML.Common;
using OWML.ModHelper;
using UnityEngine;
using static ErnestoChase.ErnestoChase;

namespace ErnestoChase.Spectating;

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
    private bool unloadRingWorldNextFrame = false;
    private bool unloadDreamWorldNextFrame = false;
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
    }
    
    private void Start()
    {
        Locator.GetPromptManager().AddScreenPrompt(_changeSpectateTargetPrompt, PromptPosition.UpperRight);
        Locator.GetPromptManager().AddScreenPrompt(_changeSpectateTypePrompt, PromptPosition.UpperRight);
        Locator.GetPromptManager().AddScreenPrompt(_enterSpectateModePrompt, PromptPosition.BottomCenter);
        Locator.GetPromptManager().AddScreenPrompt(_exitSpectateModePrompt, PromptPosition.UpperRight);

        Locator.GetPlayerDetector().gameObject.AddComponent<PlayerSectorTracker>();
        foreach (uint id in Players)
        {
            StartCoroutine(AddCamToRemotePlayer(id));
        }
    }
    
	private void Update()
    {
        if (unloadRingWorldNextFrame)
        {
            UnloadRingWorld_Internal();
            unloadRingWorldNextFrame = false;
        }

        if (unloadDreamWorldNextFrame)
        {
            UnloadDreamWorld_Internal();
            unloadDreamWorldNextFrame = false;
        }
        
        if (Players.Length > 0 && QSBAPI.GetPlayerDead(QSBAPI.GetLocalPlayerID()))
        {
            _exitSpectateModePrompt.SetVisibility(spectating);
            _enterSpectateModePrompt.SetVisibility(!spectating);

            if (OWInput.IsNewlyPressed(InputLibrary.map))
            {
                ErnestoChase.WriteDebugMessage("Press map");
                if (!spectating)
                {
                    ErnestoChase.WriteDebugMessage("\nEnable spectating");
                    spectating = true;
                    
                    SpectatorCamera targetCam = GetCurrentSpectatorCamera();
                    if (!targetCam)
                    {
                        ErnestoChase.WriteDebugMessage("No cameras to spectate!");
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
                }
                else
                {
                    ExitSpectate();
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

    private void ExitSpectate(bool toPlayer = false)
    {
        spectating = false;
        ReticleController.Show();
                    
        if (SpectateTarget != null)
        {
            SpectateTarget.Camera.enabled = false;
            SpectateTarget.AudioListener.enabled = false;

            if (SpectateTarget is ErnestoSpectatorCamera)
            {
                SpectateTarget.Detector.SetOccupantType(DynamicOccupant.Environment);
                SpectateTarget.GetComponent<ErnestoMovement>().ClearSectors();
                SpectateTarget.Detector.gameObject.SetActive(false);
            }
            else if (SpectateTarget is PlayerSpectatorCamera)
            {
                SpectateTarget.Detector.SetOccupantType(DynamicOccupant.Environment);
                var tracker = SpectateTarget.Detector.GetComponent<PlayerSectorTrackerRemote>();
                tracker.ClearSectors();
                tracker.enabled = false;
                //SpectateTarget.Detector.gameObject.SetActive(false);
            }
            else
            {
                SpectateTarget.Detector.gameObject.SetActive(false);
            }
        }
                    
        Locator.GetPlayerCameraController()._audioListener.enabled = true;
        
        if (toPlayer)
        {
            Locator.GetPlayerCamera().enabled = true;
            Locator.GetPlayerCamera().gameObject.tag = "MainCamera";
            GlobalMessenger<OWCamera>.FireEvent("SwitchActiveCamera", Locator.GetPlayerCamera());
        }
        else
        {
            Locator.GetMapController().EnterMapView(SpectateTarget.transform);
        }
                    
        SpectateTarget = null;
    }

    private Maybe<int> GetSpectatorCamIndex(List<SpectatorCamera> cams, int currentIndex, bool cycleLeft)
    {
        //ErnestoChase.WriteDebugMessage("Cycle left: " + cycleLeft);
        //ErnestoChase.WriteDebugMessage("Start index: " + currentIndex);
        int delta = cycleLeft ? -1 : 1;

        for (int i = 0; i <= cams.Count; i++)
        {
            currentIndex = (currentIndex + delta + cams.Count) % cams.Count;
            //ErnestoChase.WriteDebugMessage("Check index " + currentIndex);
            if (cams[currentIndex].CanSpectate())
            {
                //ErnestoChase.WriteDebugMessage(currentIndex + " can spectate!");
                return currentIndex;
            }
        }

        //ErnestoChase.WriteDebugMessage("No Ernestos found");
        return Maybe.None;
    }

    private Maybe<int> GetSpectatorCamIndex(List<SpectatorCamera> cams, int currentIndex)
    {
        for (int i = 0; i <= cams.Count; i++)
        {
            currentIndex = (currentIndex - 1 + cams.Count) % cams.Count;
            //ErnestoChase.WriteDebugMessage("Check index " + currentIndex);
            if (cams[currentIndex].CanSpectate())
            {
                //ErnestoChase.WriteDebugMessage(currentIndex + " can spectate!");
                return currentIndex;
            }
        }

        //ErnestoChase.WriteDebugMessage("No Ernestos found");
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
        //ErnestoChase.WriteDebugMessage("switch to " + camera);
        
        if (!camera.CanSpectate())
        {
            Instance.ModHelper.Console.WriteLine("Tried to switch to a target that can't be spectated!", MessageType.Error);
            return;
        }
        
        //ErnestoChase.WriteDebugMessage("Switching spectator camera");
        Locator.GetPlayerCamera().enabled = false;
        Locator.GetPlayerCamera().gameObject.tag = "Untagged";
        Locator.GetPlayerCameraController()._audioListener.enabled = false;

        if (SpectateTarget != null)
        {
            SpectateTarget.Camera.enabled = false;
            SpectateTarget.Camera.gameObject.tag = "Untagged";
            SpectateTarget.AudioListener.enabled = false;

            if (SpectateTarget is ErnestoSpectatorCamera)
            {
                SpectateTarget.Detector.SetOccupantType(DynamicOccupant.Environment);
                SpectateTarget.GetComponent<ErnestoMovement>().ClearSectors();
                SpectateTarget.Detector.gameObject.SetActive(false);
            }
            else if (SpectateTarget is PlayerSpectatorCamera)
            {
                SpectateTarget.Detector.SetOccupantType(DynamicOccupant.Environment);
                var tracker = SpectateTarget.Detector.GetComponent<PlayerSectorTrackerRemote>();
                tracker.ClearSectors();
                tracker.enabled = false;
                //SpectateTarget.Detector.gameObject.SetActive(false);
            }
            else
            {
                SpectateTarget.Detector.gameObject.SetActive(false);
            }
        }
        
        GlobalMessenger<OWCamera>.FireEvent("SwitchActiveCamera", camera.Camera);

        SpectateTarget = camera;
        lastSpectateTargetState = true;
        camera.Camera.enabled = true;
        camera.Camera.gameObject.tag = "MainCamera";
        camera.AudioListener.enabled = true;

        if (camera is ErnestoSpectatorCamera)
        {
            camera.Detector.SetOccupantType(DynamicOccupant.Player);
            camera.Detector.gameObject.SetActive(true);
            camera.GetComponent<ErnestoMovement>().UpdateSectors();
        }
        else if (camera is PlayerSpectatorCamera)
        {
            camera.Detector.SetOccupantType(DynamicOccupant.Player);
            var tracker = camera.Detector.GetComponent<PlayerSectorTrackerRemote>();
            tracker.enabled = true;
            tracker.UpdateSectors();
        }
        else
        {
            camera.Detector.gameObject.SetActive(true);
        }
    }

    public void LoadRingWorld()
    {
        if (!loadedRingWorld)
        {
            ErnestoChase.WriteDebugMessage("loading ring world");
            foreach (var proxy in FindObjectsOfType<CloakingFieldProxy>())
            {
                proxy.OnPlayerEnterCloakingField();
            }

            loadedRingWorld = true;
            unloadRingWorldNextFrame = false;
        }
    }

    public void UnloadRingWorld()
    {
        if (loadedRingWorld)
        {
            loadedRingWorld = false;
            unloadRingWorldNextFrame = true;
        }
    }

    private void UnloadRingWorld_Internal()
    {
        ErnestoChase.WriteDebugMessage("unloading ring world");
        foreach (var proxy in FindObjectsOfType<CloakingFieldProxy>())
        {
            proxy.OnPlayerExitCloakingField();
        }

        loadedRingWorld = false;
    }

    public void LoadDreamWorld()
    {
        if (loadedDreamWorld) return;
        
        ErnestoChase.WriteDebugMessage("loading dream world");
        
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
        unloadDreamWorldNextFrame = false;
    }

    public void UnloadDreamWorld()
    {
        if (!loadedDreamWorld) return;
        
        loadedDreamWorld = false;
        unloadDreamWorldNextFrame = true;
    }

    private void UnloadDreamWorld_Internal()
    {
        ErnestoChase.WriteDebugMessage("unloading dream world");
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
        GameObject remoteCam = LoadPrefab("Assets/ErnestoChase/PlayerRemoteSpectatorCam.prefab");
        GameObject remoteCamObj = Instantiate(remoteCam, body.transform.Find("REMOTE_PlayerCamera"));
        var cam = remoteCamObj.GetComponent<PlayerSpectatorCamera>();

        var detector = QSBInteraction.GetRemoteFluidDetector(playerID).GetAddComponent<SectorDetector>();
        detector.SetOccupantType(DynamicOccupant.Environment);
        
        cam.AssignPlayer(playerID, detector);
        playerSpectatorCams.Add(cam);

        detector.gameObject.AddComponent<PlayerSectorTrackerRemote>().enabled = false;
    }
    
    private void OnPlayerDeath(DeathType deathType)
    {
        if (spectating)
        {
            ExitSpectate(true);
        }
    }
    
    private void OnDestroy()
    {
        Locator.GetPromptManager().RemoveScreenPrompt(_changeSpectateTargetPrompt);
        Locator.GetPromptManager().RemoveScreenPrompt(_changeSpectateTypePrompt);
        Locator.GetPromptManager().RemoveScreenPrompt(_enterSpectateModePrompt);
        Locator.GetPromptManager().RemoveScreenPrompt(_exitSpectateModePrompt);
        
        GlobalMessenger<DeathType>.RemoveListener("PlayerDeath", OnPlayerDeath);
    }
}