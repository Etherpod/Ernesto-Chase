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
    public OWRigidbody ernestoBody;
    public bool playerDetectorReady = false;
    public List<GameObject> ernestos = [];
    public List<GameObject> oldErnestos = [];
    public List<TargetDataQueue> storedErnestoTargets = [];

    public float MovementSpeed => (float)settings["movementSpeed"].property;
    public float SpaceSpeed => (float)settings["spaceSpeed"].property;
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

    private Dictionary<string, (object value, object property)> settings = new()
    {
        { "randomMode", (false, false) },
        { "movementSpeed", (1f, 1f) },
        { "spaceSpeed", (1f, 1f) },
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
    };

    private Dictionary<string, object> randomSettings = new()
    {
        { "movementSpeed", new object[] { 3f, 10f } },
        { "spaceSpeed", new object[] { 3f, 10f } },
        { "brambleSpeedMultiplier", new object[] { 1f, 5f } },
        { "dreamWorldSpeedMultiplier", new object[] { 0.2f, 1.2f } },
        { "startDelay", new object[] { 10f, 60f } },
        { "spaceAccelerationType", new object[] { "Cumulative", "Linear", "Linear", "Timed", "Timed" } },
        { "spaceTimer", new object[] { 30f, 120f } },
        { "enableStealthMode", new object[] { false, false, true } },
        { "enableQuantumMode", new object[] { false, false, true } },
        { "customEndScreen", new object[] { false, true } },
        { "ernestoCam", new object[] { false, true } },
        { "ernestoNumber", new object[] { 1f, 3f } },
        { "ernestoStacking", new object[] { false, true } },
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

        LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
        {
            if (loadScene != OWScene.SolarSystem) return;

            playerDetectorReady = false;
            ernestos.Clear();
            oldErnestos.Clear();
            ernestoBody = null;
            PatchnestoClass.Initialize();

            UpdateProperties();

            StartCoroutine(WaitForPlayer());
        };

        LoadManager.OnStartSceneLoad += (scene, loadScene) =>
        {
            if (scene != OWScene.SolarSystem || loadScene != OWScene.SolarSystem) return;

            if (!ErnestoStacking)
            {
                storedErnestoTargets.Clear();
                return;
            }

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
        for (int i = 0; i < keys.Length; i++)
        {
            if (randomMode && randomSettings.ContainsKey(keys[i]) && randomSettings[keys[i]] is object[] list)
            {
                if (list[0] is float)
                {
                    settings[keys[i]] = (settings[keys[i]].value, UnityEngine.Random.Range((float)list[0], (float)list[1]));
                }
                else if (list[0] is bool)
                {
                    settings[keys[i]] = (settings[keys[i]].value, UnityEngine.Random.value > 0.5f);
                }
                else if (list[0] is string)
                {
                    int randIndex = UnityEngine.Random.Range(0, list.Length);
                    settings[keys[i]] = (settings[keys[i]].value, list[randIndex]);
                }
            }
            else
            {
                settings[keys[i]] = (settings[keys[i]].value, settings[keys[i]].value);
            }
        }
        WriteDebugMessage(MovementSpeed);
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
            GameObject ernestoObj = LoadPrefab("Assets/ErnestoChase/Ernesto.prefab");
            AssetBundleUtilities.ReplaceShaders(ernestoObj);
            if (!ErnestoCam)
            {
                ernestoObj.GetComponentInChildren<ErnestoCamera>().gameObject.SetActive(false);
            }
            ErnestoManager ernesto = Instantiate(ernestoObj, Locator.GetPlayerTransform().position, Quaternion.identity).GetComponent<ErnestoManager>();
            ernesto.IncrementSpawnDelay(i);
            ernestos.Add(ernesto.gameObject);

            if (ErnestoStacking && storedErnestoTargets.Count > i)
            {
                ernesto.SetStoredTargets(storedErnestoTargets[i]);
                oldErnestos.Add(ernesto.gameObject);
            }
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

            // this line keeps throwing an NRE, surely this will fix it
            if (selectable?.gameObject?.name == newModTab?._lastSelected?.gameObject?.name)
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
