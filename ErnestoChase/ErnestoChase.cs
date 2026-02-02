using Newtonsoft.Json.Linq;
using OWML.Common;
using OWML.ModHelper;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;
using OWML.Utils;
using System.Reflection;
using System.Linq;
using UnityEngine.Events;
using OWML.ModHelper.Menus.NewMenuSystem;
using System.Globalization;
using UnityEngine.UI;
using Newtonsoft.Json;
using UnityEngine.PostProcessing;
using CSharpFunctionalExtensions;
using MonoMod.Utils;

namespace ErnestoChase;

public class ErnestoChase : ModBehaviour
{
    public delegate void PlayerWarpEvent();
    public event PlayerWarpEvent OnPlayerWarped;

    public static ErnestoChase Instance;
    public static MinigameManager MinigameManager;
    public AssetBundle assetBundle;
    public GameObject ernesto;
    public bool playerDetectorReady = false;
    public List<GameObject> ernestos = [];
    public List<GameObject> oldErnestos = [];
    public List<GameObject> camErnestos = [];
    public List<TargetDataQueue> storedErnestoTargets = [];
    public Dictionary<uint, Dictionary<uint, GameObject>> remoteErnestos = [];
    public static IQSBAPI QSBAPI;
    public static IQSBInteraction QSBInteraction;
    public static INHInteraction NHInteraction;

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

    private CharacterDialogueTree _setupDialogue;

    public static uint[] Players => QSBAPI?.GetPlayerIDs().Where(id => id != QSBAPI.GetLocalPlayerID()).ToArray();

    public static bool InMultiplayer => QSBAPI != null && QSBAPI.GetIsInMultiplayer();

    public float MovementSpeedMultiplier => (float)settings["groundMovementSpeed"].property;
    public float SpaceSpeedMultiplier => (float)settings["spaceMovementSpeed"].property;
    public float BrambleSpeedMultiplier => (float)settings["brambleSpeedMultiplier"].property;
    public float DreamWorldSpeedMultiplier => (float)settings["dreamWorldSpeedMultiplier"].property;
    public float StartDelay => (float)settings["startDelay"].property;
    public string SpaceAccelerationType => (string)settings["spaceAccelerationType"].property;
    public float SpaceTimer => (float)settings["spaceTimer"].property;
    public bool StealthMode => (bool)settings["enableStealthMode"].property;
    public bool QuantumMode => (bool)settings["enableQuantumMode"].property;
    public bool CustomEndScreen => (bool)settings["customEndScreen"].property;
    public bool ErnestoCam => (bool)settings["ernestoCam"].property;
    public float ErnestoNumber => (float)settings["ernestoNumber"].property;
    public bool ErnestoStacking => (bool)settings["ernestoStacking"].property;
    public bool RandomMode => (bool)settings["randomMode"].property;
    public bool ErnestoMorph => (bool)settings["ernestoMorph"].property;
    public float SurvivalTimerLength => (float)settings["survivalTimerLength"].property;
    public bool AllowShipLog => (bool)settings["allowShipLog"].property;
    public bool GlobalFactGoals => (bool)settings["globalFactGoals"].property;
    public int ShipLogRounds => Mathf.Max(1, Mathf.FloorToInt((float)settings["shipLogRounds"].property));
    public bool EnableDLCLogs => (bool)settings["enableDLCLogs"].property;
    public int MaxRumorChain => Mathf.Max(1, Mathf.FloorToInt((float)settings["maxRumorChain"].property));

