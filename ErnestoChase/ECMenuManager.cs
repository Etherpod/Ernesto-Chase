using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using OWML.Common;
using UnityEngine;
using OWML.ModHelper.Menus.NewMenuSystem;
using OWML.ModHelper;
using OWML.Utils;
using UnityEngine.UI;
using static ErnestoChase.ErnestoChase;

namespace ErnestoChase;

public static class ECMenuManager
{
	private static readonly Dictionary<string, string> _customObjLabels = [];

	private static readonly IModHelper ModHelper = Instance.ModHelper;

	private static bool _detectValueChanged = true;
	
	private static List<string> GetErnestoSettings()
	{
		int start = ModHelper.Config.Settings.Keys.ToList()
			.IndexOf("groundMovementSpeed");
		int end = ModHelper.Config.Settings.Keys.ToList()
			.IndexOf("advancedSettings");

		var range = ModHelper.Config.Settings.Keys.ToList()
			.GetRange(start, end - start + 1);
		return range;
	}

	private static void OnValueChanged(string name, object oldValue, object newValue)
	{
		if (!_detectValueChanged)
		{
			return;
		}

		bool valueChanged = !oldValue.Equals(newValue);

		if (name == "spaceAccelerationType" && valueChanged)
		{
			var val = (string)newValue;
			if (val == "Timed")
			{
				RedrawSettingsMenu("spaceTimer", "spaceTimer", 
					"spaceMovementSpeed", "spaceMovementSpeed");
			}
			else
			{
				RedrawSettingsMenu("spaceMovementSpeed", "spaceMovementSpeed", 
					"spaceTimer", "spaceTimer");
			}
		}

		if (name == "advancedSettings" && valueChanged)
		{
			if ((bool)newValue)
			{
				RedrawSettingsMenu("advancedSettings", "advancedSettings", 
					"groundMovementSpeed", "advancedSettings");
			}
			else
			{
				RedrawSettingsMenu("groundMovementSpeed", "advancedSettings", 
					"advancedSettings", "advancedSettings");
			}
		}
	}

