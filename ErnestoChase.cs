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

namespace ErnestoChase;

public class ErnestoChase : ModBehaviour
{
    public delegate void PlayerWarpEvent();
    public event PlayerWarpEvent OnPlayerWarped;

    public static ErnestoChase Instance;
    public AssetBundle assetBundle;
    public GameObject ernesto;
    public bool playerDetectorReady = false;
    public List<GameObject> ernestos = [];
    public List<GameObject> oldErnestos = [];
    public List<GameObject> camErnestos = [];
    public List<TargetDataQueue> storedErnestoTargets = [];
    public Dictionary<uint, Dictionary<uint, GameObject>> remoteErnestos = [];
    public static IQSBAPI QSBAPI;
    public static uint[] Players => QSBAPI?.GetPlayerIDs().Where(id => id != QSBAPI.GetLocalPlayerID()).ToArray();

    public static bool InMultiplayer => QSBAPI != null && QSBAPI.GetIsInMultiplayer();

    public float MovementSpeed => (float)settings["groundMovementSpeed"].property;
    public float SpaceSpeed => (float)settings["spaceMovementSpeed"].property;
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
    public bool ErnestoMusic => (bool)settings["ernestoMusic"].property;

    private Dictionary<string, (object value, object property)> settings = new()
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
    };

    public static readonly bool EnableDebugMode = true;

    private void Awake()
    {
        Instance = this;
        HarmonyLib.Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
    }

    private void Start()
    {
        assetBundle = AssetBundle.LoadFromFile(Path.Combine(ModHelper.Manifest.ModFolderPath, "assets/ernestochase"));
        ernesto = LoadPrefab("Assets/ErnestoChase/Ernesto.prefab");
        AssetBundleUtilities.ReplaceShaders(ernesto);
        ernesto.SetActive(false);

        if (ModHelper.Interaction.ModExists("Raicuparta.QuantumSpaceBuddies"))
        {
            QSBAPI = ModHelper.Interaction.TryGetModApi<IQSBAPI>("Raicuparta.QuantumSpaceBuddies");
            QSBCompat.Init(QSBAPI);
        }

        LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
        {
            if (loadScene != OWScene.SolarSystem) return;

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

            StartCoroutine(WaitForPlayer());
        };

        LoadManager.OnStartSceneLoad += (scene, loadScene) =>
        {
            if (scene != OWScene.SolarSystem || loadScene != OWScene.SolarSystem) return;

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

                storedErnestoTargets.Add(ernesto.GetComponent<ErnestoManager>().GetStoredTargets());
            }
        };
    }

    private void UpdateProperties()
    {
        var keys = settings.Keys.ToArray();
        bool randomMode = (bool)settings["randomMode"].value;

        if (randomMode)
        {
            settings = GenerateRandomSettings();
        }
        else
        {
            for (int i = 0; i < keys.Length; i++)
            {
                settings[keys[i]] = (settings[keys[i]].value, settings[keys[i]].value);
            }
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

                    output[setting] = (output[setting].value, UnityEngine.Random.value < boolData.Chance);
                    continue;
                }
                catch (JsonSerializationException) { }
            }
        }

        return output;
    }

    private IEnumerator WaitForPlayer()
    {
        yield return new WaitUntil(() => Locator.GetPlayerBody() != null);
        SpawnErnestos();
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
                state.InitializeStats(GenerateRandomSettings());
            }
            else
            {
                state.InitializeStats(settings);
            }

            ernestoObj.SetActive(true);
            manager.IncrementSpawnDelay(i);
            ernestos.Add(ernestoObj);

            if (state.ErnestoCam)
            {
                camErnestos.Add(ernestoObj);
            }

            if (ErnestoStacking && storedErnestoTargets.Count > i)
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

                uint localID = Convert.ToUInt32(remoteErnestos[0].Count);
                state.LocalID = localID;
                remoteErnestos[0].Add(localID, ernestoObj);

                foreach (var id in Players)
                {
                    QSBCompat.SendErnestoData(id, state.GetData());
                }
            }
        }
    }

    public IEnumerator SpawnErnestoRemote(ErnestoData data)
    {
        yield return new WaitUntil(() => QSBAPI.GetPlayerReady(QSBAPI.GetLocalPlayerID()));

        ErnestoChase.WriteDebugMessage("Spawn Ernesto with data");

        GameObject ernestoObj = Instantiate(ernesto, Locator.GetPlayerTransform().position, Quaternion.identity);
        ErnestoManager manager = ernestoObj.GetComponent<ErnestoManager>();
        ErnestoState state = ernestoObj.GetComponent<ErnestoState>();

        state.SetData(data);

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

        /*if (ErnestoStacking && storedErnestoTargets.Count > i)
        {
            manager.SetStoredTargets(storedErnestoTargets[i]);
            oldErnestos.Add(ernestoObj);
        }*/
    }

    public void AddTargetDataRemote(uint from, uint localID, TargetDataQueue.TargetData targetData)
    {
        if (remoteErnestos.ContainsKey(from) && remoteErnestos[from].ContainsKey(localID))
        {
            remoteErnestos[from][localID].GetComponent<ErnestoManager>().AddTargetData(targetData);
        }
    }

    public void OnPlayerWarpedEvent()
    {
        OnPlayerWarped?.Invoke();
    }

    public void RespawnErnesto()
    {
        foreach (GameObject ernesto in ernestos)
        {
            Destroy(ernesto);
            WaitForPlayer();
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
        var keys = settings.Keys.ToArray();
        bool anyChanged = false;
        for (int i = 0; i < keys.Length; i++)
        {
            object configValue = ConvertJValue(config.GetSettingsValue<object>(keys[i]));
            if (!anyChanged && !settings[keys[i]].value.Equals(configValue)
                && keys[i] == "spaceAccelerationType")
            {
                WriteDebugMessage(keys[i] + " was changed");
                anyChanged = true;
            }
            settings[keys[i]] = (ConvertJValue(config.GetSettingsValue<object>(keys[i])), settings[keys[i]].property);
        }

        if (anyChanged)
        {
            RedrawSettingsMenu();
        }
    }

    public void RedrawSettingsMenu()
    {
        MenuManager menuManager = StartupPopupPatches.menuManager;
        IOptionsMenuManager OptionsMenuManager = menuManager.OptionsMenuManager;

        var menus = typeof(MenuManager).GetField("ModSettingsMenus", BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.Static).GetValue(menuManager)
            as List<(IModBehaviour behaviour, Menu modMenu)>;

        Menu newModTab = null;

        for (int i = 0; i < menus.Count; i++)
        {
            if ((object)menus[i].behaviour == this)
            {
                newModTab = menus[i].modMenu;
            }
        }

        if (newModTab == null) return;

        newModTab._menuOptions = [];

        Scrollbar scrollbar = newModTab.transform.Find("Scroll View/Scrollbar Vertical").GetComponent<Scrollbar>();
        float lastScrollValue = scrollbar.value;

        Transform settingsParent = newModTab.transform.Find("Scroll View/Viewport/Content");

        if (!DestroyExistingSettings(newModTab, settingsParent))
        {
            return;
        }

        OptionsMenuManager.AddSeparator(newModTab, true);
        OptionsMenuManager.CreateLabel(newModTab, "Any changes to Ernesto are applied on the next loop!");

        int startIndex = 0;
        int endIndex = ModHelper.Config.Settings.Count;

        for (int i = startIndex; i < endIndex; i++)
        {
            string name = ModHelper.Config.Settings.ElementAt(i).Key;

            if (ShouldHideSetting(i, name))
            {
                continue;
            }

            object setting = ModHelper.Config.Settings.ElementAt(i).Value;
            var settingType = GetSettingType(setting);
            var label = ModHelper.MenuTranslations.GetLocalizedString(name);
            var tooltip = "";

            var settingObject = setting as JObject;

            if (settingObject != default(JObject))
            {
                if (settingObject["dlcOnly"]?.ToObject<bool>() ?? false)
                {
                    if (EntitlementsManager.IsDlcOwned() == EntitlementsManager.AsyncOwnershipStatus.NotOwned)
                    {
                        continue;
                    }
                }

                if (settingObject["title"] != null)
                {
                    if (!SetCustomSettingName(ref label, name))
                    {
                        label = ModHelper.MenuTranslations.GetLocalizedString(settingObject["title"].ToString());
                    }
                }

                if (settingObject["tooltip"] != null)
                {
                    if (!SetCustomTooltip(ref tooltip, name))
                    {
                        tooltip = ModHelper.MenuTranslations.GetLocalizedString(settingObject["tooltip"].ToString());
                    }
                }
            }

            switch (settingType)
            {
                case SettingType.CHECKBOX:
                    var currentCheckboxValue = ModHelper.Config.GetSettingsValue<bool>(name);
                    var settingCheckbox = OptionsMenuManager.AddCheckboxInput(newModTab, label, tooltip, currentCheckboxValue);
                    settingCheckbox.ModSettingKey = name;
                    settingCheckbox.OnValueChanged += (bool newValue) =>
                    {
                        ModHelper.Config.SetSettingsValue(name, newValue);
                        ModHelper.Storage.Save(ModHelper.Config, Constants.ModConfigFileName);
                        Configure(ModHelper.Config);
                    };
                    break;
                case SettingType.TOGGLE:
                    var currentToggleValue = ModHelper.Config.GetSettingsValue<bool>(name);
                    var yes = settingObject["yes"].ToString();
                    var no = settingObject["no"].ToString();
                    var settingToggle = OptionsMenuManager.AddToggleInput(newModTab, label, yes, no, tooltip, currentToggleValue);
                    settingToggle.ModSettingKey = name;
                    settingToggle.OnValueChanged += (bool newValue) =>
                    {
                        ModHelper.Config.SetSettingsValue(name, newValue);
                        ModHelper.Storage.Save(ModHelper.Config, Constants.ModConfigFileName);
                        Configure(ModHelper.Config);
                    };
                    break;
                case SettingType.SELECTOR:
                    var currentSelectorValue = ModHelper.Config.GetSettingsValue<string>(name);
                    var options = settingObject["options"].ToArray().Select(x => x.ToString()).ToArray();
                    var currentSelectedIndex = Array.IndexOf(options, currentSelectorValue);
                    var settingSelector = OptionsMenuManager.AddSelectorInput(newModTab, label, options, tooltip, true, currentSelectedIndex);
                    settingSelector.ModSettingKey = name;
                    settingSelector.OnValueChanged += (int newIndex, string newSelection) =>
                    {
                        ModHelper.Config.SetSettingsValue(name, newSelection);
                        ModHelper.Storage.Save(ModHelper.Config, Constants.ModConfigFileName);
                        Configure(ModHelper.Config);
                    };
                    break;
                case SettingType.SEPARATOR:
                    OptionsMenuManager.AddSeparator(newModTab, true);
                    OptionsMenuManager.CreateLabel(newModTab, name);
                    OptionsMenuManager.AddSeparator(newModTab, false);
                    break;
                case SettingType.SLIDER:
                    var currentSliderValue = ModHelper.Config.GetSettingsValue<float>(name);
                    var lower = settingObject["min"].ToObject<float>();
                    var upper = settingObject["max"].ToObject<float>();
                    var settingSlider = OptionsMenuManager.AddSliderInput(newModTab, label, lower, upper, tooltip, currentSliderValue);
                    settingSlider.ModSettingKey = name;
                    settingSlider.OnValueChanged += (float newValue) =>
                    {
                        ModHelper.Config.SetSettingsValue(name, newValue);
                        ModHelper.Storage.Save(ModHelper.Config, Constants.ModConfigFileName);
                        Configure(ModHelper.Config);
                    };
                    break;
                case SettingType.TEXT:
                    var currentTextValue = ModHelper.Config.GetSettingsValue<string>(name);
                    var textInput = OptionsMenuManager.AddTextEntryInput(newModTab, label, currentTextValue, tooltip, false);
                    textInput.ModSettingKey = name;
                    textInput.OnConfirmEntry += () =>
                    {
                        var newValue = textInput.GetInputText();
                        ModHelper.Config.SetSettingsValue(name, newValue);
                        ModHelper.Storage.Save(ModHelper.Config, Constants.ModConfigFileName);
                        Configure(ModHelper.Config);
                        textInput.SetText(newValue);
                    };
                    break;
                case SettingType.NUMBER:
                    var currentValue = ModHelper.Config.GetSettingsValue<double>(name);
                    var numberInput = OptionsMenuManager.AddTextEntryInput(newModTab, label, currentValue.ToString(CultureInfo.CurrentCulture), tooltip, true);
                    numberInput.ModSettingKey = name;
                    numberInput.OnConfirmEntry += () =>
                    {
                        if (!string.IsNullOrEmpty(numberInput.GetInputText()))
                        {
                            var newValue = double.Parse(numberInput.GetInputText());
                            ModHelper.Config.SetSettingsValue(name, newValue);
                            ModHelper.Storage.Save(ModHelper.Config, Constants.ModConfigFileName);
                            Configure(ModHelper.Config);
                            numberInput.SetText(newValue.ToString());
                        }
                    };
                    break;
                default:
                    WriteDebugMessage($"Couldn't generate input for unkown input type {settingType}");
                    OptionsMenuManager.CreateLabel(newModTab, $"Unknown {settingType} : {name}");
                    break;
            }
        }


        if (newModTab._tooltipDisplay != null)
        {
            foreach (MenuOption option in newModTab.GetComponentsInChildren<MenuOption>(true))
            {
                option.SetTooltipDisplay(newModTab._tooltipDisplay);
            }
        }

        bool foundSelectable = false;
        newModTab._listSelectables = newModTab.GetComponentsInChildren<Selectable>(true);
        foreach (Selectable selectable in newModTab._listSelectables)
        {
            selectable.gameObject.GetAddComponent<Menu.MenuSelectHandler>().OnSelectableSelected += newModTab.OnMenuItemSelected;

            if (newModTab._lastSelected != null
                && selectable.gameObject.name == newModTab._lastSelected.gameObject.name)
            {
                SelectableAudioPlayer component = newModTab._selectOnActivate.GetComponent<SelectableAudioPlayer>();
                if (component != null)
                {
                    component.SilenceNextSelectEvent();
                }
                Locator.GetMenuInputModule().SelectOnNextUpdate(selectable);
                foundSelectable = true;
            }
        }

        if (!foundSelectable && newModTab._selectOnActivate != null)
        {
            SelectableAudioPlayer component = newModTab._selectOnActivate.GetComponent<SelectableAudioPlayer>();
            if (component != null)
            {
                component.SilenceNextSelectEvent();
            }
            Locator.GetMenuInputModule().SelectOnNextUpdate(newModTab._selectOnActivate);
            newModTab._lastSelected = newModTab._selectOnActivate;
        }

        if (newModTab._setMenuNavigationOnActivate)
        {
            Menu.SetVerticalNavigation(newModTab, newModTab._menuOptions);
        }

        ModHelper.Events.Unity.FireInNUpdates(() =>
        {
            scrollbar.value = lastScrollValue;
        }, 2);
    }

    private bool DestroyExistingSettings(Menu menu, Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            if (i < 2)
            {
                MenuOption option = parent.GetChild(i).GetComponentInChildren<MenuOption>();
                if (option != null)
                {
                    menu._menuOptions = menu._menuOptions.Add(option);
                }
            }
            else
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        return true;
    }

    private bool ShouldHideSetting(int currIndex, string name)
    {
        if (name == "spaceTimer" && (string)settings["spaceAccelerationType"].value != "Timed")
        {
            return true;
        }
        if (name == "spaceSpeed" && (string)settings["spaceAccelerationType"].value == "Timed")
        {
            return true;
        }
        return false;
    }

    private bool SetCustomSettingName(ref string label, string settingName)
    {
        return false;
    }

    private bool SetCustomTooltip(ref string tooltip, string settingName)
    {
        if (settingName == "spaceAccelerationType")
        {
            string value = (string)settings["spaceAccelerationType"].value;
            if (value == "Cumulative")
            {
                tooltip = "Cumulative means Ernesto will accelerate towards you faster and faster as time goes on. You can sometimes outrun him, and he may frequently miss his target.";
            }
            else if (value == "Linear")
            {
                tooltip = "Linear means Ernesto will move at a constant speed towards you, except it's impossible to outrun him. He will always be getting closer.";
            }
            else if (value == "Timed")
            {
                tooltip = "Timed means Ernesto will reach you in a set amount of time, no matter how far away you are. This is the most balanced type.";
            }
            return true;
        }
        if (settingName == "spaceSpeed")
        {
            string value = (string)settings["spaceAccelerationType"].value;
            if (value == "Cumulative")
            {
                tooltip = "This changes how quickly Ernesto accelerates towards you in space.";
            }
            else
            {
                tooltip = "This changes how quickly Ernesto moves towards you in space.";
            }
            return true;
        }

        return false;
    }

    private SettingType GetSettingType(object setting)
    {
        var settingObject = setting as JObject;

        if (setting is bool || (settingObject != null && settingObject["type"].ToString() == "toggle" && (settingObject["yes"] == null || settingObject["no"] == null)))
        {
            return SettingType.CHECKBOX;
        }
        else if (setting is string || (settingObject != null && settingObject["type"].ToString() == "text"))
        {
            return SettingType.TEXT;
        }
        else if (setting is int || setting is long || setting is float || setting is double || setting is decimal || (settingObject != null && settingObject["type"].ToString() == "number"))
        {
            return SettingType.NUMBER;
        }
        else if (settingObject != null && settingObject["type"].ToString() == "toggle")
        {
            return SettingType.TOGGLE;
        }
        else if (settingObject != null && settingObject["type"].ToString() == "selector")
        {
            return SettingType.SELECTOR;
        }
        else if (settingObject != null && settingObject["type"].ToString() == "slider")
        {
            return SettingType.SLIDER;
        }
        else if (settingObject != null && settingObject["type"].ToString() == "separator")
        {
            return SettingType.SEPARATOR;
        }

        WriteDebugMessage($"Couldn't work out setting type. Type:{setting.GetType().Name} SettingObjectType:{settingObject?["type"].ToString()}");
        return SettingType.NONE;
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

    enum SettingType
    {
        NONE,
        CHECKBOX,
        TOGGLE,
        TEXT,
        NUMBER,
        SELECTOR,
        SLIDER,
        SEPARATOR
    }
}