    public Dictionary<string, (object value, object property)> settings = new()
    {
        { "randomMode", (false, false) },
        { "groundMovementSpeed", (1f, 1f) },
        { "spaceMovementSpeed", (1f, 1f) },
        { "brambleSpeedMultiplier", (1f, 1f) },
        { "dreamWorldSpeedMultiplier", (1f, 1f) },
        { "startDelay", (1f, 1f) },
        { "spaceAccelerationType", ("", "") },
        { "spaceTimer", (1f, 1f) },
        { "enableStealthMode", (false, false) },
        { "enableQuantumMode", (false, false) },
        { "customEndScreen", (false, false) },
        { "ernestoCam", (false, false) },
        { "ernestoNumber", (1f, 1f) },
        { "ernestoStacking", (false, false) },
        { "ernestoMusic", (false, false) },
        { "disableLight", (false, false) },
        { "advancedSettings", (false, false) },
        { "ernestoMorph", (false, false) },
        { "survivalTimerLength", (1f, 1f) },
        { "allowShipLog", (false, false) },
        { "globalFactGoals", (false, false) },
        { "speedAccumulationType", ("", "") },
        { "speedAccumulationRate", (1f, 1f) },
        { "distanceSpeedMultiplier", (1f, 1f) },
        { "shipLogRounds", (1f, 1f) },
        { "enableDLCLogs", (false, false) },
        { "maxRumorChain", (1f, 1f) },
    };

    public static readonly bool EnableDebugMode = true;

    private void Awake()
    {
        Instance = this;
        MinigameManager = gameObject.AddComponent<MinigameManager>();
        HarmonyLib.Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
    }

    private void Start()
    {
        assetBundle = AssetBundle.LoadFromFile(Path.Combine(ModHelper.Manifest.ModFolderPath, "assets/ernestochase"));
        ernesto = LoadPrefab("Assets/ErnestoChase/Ernesto.prefab");
        AssetBundleUtilities.ReplaceShaders(ernesto);
        ernesto.SetActive(false);

        _changeSpectateTargetPrompt = new(InputLibrary.toolOptionLeft, InputLibrary.toolOptionRight, 
            "Switch Spectate Target" + " <CMD1> <CMD2>", ScreenPrompt.MultiCommandType.CUSTOM_BOTH);
        _changeSpectateTypePrompt = new(InputLibrary.toolOptionUp, InputLibrary.toolOptionDown,
            "Switch Spectate Type" + " <CMD1> <CMD2>", ScreenPrompt.MultiCommandType.CUSTOM_BOTH);
        _enterSpectateModePrompt = new(InputLibrary.map, "Enter Spectator Mode");
        _exitSpectateModePrompt = new(InputLibrary.map, "Exit Spectator Mode");

        InitializeQSB();
        InitializeNH();
        
        GlobalMessenger<DeathType>.AddListener("PlayerDeath", OnPlayerDeath);

        LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
        {
            if (loadScene != OWScene.SolarSystem) return;

            if (scene != OWScene.SolarSystem)
            {
                ErnestoConditionManager.Reset();
            }

            playerDetectorReady = false;
            ernestos.Clear();
            camErnestos.Clear();
            oldErnestos.Clear();
            remoteErnestos.Clear();
            playerRingWorldStates.Clear();
            ernestoRingWorldStates.Clear();
            PatchnestoClass.Initialize();

            UpdateProperties();

            if (!ErnestoStacking)
            {
                storedErnestoTargets.Clear();
            }

            if (ErnestoConditionManager.StartingGame)
            {
                ErnestoConditionManager.GameStarted = true;
            }

            var prefab = LoadPrefab("Assets/ErnestoChase/EC_SetupDialogue.prefab");
            var obj = Instantiate(prefab, FindObjectOfType<PlayerCameraController>().transform);
            obj.transform.localPosition = new Vector3(0f, 0f, 1.5f);
            _setupDialogue = obj.GetComponentInChildren<CharacterDialogueTree>();
            _setupDialogue.OnEndConversation += OnEndSetupConversation;
            DialogueBuilder.FixCustomDialogue(obj, "ConversationZone");

            if (ErnestoConditionManager.GameStarted)
            {
                StartCoroutine(WaitForPlayer());

                if (InMultiplayer)
                {
                    Locator.GetCloakFieldController()?.OnPlayerEnter -= OnPlayerTriggerCloak;
                    Locator.GetCloakFieldController()?.OnPlayerExit -= OnPlayerTriggerCloak;
                    GlobalMessenger.AddListener("EnterDreamWorld", OnPlayerTriggerDreamWorld);
                    GlobalMessenger.AddListener("ExitDreamWorld", OnPlayerTriggerDreamWorld);
                    
                    foreach (uint id in Players)
                    {
                        StartCoroutine(AddCamToRemotePlayer(id));
                    }
                }
            }
        };

        LoadManager.OnStartSceneLoad += (scene, loadScene) =>
        {
            if (scene != OWScene.SolarSystem || loadScene != OWScene.SolarSystem) return;
            
            MinigameManager.OnSceneUnloaded();

            ernestoSpectatorCams.Clear();
            playerSpectatorCams.Clear();
            spectating = false;

            foreach (var list in storedErnestoTargets)
            {
                list.Reset();
            }

            foreach (var ernesto in ernestos)
            {
                if (oldErnestos.Contains(ernesto))
                {
                    oldErnestos.Remove(ernesto);
                    continue;
                }

                if (InMultiplayer && remoteErnestos[0].All(e => e.Value != ernesto))
                {
                    continue;
                }

                storedErnestoTargets.Add(ernesto.GetComponent<ErnestoManager>().GetStoredTargets());
            }

            if (_setupDialogue != null)
            {
                _setupDialogue.OnEndConversation -= OnEndSetupConversation;
            }

            if (InMultiplayer)
            {
                Locator.GetPromptManager().RemoveScreenPrompt(_changeSpectateTargetPrompt);
                Locator.GetPromptManager().RemoveScreenPrompt(_changeSpectateTypePrompt);
                Locator.GetPromptManager().RemoveScreenPrompt(_enterSpectateModePrompt);
                Locator.GetPromptManager().RemoveScreenPrompt(_exitSpectateModePrompt);

                Locator.GetCloakFieldController()?.OnPlayerEnter -= OnPlayerTriggerCloak;
                Locator.GetCloakFieldController()?.OnPlayerExit -= OnPlayerTriggerCloak;
                GlobalMessenger.RemoveListener("EnterDreamWorld", OnPlayerTriggerDreamWorld);
                GlobalMessenger.RemoveListener("ExitDreamWorld", OnPlayerTriggerDreamWorld);
            }
        };
    }