	public static void RedrawSettingsMenu(string startSetting = "", string endSetting = "",
		string startDestroySetting = "", string endDestroySetting = "")
	{
		if (startDestroySetting == "")
		{
			startDestroySetting = startSetting;
		}

		if (endDestroySetting == "")
		{
			endDestroySetting = endSetting;
		}

		MenuManager menuManager = StartupPopupPatches.menuManager;
		IOptionsMenuManager OptionsMenuManager = menuManager.OptionsMenuManager;

		var menus = typeof(MenuManager).GetField("ModSettingsMenus", BindingFlags.Public
				| BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(menuManager)
			as List<(IModBehaviour behaviour, Menu modMenu)>;

		if (menus == null) return;

		Menu newModTab = null;

		for (int i = 0; i < menus.Count; i++)
		{
			if ((object)menus[i].behaviour == Instance)
			{
				newModTab = menus[i].modMenu;
			}
		}

		if (newModTab == null) return;

		newModTab._menuOptions = [];

		Scrollbar scrollbar = newModTab.transform.Find("Scroll View/Scrollbar Vertical").GetComponent<Scrollbar>();
		float lastScrollValue = scrollbar.value;

		Transform settingsParent = newModTab.transform.Find("Scroll View/Viewport/Content");

		if (!DestroyExistingSettings(newModTab, settingsParent, startDestroySetting, endDestroySetting,
			out int insertionIndex))
		{
			return;
		}

		_detectValueChanged = false;

		if (startSetting == "")
		{
			OptionsMenuManager.AddSeparator(newModTab, true);
			OptionsMenuManager.CreateLabel(newModTab, "Any changes to the settings are applied on the next loop!");
			//OptionsMenuManager.AddSeparator(newModTab, true);
		}

		int startIndex = 0;
		int endIndex = ModHelper.Config.Settings.Count - 1;
		if (startSetting != "")
		{
			startIndex = ModHelper.Config.Settings.Keys.ToList().IndexOf(startSetting);
		}

		if (endSetting != "")
		{
			endIndex = ModHelper.Config.Settings.Keys.ToList().IndexOf(endSetting);
		}

		Dictionary<int, string> cachedNames = [];

		for (int i = startIndex; i < ModHelper.Config.Settings.Count; i++)
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
					if (!SetCustomSettingName(settingsParent, ref label, ref cachedNames, name))
					{
						label = ModHelper.MenuTranslations.GetLocalizedString(settingObject["title"].ToString());

						if (_customObjLabels.ContainsKey(name))
						{
							string old = _customObjLabels[name];
							for (int c = 0; c < settingsParent.childCount; c++)
							{
								if (settingsParent.GetChild(c).name == "UIElement-" + old)
								{
									var id = settingsParent.GetChild(c).GetInstanceID();
									if (!cachedNames.ContainsKey(id))
									{
										cachedNames.Add(id, "UIElement-" + label);
									}
								}
							}

							_customObjLabels[name] = label;
						}
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

			if (endSetting != "" && i > endIndex)
			{
				for (int j = 0; j < settingsParent.childCount; j++)
				{
					if (settingsParent.GetChild(j).name == "UIElement-" + label)
					{
						MenuOption option = settingsParent.GetChild(j).GetComponentInChildren<MenuOption>();
						if (option != null)
						{
							newModTab._menuOptions = newModTab._menuOptions.Add(option);
						}
					}
				}

				continue;
			}

			switch (settingType)
			{
				case SettingType.CHECKBOX:
					var currentCheckboxValue = ModHelper.Config.GetSettingsValue<bool>(name);
					var settingCheckbox =
						OptionsMenuManager.AddCheckboxInput(newModTab, label, tooltip, currentCheckboxValue);
					settingCheckbox.ModSettingKey = name;
					settingCheckbox.OnValueChanged += (bool newValue) =>
					{
						var oldValue = ModHelper.Config.GetSettingsValue<bool>(name);
						ModHelper.Config.SetSettingsValue(name, newValue);
						ModHelper.Storage.Save(ModHelper.Config, Constants.ModConfigFileName);
						Instance.Configure(ModHelper.Config);
						OnValueChanged(name, oldValue, newValue);
					};
					break;
				case SettingType.TOGGLE:
					var currentToggleValue = ModHelper.Config.GetSettingsValue<bool>(name);
					var yes = settingObject["yes"].ToString();
					var no = settingObject["no"].ToString();
					var settingToggle =
						OptionsMenuManager.AddToggleInput(newModTab, label, yes, no, tooltip, currentToggleValue);
					settingToggle.ModSettingKey = name;
					settingToggle.OnValueChanged += (bool newValue) =>
					{
						var oldValue = ModHelper.Config.GetSettingsValue<bool>(name);
						ModHelper.Config.SetSettingsValue(name, newValue);
						ModHelper.Storage.Save(ModHelper.Config, Constants.ModConfigFileName);
						Instance.Configure(ModHelper.Config);
						OnValueChanged(name, oldValue, newValue);
					};
					break;
				case SettingType.SELECTOR:
					var currentSelectorValue = ModHelper.Config.GetSettingsValue<string>(name);
					var options = settingObject["options"].ToArray().Select(x => x.ToString()).ToArray();
					var currentSelectedIndex = Array.IndexOf(options, currentSelectorValue);
					var settingSelector = OptionsMenuManager.AddSelectorInput(newModTab, label, options, tooltip, true,
						currentSelectedIndex);
					settingSelector.ModSettingKey = name;
					settingSelector.OnValueChanged += (int newIndex, string newSelection) =>
					{
						var oldValue = ModHelper.Config.GetSettingsValue<string>(name);
						ModHelper.Config.SetSettingsValue(name, newSelection);
						ModHelper.Storage.Save(ModHelper.Config, Constants.ModConfigFileName);
						Instance.Configure(ModHelper.Config);
						OnValueChanged(name, oldValue, newSelection);
					};
					break;
				case SettingType.SEPARATOR:
					if (settingObject["separatorType"] != null)
					{
						if (settingObject["separatorType"].ToString() == "line")
						{
							OptionsMenuManager.AddSeparator(newModTab, true);
						}
						else if (settingObject["separatorType"].ToString() == "sidebar")
						{
							CreateSideLabel(newModTab, label);
						}
						else if (settingObject["separatorType"].ToString() == "header")
						{
							OptionsMenuManager.AddSeparator(newModTab, true);
							OptionsMenuManager.CreateLabel(newModTab, label);
							//OptionsMenuManager.AddSeparator(newModTab, false);
						}
					}
					else
					{
						OptionsMenuManager.AddSeparator(newModTab, false);
					}

					break;
				case SettingType.SLIDER:
					var currentSliderValue = ModHelper.Config.GetSettingsValue<float>(name);
					var lower = settingObject["min"].ToObject<float>();
					var upper = settingObject["max"].ToObject<float>();
					var settingSlider =
						OptionsMenuManager.AddSliderInput(newModTab, label, lower, upper, tooltip, currentSliderValue);
					settingSlider.ModSettingKey = name;
					settingSlider.OnValueChanged += (float newValue) =>
					{
						var oldValue = ModHelper.Config.GetSettingsValue<float>(name);
						ModHelper.Config.SetSettingsValue(name, newValue);
						ModHelper.Storage.Save(ModHelper.Config, Constants.ModConfigFileName);
						Instance.Configure(ModHelper.Config);
						OnValueChanged(name, oldValue, newValue);
					};
					break;
				case SettingType.TEXT:
					var currentTextValue = ModHelper.Config.GetSettingsValue<string>(name);
					var textInput =
						OptionsMenuManager.AddTextEntryInput(newModTab, label, currentTextValue, tooltip, false);
					textInput.ModSettingKey = name;
					textInput.OnConfirmEntry += () =>
					{
						var oldValue = ModHelper.Config.GetSettingsValue<string>(name);
						var newValue = textInput.GetInputText();
						ModHelper.Config.SetSettingsValue(name, newValue);
						ModHelper.Storage.Save(ModHelper.Config, Constants.ModConfigFileName);
						Instance.Configure(ModHelper.Config);
						textInput.SetText(newValue);
						OnValueChanged(name, oldValue, newValue);
					};
					break;
				case SettingType.NUMBER:
					var currentValue = ModHelper.Config.GetSettingsValue<double>(name);
					var numberInput = OptionsMenuManager.AddTextEntryInput(newModTab, label,
						currentValue.ToString(CultureInfo.CurrentCulture), tooltip, true);
					numberInput.ModSettingKey = name;
					numberInput.OnConfirmEntry += () =>
					{
						if (!string.IsNullOrEmpty(numberInput.GetInputText()))
						{
							var oldValue = ModHelper.Config.GetSettingsValue<double>(name);
							var newValue = double.Parse(numberInput.GetInputText());
							ModHelper.Config.SetSettingsValue(name, newValue);
							ModHelper.Storage.Save(ModHelper.Config, Constants.ModConfigFileName);
							Instance.Configure(ModHelper.Config);
							numberInput.SetText(newValue.ToString());
							OnValueChanged(name, oldValue, newValue);
						}
					};
					break;
				default:
					WriteDebugMessage($"Couldn't generate input for unkown input type {settingType}");
					OptionsMenuManager.CreateLabel(newModTab, $"Unknown {settingType} : {name}");
					break;
			}

			if (startSetting != "")
			{
				if (insertionIndex >= 0)
				{
					var addedSetting = settingsParent.GetChild(settingsParent.childCount - 1);
					addedSetting.SetSiblingIndex(insertionIndex);
					insertionIndex++;

					/*if (GetDecorationSettings().Contains(name) && ShouldSplitDecoration(name))
					{
						OptionsMenuManager.AddSeparator(newModTab, false);
						var sep = settingsParent.GetChild(settingsParent.childCount - 1);
						sep.name = "UIElement-" + label;
						sep.SetSiblingIndex(insertionIndex);
						insertionIndex++;
					}*/
				}
			}
			/*else
			{
				if (GetDecorationSettings().Contains(name) && ShouldSplitDecoration(name))
				{
					OptionsMenuManager.AddSeparator(newModTab, false);
				}
			}*/
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
			selectable.gameObject.GetAddComponent<Menu.MenuSelectHandler>().OnSelectableSelected +=
				newModTab.OnMenuItemSelected;

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

		ModHelper.Events.Unity.FireInNUpdates(() => { scrollbar.value = lastScrollValue; }, 2);

		ModHelper.Events.Unity.FireInNUpdates(() => { _detectValueChanged = true; }, 5);
	}

	private static void CreateSideLabel(Menu menu, string label)
	{
		var newObj = new GameObject("Label");

		var layoutElement = newObj.AddComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1;

		var verticalLayout = newObj.AddComponent<VerticalLayoutGroup>();
		verticalLayout.padding = new RectOffset(20, 180, 0, 0);
		verticalLayout.spacing = 0;
		verticalLayout.childAlignment = TextAnchor.MiddleLeft;
		verticalLayout.childForceExpandHeight = false;
		verticalLayout.childForceExpandWidth = false;
		verticalLayout.childControlHeight = true;
		verticalLayout.childControlWidth = true;
		verticalLayout.childScaleHeight = false;
		verticalLayout.childScaleWidth = false;

		var textObj = new GameObject("Text");

		var text = textObj.AddComponent<Text>();
		text.text = label;
		text.font = Resources.Load<Font>("fonts/english - latin/Adobe - SerifGothicStd");
		text.fontSize = 36;
		text.alignment = TextAnchor.MiddleLeft;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Truncate;

		var textLayoutElement = textObj.AddComponent<LayoutElement>();
		textLayoutElement.minHeight = 70;

		textObj.transform.parent = newObj.transform;
		textObj.transform.localScale = Vector3.one;
		textObj.transform.localPosition = Vector3.zero;
		textObj.transform.localRotation = Quaternion.identity;

		var parent = menu.transform;

		if (menu.transform.Find("Scroll View") != null)
		{
			parent = menu.transform.Find("Scroll View").Find("Viewport").Find("Content");
		}

		if (menu.transform.Find("Content") != null)
		{
			parent = menu.transform.Find("Content");
		}

		newObj.transform.parent = parent;
		newObj.transform.localScale = Vector3.one;
		newObj.transform.localPosition = Vector3.zero;
		newObj.transform.localRotation = Quaternion.identity;
	}

	private static bool DestroyExistingSettings(Menu menu, Transform parent, string startSetting, string endSetting,
		out int insertionIndex)
	{
		bool hasStart = startSetting != "";
		bool hasEnd = endSetting != "";
		if (hasStart || hasEnd)
		{
			string startTitle = "";
			if (hasStart)
			{
				var setting = ModHelper.Config.Settings[startSetting] as JObject;
				if (setting != default(JObject) && setting["title"] != null)
				{
					if (_customObjLabels.ContainsKey(startSetting))
					{
						startTitle = _customObjLabels[startSetting];
					}
					else
					{
						startTitle = setting["title"].ToString();
					}
				}
			}

			string endTitle = "";
			if (hasEnd)
			{
				var setting = ModHelper.Config.Settings[endSetting] as JObject;
				if (setting != default(JObject) && setting["title"] != null)
				{
					if (_customObjLabels.ContainsKey(endSetting))
					{
						endTitle = _customObjLabels[endSetting];
					}
					else
					{
						endTitle = setting["title"].ToString();
					}
				}
			}

			int startIndex = -1;
			int endIndex = -1;
			for (int i = 0; i < parent.childCount; i++)
			{
				if (parent.GetChild(i).name == "UIElement-" + startTitle)
				{
					startIndex = i;
				}

				if (startIndex < 0)
				{
					MenuOption option = parent.GetChild(i).GetComponentInChildren<MenuOption>();
					if (option != null)
					{
						menu._menuOptions = menu._menuOptions.Add(option);
					}

					continue;
				}

				if (parent.GetChild(i).name == "UIElement-" + endTitle)
				{
					endIndex = i;
				}

				UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);

				if (endIndex > 0)
				{
					insertionIndex = startIndex;
					return startIndex >= 0;
				}
			}

			insertionIndex = startIndex;
			return startIndex >= 0;
		}

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
				UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
			}
		}

