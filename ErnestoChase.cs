﻿using OWML.Common;
using OWML.ModHelper;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ErnestoChase;

public class ErnestoChase : ModBehaviour
{
    public static ErnestoChase Instance;
    public AssetBundle assetBundle;
    public OWRigidbody ernestoBody;
    public bool playerDetectorReady = false;
    public ErnestoController ernesto;
    public bool caughtPlayer;
    public bool inFogWarp = false;

    public float MovementSpeed;
    public float SpaceSpeed;
    public float BrambleSpeedMultiplier;
    public float DreamWorldSpeedMultiplier;
    public float StartDelay;
    public string SpaceAccelerationType;
    public float SpaceTimer;
    public bool StealthMode;
    public bool QuantumMode;
    public bool CustomEndScreen;

    private float movementSpeed;
    private float spaceSpeed;
    private float brambleSpeedMultiplier;
    private float dreamWorldSpeedMultiplier;
    private float startDelay;
    private string spaceAccelerationType;
    private float spaceTimer;
    private bool stealthMode;
    private bool quantumMode;
    private bool customEndScreen;

    public static readonly bool EnableDebugMode = false;

    private void Awake()
    {
        Instance = this;
        HarmonyLib.Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly());
    }

    private void Start()
    {
        assetBundle = AssetBundle.LoadFromFile(Path.Combine(ModHelper.Manifest.ModFolderPath, "assets/ernestochase"));

        GlobalMessenger.AddListener("PlayerFogWarp", OnPlayerFogWarp);

        // Example of accessing game code.
        LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
        {
            if (loadScene != OWScene.SolarSystem) return;

            playerDetectorReady = false;
            caughtPlayer = false;
            ernesto = null;
            ernestoBody = null;
            inFogWarp = false;

            MovementSpeed = movementSpeed;
            SpaceSpeed = spaceSpeed;
            BrambleSpeedMultiplier = brambleSpeedMultiplier;
            DreamWorldSpeedMultiplier = dreamWorldSpeedMultiplier;
            StartDelay = startDelay;
            SpaceAccelerationType = spaceAccelerationType;
            SpaceTimer = spaceTimer;
            StealthMode = stealthMode;
            QuantumMode = quantumMode;
            //CustomEndScreen = customEndScreen;

            StartCoroutine(WaitForPlayer());
        };
    }

    private IEnumerator WaitForPlayer()
    {
        WriteDebugMessage("Waiting for player");
        yield return new WaitUntil(() => Locator.GetPlayerBody() != null);
        WriteDebugMessage("Player found");
        GameObject body = LoadPrefab("Assets/ErnestoChase/ErnestoBody.prefab");
        ernestoBody = Instantiate(body, Vector3.zero, Quaternion.identity).GetComponent<OWRigidbody>();
        GameObject ernestoObj = LoadPrefab("Assets/ErnestoChase/Ernesto.prefab");
        AssetBundleUtilities.ReplaceShaders(ernestoObj);
        ernesto = Instantiate(ernestoObj, Locator.GetPlayerTransform().position, Quaternion.identity).GetComponent<ErnestoController>();
    }

    private void OnPlayerFogWarp()
    {
        inFogWarp = true;
    }

    public void RespawnErnesto()
    {
        if (ernesto != null)
        {
            Destroy(ernesto.gameObject);
            caughtPlayer = false;
            inFogWarp = false;

            GameObject ernestoObj = LoadPrefab("Assets/ErnestoChase/Ernesto.prefab");
            AssetBundleUtilities.ReplaceShaders(ernestoObj);
            ernesto = Instantiate(ernestoObj, Locator.GetPlayerTransform().position, Quaternion.identity).GetComponent<ErnestoController>();
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
            Instance.ModHelper.Console.WriteLine(message.ToString());
        }
    }

    public override void Configure(IModConfig config)
    {
        movementSpeed = config.GetSettingsValue<int>("movementSpeed");
        spaceSpeed = config.GetSettingsValue<int>("spaceSpeed");

        brambleSpeedMultiplier = config.GetSettingsValue<float>("brambleSpeedMultiplier");
        dreamWorldSpeedMultiplier = config.GetSettingsValue<float>("dreamWorldSpeedMultiplier");
        startDelay = config.GetSettingsValue<float>("startDelay");
        spaceAccelerationType = config.GetSettingsValue<string>("spaceAccelerationType");
        spaceTimer = config.GetSettingsValue<float>("spaceTimer");
        stealthMode = config.GetSettingsValue<bool>("enableStealthMode");
        quantumMode = config.GetSettingsValue<bool>("enableQuantumMode");
        customEndScreen = config.GetSettingsValue<bool>("customEndScreen");
        CustomEndScreen = customEndScreen;
    }
}