    private void Update()
    {
        if (LoadManager.GetCurrentScene() != OWScene.SolarSystem) return;

        if ((!InMultiplayer || QSBInteraction.GetLocalPlayerReady() && 
            QSBAPI.GetPlayerReady(QSBAPI.GetLocalPlayerID()) && 
            !QSBAPI.GetPlayerDead(QSBAPI.GetLocalPlayerID())))
        {
            // make VR compatible (not keyboard)
            if (OWInput.IsInputMode(InputMode.Character) && Keyboard.current.iKey.wasPressedThisFrame)
            {
                _setupDialogue.StartConversation();
            }
            else if (OWInput.IsNewlyPressed(InputLibrary.cancel, InputMode.Dialogue) &&
                _setupDialogue.InConversation())
            {
                _setupDialogue.EndConversation();
            }
        }
        
        if (!InMultiplayer) return;

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
            ModHelper.Console.WriteLine("Tried to switch to a target that can't be spectated!", MessageType.Error);
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
    
    private void InitializeQSB()
    {
        bool qsbEnabled = ModHelper.Interaction.ModExists("Raicuparta.QuantumSpaceBuddies");
        if (qsbEnabled)
        {
            QSBAPI = ModHelper.Interaction.TryGetModApi<IQSBAPI>("Raicuparta.QuantumSpaceBuddies");
            QSBCompat.Init(QSBAPI);
            var qsbAssembly = Assembly.LoadFrom(Path.Combine(ModHelper.Manifest.ModFolderPath, "ErnestoChaseQSB.dll"));
            gameObject.AddComponent(qsbAssembly.GetType("ErnestoChaseQSB.QSBInteraction", true));

            QSBAPI.RegisterRequiredForAllPlayers(this);
        }
    }
    
    private void InitializeNH()
    {
        bool nhEnabled = ModHelper.Interaction.ModExists("xen.NewHorizons");
        if (nhEnabled)
        {
            var nhAssembly = Assembly.LoadFrom(Path.Combine(ModHelper.Manifest.ModFolderPath, "ErnestoChaseNH.dll"));
            gameObject.AddComponent(nhAssembly.GetType("ErnestoChaseNH.NHInteraction", true));
        }
    }

    public void SetQSBInterface(IQSBInteraction i)
    {
        QSBInteraction = i;
    }
    
    public void SetNHInterface(INHInteraction i)
    {
        NHInteraction = i;
    }

    private void UpdateProperties()
    {
        var keys = settings.Keys.ToArray();
        for (int i = 0; i < keys.Length; i++)
        {
            settings[keys[i]] = (settings[keys[i]].value, settings[keys[i]].value);
        }
    }

    private Dictionary<string, (object value, object property)> GenerateRandomSettings()
    {
        var keys = settings.Keys.ToArray();
        Dictionary<string, (object value, object property)> output = new();

        foreach (var pair in settings)
        {
            output.Add(pair.Key, pair.Value);
        }

        var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            File.ReadAllText(Path.Combine(ModHelper.Manifest.ModFolderPath, "RandomizerSettings.json"))
        );

        for (int i = 0; i < keys.Length; i++)
        {
            string setting = keys[i];
            output[setting] = (output[setting].value, output[setting].value);

            if (data.ContainsKey(setting))
            {
                try
                {
                    var optionData = JsonConvert.DeserializeObject<RandomOption>(data[setting].ToString());

                    if (UnityEngine.Random.value < optionData.Chance)
                    {
                        int randIndex = UnityEngine.Random.Range(0, optionData.Options.Length);
                        output[setting] = (output[setting].value, optionData.Options[randIndex]);
                    }
                    continue;
                }
                catch (JsonSerializationException) { }

                try
                {
                    var floatData = JsonConvert.DeserializeObject<RandomFloat>(data[setting].ToString());
                    if (UnityEngine.Random.value < floatData.Chance)
                    {
                        output[setting] = (output[setting].value, UnityEngine.Random.Range(floatData.Min, floatData.Max));
                    }
                    continue;
                }
                catch (JsonSerializationException) { }

                try
                {
                    var boolData = JsonConvert.DeserializeObject<RandomBool>(data[setting].ToString());
                    ErnestoChase.WriteDebugMessage("Processing " + boolData);

                    output[setting] = (output[setting].value, UnityEngine.Random.value < boolData.Chance);
                    ErnestoChase.WriteDebugMessage("Set to " + output[setting].property + "\n");
                    continue;
                }
                catch (JsonSerializationException) { }
            }
        }

        return output;
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

    public static bool TryGetRemoteErnesto(uint playerID, uint localID, out GameObject remoteErnesto)
    {
        if (Instance.remoteErnestos.ContainsKey(playerID) && Instance.remoteErnestos[playerID].ContainsKey(localID))
        {
            remoteErnesto = Instance.remoteErnestos[playerID][localID];
            return true;
        }

        remoteErnesto = null;
        return false;
    }

    private IEnumerator WaitForPlayer()
    {
        yield return new WaitUntil(() => Locator.GetPlayerBody() != null 
        && (!InMultiplayer || QSBAPI.GetPlayerReady(QSBAPI.GetLocalPlayerID())));

        Locator.GetPromptManager().AddScreenPrompt(_changeSpectateTargetPrompt, PromptPosition.UpperRight);
        Locator.GetPromptManager().AddScreenPrompt(_changeSpectateTypePrompt, PromptPosition.UpperRight);
        Locator.GetPromptManager().AddScreenPrompt(_enterSpectateModePrompt, PromptPosition.BottomCenter);
        Locator.GetPromptManager().AddScreenPrompt(_exitSpectateModePrompt, PromptPosition.UpperRight);

        MinigameManager.SetUpMinigames();
        
        ModHelper.Events.Unity.FireInNUpdates(() =>
        {
            if (ErnestoMorph)
            {
                GameObject prefab = LoadPrefab("Assets/ErnestoChase/ControllableErnesto_Body.prefab");
                ControllableErnesto ernesto = Instantiate(prefab, Locator.GetPlayerTransform().position + Locator.GetPlayerTransform().up * 3f, Locator.GetPlayerTransform().rotation)
                    .GetComponent<ControllableErnesto>();
                ModHelper.Events.Unity.FireOnNextUpdate(ernesto.AttachPlayer);

                if (InMultiplayer)
                {
                    ErnestoData fakeData = new();
                    foreach (var id in Players)
                    {
                        QSBCompat.SendControlledErnestoData(id, fakeData);
                    }
                }
            }

            SpawnErnestos();
        }, 50);
    }

    private void SpawnErnestos()
    {
        for (int i = 0; i < (int)ErnestoNumber + storedErnestoTargets.Count; i++)
        {
            GameObject ernestoObj = Instantiate(ernesto, Locator.GetPlayerTransform().position, Quaternion.identity);
            ErnestoManager manager = ernestoObj.GetComponent<ErnestoManager>();
            ErnestoState state = ernestoObj.GetComponent<ErnestoState>();

            for (uint s = 1; s <= 2; s++)
            {
                if (RandomMode)
                {
                    state.InitializeStats(s, GenerateRandomSettings());
                }
                else
                {
                    Dictionary<string, (object value, object property)> customSettings = [];
                    customSettings.AddRange(settings);
                    if (s == 1)
                    {
                        customSettings["enableStealthMode"] = (true, true);
                        customSettings["ernestoMusic"] = (true, true);
                        customSettings["groundMovementSpeed"] = (0.1f, 0.1f);
                        customSettings["distanceSpeedMultiplier"] = (5f, 5f);
                    }
                    if (s == 2)
                    {
                        customSettings["groundMovementSpeed"] = (2f, 2f);
                        customSettings["distanceSpeedMultiplier"] = (1.5f, 1.5f);
                        customSettings["enableStealthMode"] = (false, false);
                        customSettings["ernestoMusic"] = (true, true);
                    }
                    state.InitializeStats(s, customSettings);
                }
            }

            ernestoObj.SetActive(true);
            manager.IncrementSpawnDelay(i);
            ernestos.Add(ernestoObj);

            if (state.ErnestoCam)
            {
                camErnestos.Add(ernestoObj);
            }

            if (ErnestoStacking && i < storedErnestoTargets.Count)
            {
                manager.SetStoredTargets(storedErnestoTargets[i]);
                oldErnestos.Add(ernestoObj);
            }

            if (InMultiplayer)
            {
                if (!remoteErnestos.ContainsKey(0))
                {
                    remoteErnestos.Add(0, []);
                }

                int numErnestos = remoteErnestos[0].Count;
                if (ErnestoMorph)
                {
                    numErnestos++;
                }
                uint localID = Convert.ToUInt32(numErnestos);
                remoteErnestos[0].Add(localID, ernestoObj);

                for (uint k = 1; k <= state.DataStates.Count; k++)
                {
                    state.DataStates[k].localid = localID;
                }
                
                foreach (var id in Players)
                {
                    QSBCompat.SendErnestoData(id, state.DataStates);
                }
            }
        }
    }

    public IEnumerator SpawnErnestoRemote(Dictionary<uint, ErnestoData> dataStates)
    {
        yield return new WaitUntil(() => QSBAPI.GetPlayerReady(QSBAPI.GetLocalPlayerID()));

        ErnestoChase.WriteDebugMessage("Spawn Ernesto with data");

        GameObject ernestoObj = Instantiate(ernesto, Locator.GetPlayerTransform().position, Quaternion.identity);
        ErnestoManager manager = ernestoObj.GetComponent<ErnestoManager>();
        ErnestoState state = ernestoObj.GetComponent<ErnestoState>();

        state.SetData(dataStates);

        ernestoObj.SetActive(true);
        ernestos.Add(ernestoObj);

        if (state.ErnestoCam)
        {
            camErnestos.Add(ernestoObj);
        }

        var firstState = dataStates[dataStates.Keys.First()];
        if (!remoteErnestos.ContainsKey(firstState.id))
        {
            remoteErnestos.Add(firstState.id, []);
        }

        remoteErnestos[firstState.id].Add(firstState.localid, ernestoObj);
        manager.SetStoredTargets(new TargetDataQueue());

        /*if (ErnestoStacking && storedErnestoTargets.Count > i)
        {
            manager.SetStoredTargets(storedErnestoTargets[i]);
            oldErnestos.Add(ernestoObj);
        }*/
    }

    public IEnumerator SpawnControlledErnestoRemote(ErnestoData data)
    {
        yield return new WaitUntil(() => QSBAPI.GetPlayerReady(QSBAPI.GetLocalPlayerID()) 
            && QSBAPI.GetPlayerReady(data.id));

        ErnestoChase.WriteDebugMessage("Spawn controlled Ernesto with data");

        GameObject ernestoObj = Instantiate(ernesto, Locator.GetPlayerTransform().position, Quaternion.identity);
        ErnestoManager manager = ernestoObj.GetComponent<ErnestoManager>();
        ErnestoState state = ernestoObj.GetComponent<ErnestoState>();
        ernestoObj.AddComponent<RemoteSizeChanger>();

        state.SetData(1, data);
        state.AIEnabled = false;

        ernestoObj.SetActive(true);
        ernestos.Add(ernestoObj);

        if (state.ErnestoCam)
        {
            camErnestos.Add(ernestoObj);
        }

        if (!remoteErnestos.ContainsKey(data.id))
        {
            remoteErnestos.Add(data.id, []);
        }

        remoteErnestos[data.id].Add(data.localid, ernestoObj);
        manager.SetStoredTargets(new());

        Transform remoteParent = QSBAPI.GetPlayerBody(data.id).transform;
        foreach (var renderer in remoteParent.GetComponentsInChildren<Renderer>())
        {
            renderer.forceRenderingOff = true;
        }
        ernestoObj.transform.parent = remoteParent;
        ModHelper.Events.Unity.FireInNUpdates(() =>
        {
            ernestoObj.transform.localPosition = new Vector3(0f, 0f, 0f);
            ernestoObj.transform.localRotation = Quaternion.identity;
        }, 10);

        /*if (ErnestoStacking && storedErnestoTargets.Count > i)
        {
            manager.SetStoredTargets(storedErnestoTargets[i]);
            oldErnestos.Add(ernestoObj);
        }*/
    }

    public void AddTargetDataRemote(uint from, uint localID, TargetDataQueue.TargetData targetData)
    {
        if (TryGetRemoteErnesto(from, localID, out GameObject remoteErnesto))
        {
            remoteErnesto?.GetComponent<ErnestoMovement>()?.AddTargetData(targetData);
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

    public void OnPlayerWarpedEvent()
    {
        OnPlayerWarped?.Invoke();
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

    private void OnEndSetupConversation()
    {
        GameObject.FindWithTag("DialogueGui").GetRequiredComponent<DialogueBoxVer2>()._revealingOptions = false;
        
        if (!ErnestoConditionManager.GameStarted && 
            DialogueConditionManager.SharedInstance.GetConditionState("EC_START_GAME"))
        {
            ErnestoConditionManager.StartingGame = true;
            Locator.GetDeathManager().KillPlayer(DeathType.Meditation);
            DialogueConditionManager.SharedInstance.SetConditionState("EC_START_GAME");

            if (InMultiplayer)
            {
                foreach (var id in Players)
                {
                    QSBCompat.SendStartGame(id);
                }
            }
        }
        else if (ErnestoConditionManager.GameStarted &&
            DialogueConditionManager.SharedInstance.GetConditionState("EC_STOP_GAME"))
        {
            DialogueConditionManager.SharedInstance.SetConditionState("EC_STOP_GAME");
            ErnestoConditionManager.Reset();

            MinigameManager.OnGameStopped();
            
            foreach (var e in ernestos)
            {
                e.GetComponent<ErnestoManager>().OnGameStopped();
            }
            
            if (InMultiplayer)
            {
                foreach (var id in Players)
                {
                    QSBCompat.SendStopGame(id);
                }
            }
        }
    }

    public void OnInputDialogueOption(CharacterDialogueTree tree)
    {
        if (tree != _setupDialogue) return;
        
        if (DialogueConditionManager.SharedInstance.GetConditionState("EC_RSSR_MODE_SELECTED"))
        {
            ErnestoConditionManager.RandomShipLogEnabled = true;
            DialogueConditionManager.SharedInstance.SetConditionState("EC_RSSR_MODE_SELECTED");
        }
        else if (DialogueConditionManager.SharedInstance.GetConditionState("EC_SURVIVAL_MODE_SELECTED"))
        {
            ErnestoConditionManager.SurvivalEnabled = true;
            DialogueConditionManager.SharedInstance.SetConditionState("EC_SURVIVAL_MODE_SELECTED");
        }
        else if (DialogueConditionManager.SharedInstance.GetConditionState("EC_MINIGAMES_DISABLED"))
        {
            ErnestoConditionManager.DeselectMinigame();
            DialogueConditionManager.SharedInstance.SetConditionState("EC_MINIGAMES_DISABLED");
        }
    }

    public void StopGameRemote()
    {
        DialogueConditionManager.SharedInstance.SetConditionState("EC_STOP_GAME");
        ErnestoConditionManager.Reset();
            
        foreach (var e in ernestos)
        {
            e.GetComponent<ErnestoManager>().OnGameStopped();
        }
    }

    public void OnCountdownComplete()
    {
        MinigameManager.OnGameStopped();
        foreach (var e in ernestos)
        {
            e.GetComponent<ErnestoManager>().OnGameStopped();
        }
    }

    public void RespawnErnesto()
    {
        foreach (GameObject ernesto in ernestos)
        {
            Destroy(ernesto);
            StartCoroutine(WaitForPlayer());
        }
    }

    public static GameObject LoadPrefab(string path)
    {
        return (GameObject)Instance.assetBundle.LoadAsset(path);
    }

    public static void WriteDebugMessage(object message)
    {
        if (EnableDebugMode)
        {
            message ??= "null";
            Instance.ModHelper.Console.WriteLine(message.ToString());
        }
    }

    public override void Configure(IModConfig config)
    {
        var keys = settings.Keys.ToArray();
        //bool anyChanged = false;
        for (int i = 0; i < keys.Length; i++)
        {
            /*object configValue = ConvertJValue(config.GetSettingsValue<object>(keys[i]));
            if (!anyChanged && !settings[keys[i]].value.Equals(configValue)
                && (keys[i] == "spaceAccelerationType" || keys[i] == "advancedSettings"))
            {
                WriteDebugMessage(keys[i] + " was changed");
                anyChanged = true;
            }*/
            settings[keys[i]] = (ConvertJValue(config.GetSettingsValue<object>(keys[i])), settings[keys[i]].property);
        }

        /*if (anyChanged)
        {
            ECMenuManager.RedrawSettingsMenu();
        }*/
    }

    public static object ConvertJValue(object obj)
    {
        if (obj is not JValue) return null;

        JValue value = (JValue)obj;
        if (value.Type == JTokenType.Boolean)
        {
            return Convert.ToBoolean(value);
        }
        else if (value.Type == JTokenType.Float)
        {
            return float.Parse(value.ToString());
        }
        else if (value.Type == JTokenType.Integer)
        {
            return (float)int.Parse(value.ToString());
        }
        else if (value.Type == JTokenType.String)
        {
            return value.ToString();
        }
        return value;
    }
}