		insertionIndex = -1;
		return true;
	}

	private static bool ShouldHideSetting(int currIndex, string name)
	{
		if (name == "spaceTimer" && (string)Instance.settings["spaceAccelerationType"].value != "Timed")
		{
			return true;
		}
		
		if (name == "spaceMovementSpeed" && (string)Instance.settings["spaceAccelerationType"].value == "Timed")
		{
			return true;
		}
		
		if (name != "advancedSettings" && (bool)Instance.settings["advancedSettings"].value 
			&& GetErnestoSettings().Contains(name))
		{
			return true;
		}

		if (name == "globalFactGoals" && !InMultiplayer)
		{
			return true;
		}

		return false;
	}

	private static bool SetCustomSettingName(Transform settingsParent, ref string label,
		ref Dictionary<int, string> cachedNames, string settingName)
	{
		return false;
	}

	private static bool SetCustomTooltip(ref string tooltip, string settingName)
	{
		if (settingName == "spaceAccelerationType")
		{
			string value = (string)Instance.settings["spaceAccelerationType"].value;
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
			string value = (string)Instance.settings["spaceAccelerationType"].value;
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

		if (settingName == "survivalTimerLength")
		{
			tooltip = InMultiplayer 
				? "Sets the number of minutes you need to survive for in order to win.\nOnly the host can change this." 
				: "Sets the number of minutes you need to survive for in order to win.";
			return true;
		}
		
		return false;
	}

	private static SettingType GetSettingType(object setting)
	{
		var settingObject = setting as JObject;

		if (setting is bool || (settingObject != null && settingObject["type"].ToString() == "toggle" &&
			(settingObject["yes"] == null || settingObject["no"] == null)))
		{
			return SettingType.CHECKBOX;
		}
		else if (setting is string || (settingObject != null && settingObject["type"].ToString() == "text"))
		{
			return SettingType.TEXT;
		}
		else if (setting is int || setting is long || setting is float || setting is double || setting is decimal ||
			(settingObject != null && settingObject["type"].ToString() == "number"))
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

		WriteDebugMessage(
			$"Couldn't work out setting type. Type:{setting.GetType().Name} SettingObjectType:{settingObject?["type"].ToString()}");
		return SettingType.NONE;
	}

	private enum SettingType
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