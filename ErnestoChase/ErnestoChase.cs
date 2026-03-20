using Newtonsoft.Json.Linq;
using OWML.Common;
using OWML.ModHelper;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json;
using ErnestoChase.ErnestoAI;
using ErnestoChase.Files;
using ErnestoChase.Interaction;

namespace ErnestoChase;

public class ErnestoChase : ModBehaviour
{
    public delegate void PlayerWarpEvent();
    public event PlayerWarpEvent OnPlayerWarped;

    public static ErnestoChase Instance;
    public static Minigames.MinigameManager MinigameManager;
    public static Spectating.SpectateManager SpectateManager;
    public static SaveDataJson CurrentSave;
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
    public static List<OWRigidbody> ActiveIslands = [];

    private CharacterDialogueTree _setupDialogue;

    public static uint[] Players => QSBAPI?.GetPlayerIDs().Where(id => 
        id != QSBAPI.GetLocalPlayerID()).ToArray();
    public static uint[] AlivePlayers => QSBAPI?.GetPlayerIDs().Where(id => 
        id != QSBAPI.GetLocalPlayerID() && !QSBAPI.GetPlayerDead(id)).ToArray();

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
        MinigameManager = gameObject.AddComponent<Minigames.MinigameManager>();
        HarmonyLib.Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
    }

    private void Start()
    {
        assetBundle = AssetBundle.LoadFromFile(Path.Combine(ModHelper.Manifest.ModFolderPath, "assets/ernestochase"));
        CurrentSave = ModHelper.Storage.Load<SaveDataJson>("save.json");
        CurrentSave ??= new SaveDataJson();
        GameStateManager.LoadSaveData(CurrentSave);
        ernesto = LoadPrefab("Assets/ErnestoChase/Ernesto.prefab");
        ernesto.SetActive(false);

        InitializeQSB();
        InitializeNH();

        LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
        {
            if (loadScene != OWScene.SolarSystem)
            {
                return;
            }

            if (scene != OWScene.SolarSystem)
            {
                GameStateManager.Reset();
            }

            playerDetectorReady = false;
            ernestos.Clear();
            camErnestos.Clear();
            oldErnestos.Clear();
            remoteErnestos.Clear();
            PatchnestoClass.Initialize();

            UpdateProperties();

            if (!ErnestoStacking)
            {
                storedErnestoTargets.Clear();
            }

            if (GameStateManager.StartingGame)
            {
                GameStateManager.GameStarted = true;
            }

            DialogueConditionManager.SharedInstance.SetConditionState("EC_NON_HOST",
                InMultiplayer && !QSBAPI.GetIsHost());

            var spectate = new GameObject("EC_SpecatateManager");
            SpectateManager = spectate.AddComponent<Spectating.SpectateManager>();
            
            var prefab = LoadPrefab("Assets/ErnestoChase/EC_SetupDialogue.prefab");
            var obj = Instantiate(prefab, FindObjectOfType<PlayerCameraController>().transform);
            obj.transform.localPosition = new Vector3(0f, 0f, 1.5f);
            _setupDialogue = obj.GetComponentInChildren<CharacterDialogueTree>();
            _setupDialogue.OnEndConversation += OnEndSetupConversation;
            DialogueBuilder.FixCustomDialogue(obj, "ConversationZone");

            if (GameStateManager.GameStarted)
            {
                SetUpIslandTriggers();
                StartCoroutine(WaitForPlayer());
            }
        };

        LoadManager.OnStartSceneLoad += (scene, loadScene) =>
        {
            if (scene != OWScene.SolarSystem || loadScene != OWScene.SolarSystem) return;
            
            MinigameManager.OnSceneUnloaded();
            SpectateManager = null;

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

                if (InMultiplayer && remoteErnestos.ContainsKey(0) && 
                    remoteErnestos[0].All(e => e.Value != ernesto))
                {
                    continue;
                }

                if (ernesto.TryGetComponent(out ErnestoManager manager))
                {
                    storedErnestoTargets.Add(manager.GetStoredTargets());
                }
            }

            ActiveIslands.Clear();

            if (_setupDialogue != null)
            {
                _setupDialogue.OnEndConversation -= OnEndSetupConversation;
            }
        };
    }

    private void SetUpIslandTriggers()
    {
        var gb = GameObject.Find("GabbroIsland_Body");
        if (gb)
        {
            var vol = gb.transform.Find("Sector_GabbroIsland/Volumes_GabbroIsland/InheritanceVolume");
            if (vol) SpawnIslandTrigger(vol);
        }

        var st = GameObject.Find("StatueIsland_Body");
        if (st)
        {
            var vol = st.transform.Find("Sector_StatueIsland/Volumes_StatueIsland/InheritanceVolume (1)");
            if (vol) SpawnIslandTrigger(vol);
        }
        
        var cy = GameObject.Find("ConstructionYardIsland_Body");
        if (cy)
        {
            var vol = cy.transform.Find("Sector_ConstructionYard/Volumes_ConstructionYard/InheritanceVolume (2)");
            if (vol) SpawnIslandTrigger(vol);
        }
        
        var db = GameObject.Find("BrambleIsland_Body");
        if (db)
        {
            var vol = db.transform.Find("Sector_BrambleIsland/Volumes_BrambleIsland/InheritanceVolume (2)");
            if (vol) SpawnIslandTrigger(vol);
        }
    }

    private void SpawnIslandTrigger(Transform source)
    {
        var trigger = new GameObject("EC_IslandTriggerVolume");
        trigger.transform.parent = source.transform.parent;
        trigger.transform.localPosition = source.transform.localPosition;
        trigger.transform.localRotation = source.transform.localRotation;

        Shape shape = source.GetComponent<Shape>();
        if (shape is SphereShape sphere)
        {
            var copy = trigger.AddComponent<SphereShape>();
            copy.center = sphere.center;
            copy.radius = sphere.radius;
            copy.pointChecksOnly = sphere.pointChecksOnly;
        }
        else if (shape is CapsuleShape capsule)
        {
            var copy = trigger.AddComponent<CapsuleShape>();
            copy.center = capsule.center;
            copy.radius = capsule.radius;
            copy.height = capsule.height;
            copy.direction = capsule.direction;
            copy.pointChecksOnly = capsule.pointChecksOnly;
        }

        trigger.AddComponent<OWTriggerVolume>();
        trigger.AddComponent<IslandTriggerVolume>();
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
        foreach (var key in keys)
        {
            settings[key] = (settings[key].value, settings[key].value);
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

    public static void SaveData()
    {
        CurrentSave ??= new SaveDataJson();
        CurrentSave.SelectedMinigame = GameStateManager.GetSelectedMinigame();
        CurrentSave.SelectedShipLogModes = GameStateManager.GetSelectedShipLogModes();
        Instance.ModHelper.Storage.Save(CurrentSave, "save.json");
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

        

        MinigameManager.SetUpMinigames();
        
        ModHelper.Events.Unity.FireInNUpdates(() =>
        {
            if (ErnestoMorph)
            {
                GameObject prefab = LoadPrefab("Assets/ErnestoChase/PlayerMorphController.prefab");
                Instantiate(prefab, Locator.GetPlayerTransform());
                
                if (InMultiplayer)
                {
                    ErnestoData fakeData = new();
                    fakeData.id = InMultiplayer ? QSBAPI.GetLocalPlayerID() : 0;
                    fakeData.localid = 0;
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

            if (RandomMode)
            {
                state.InitializeStats(1, GenerateRandomSettings());
            }
            else
            {
                state.InitializeStats(1, settings);
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

        state.SetRemoteData(dataStates);

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
    }

    public IEnumerator SpawnControlledErnestoRemote(ErnestoData data)
    {
        yield return new WaitUntil(() => QSBAPI.GetPlayerReady(QSBAPI.GetLocalPlayerID()) 
            && QSBAPI.GetPlayerReady(data.id));

        ErnestoChase.WriteDebugMessage("Spawn controlled Ernesto");

        Transform remoteParent = QSBAPI.GetPlayerBody(data.id).transform;
        var obj = LoadPrefab("Assets/ErnestoChase/ControllableErnesto_Remote.prefab");
        obj.SetActive(false);
        GameObject ernestoObj = Instantiate(obj, Vector3.zero, Quaternion.identity, remoteParent);
        
        ernestoObj.SetActive(true);
        ernestos.Add(ernestoObj);
        
        // TODO: toggleable camera?
        camErnestos.Add(ernestoObj);

        if (!remoteErnestos.ContainsKey(data.id))
        {
            remoteErnestos.Add(data.id, []);
        }
        remoteErnestos[data.id].Add(data.localid, ernestoObj);

        remoteParent.gameObject.AddComponent<PlayerErnesto.ControllableErnestoRemote>();
    }

    public void AddTargetDataRemote(uint from, uint localID, TargetDataQueue.TargetData targetData)
    {
        if (TryGetRemoteErnesto(from, localID, out GameObject remoteErnesto))
        {
            remoteErnesto?.GetComponent<ErnestoMovement>()?.AddTargetData(targetData);
        }
    }

    public void OnPlayerWarpedEvent()
    {
        OnPlayerWarped?.Invoke();
    }

    private void OnEndSetupConversation()
    {
        GameObject.FindWithTag("DialogueGui").GetRequiredComponent<DialogueBoxVer2>()._revealingOptions = false;
        
        if (!GameStateManager.GameStarted && 
            DialogueConditionManager.SharedInstance.GetConditionState("EC_START_GAME"))
        {
            GameStateManager.StartingGame = true;
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
        else if (GameStateManager.GameStarted &&
            DialogueConditionManager.SharedInstance.GetConditionState("EC_STOP_GAME"))
        {
            StopGame();
        }
    }

    public void OnInputDialogueOption(CharacterDialogueTree tree)
    {
        if (tree != _setupDialogue) return;
        
        if (DialogueConditionManager.SharedInstance.GetConditionState("EC_RSSR_MODE_SELECTED"))
        {
            GameStateManager.SelectMinigame(Minigame.RandomShipLog);
            DialogueConditionManager.SharedInstance.SetConditionState("EC_RSSR_MODE_SELECTED");
        }
        else if (DialogueConditionManager.SharedInstance.GetConditionState("EC_SURVIVAL_MODE_SELECTED"))
        {
            GameStateManager.SelectMinigame(Minigame.Survival);
            DialogueConditionManager.SharedInstance.SetConditionState("EC_SURVIVAL_MODE_SELECTED");
        }
        else if (DialogueConditionManager.SharedInstance.GetConditionState("EC_MINIGAMES_DISABLED"))
        {
            GameStateManager.SelectMinigame(Minigame.None);
            DialogueConditionManager.SharedInstance.SetConditionState("EC_MINIGAMES_DISABLED");
        }
    }

    public void StopGame()
    {
        if (InMultiplayer && !QSBAPI.GetIsHost()) return;
        
        WriteDebugMessage("Stopping game");
        
        DialogueConditionManager.SharedInstance.SetConditionState("EC_STOP_GAME");
        GameStateManager.Reset();
        
        GlobalMessenger.FireEvent("EC_GameStopped");
            
        if (InMultiplayer)
        {
            foreach (var id in Players)
            {
                QSBCompat.SendStopGame(id);
            }
        }
    }

    public void StopGameRemote()
    {
        DialogueConditionManager.SharedInstance.SetConditionState("EC_STOP_GAME");
        GameStateManager.Reset();
        
        GlobalMessenger.FireEvent("EC_GameStopped");
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
        var obj = (GameObject)Instance.assetBundle.LoadAsset(path);
        if (obj)
        {
            AssetBundleUtilities.ReplaceShaders(obj);
            return obj;
        }

        return null;
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
        foreach (var key in keys)
        {
            settings[key] = (ConvertJValue(config.GetSettingsValue<object>(key)), settings[key].property);
        }
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

    private void OnDestroy()
    {
        SaveData();
    }
}
