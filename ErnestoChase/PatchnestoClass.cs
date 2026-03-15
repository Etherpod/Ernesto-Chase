using UnityEngine;
using HarmonyLib;
using OWML.ModHelper.Menus.NewMenuSystem;
using OWML.Common;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using ErnestoChase.ErnestoAI;
using ErnestoChase.Spectating;
using ErnestoChase.PlayerErnesto;

namespace ErnestoChase;

[HarmonyPatch]
public static class PatchnestoClass
{
	private static readonly List<ErnestoManager> caughtErnestos = [];

	public static void Initialize()
	{
		caughtErnestos.Clear();
		ernestoCamIndex = 0;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(ForceDetector), nameof(ForceDetector.AddVolume))]
	public static void OnAddGravityVolume(AlignmentForceDetector __instance)
	{
		if (!__instance.CompareTag("PlayerDetector") || !TimeLoop.IsTimeFlowing())
		{
			return;
		}

		ErnestoChase.Instance.ModHelper.Events.Unity.FireOnNextUpdate(() =>
		{
			ErnestoChase.Instance.playerDetectorReady = true;
		});
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(OWRigidbody), nameof(OWRigidbody.SetPosition))]
	public static void DetectPlayerWarp(OWRigidbody __instance, Vector3 worldPosition)
	{
		bool flag = (__instance.CompareTag("Player") && !PlayerState.IsInsideShip()) ||
			(__instance.CompareTag("Ship") && PlayerState.IsInsideShip()) ||
			(__instance.CompareTag("ShipCockpit") && PlayerState.AtFlightConsole() && PlayerState.IsAttached()) ||
			(__instance is ControllableErnestoBody && PlayerState.IsAttached());

		if (!flag || !ErnestoChase.Instance.playerDetectorReady || !TimeLoop.IsTimeFlowing())
		{
			return;
		}

		if ((worldPosition - Locator.GetPlayerTransform().position).sqrMagnitude > 50f * 50f)
		{
			ErnestoChase.WriteDebugMessage("Player warped");
			ErnestoChase.Instance.OnPlayerWarpedEvent();
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(DreamWorldController), nameof(DreamWorldController.ExitDreamWorld), [typeof(DeathType)])]
	public static bool ExitDreamWorld(DreamWorldController __instance, DeathType deathType)
	{
		if (deathType != DeathType.Digestion || caughtErnestos.Count == 0 || !TimeLoop.IsTimeFlowing())
		{
			return true;
		}

		DeathManager deathManager = Locator.GetDeathManager();
		deathManager._isDying = true;
		deathManager._deathType = deathType;
		MonoBehaviour.print("Player was killed by " + deathType);
		Locator.GetPauseCommandListener().AddPauseCommandLock();
		PlayerData.SetLastDeathType(deathType);
		GlobalMessenger<DeathType>.FireEvent("PlayerDeath", deathType);
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(DeathManager), nameof(DeathManager.KillPlayer))]
	public static void OverwriteInvincibility(DeathManager __instance)
	{
		if (caughtErnestos.Count > 0 || GameStateManager.StartingGame)
		{
			__instance._invincible = false;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(DeathManager), nameof(DeathManager.KillPlayer))]
	public static void CheckPlayerDied(DeathManager __instance, bool __runOriginal)
	{
		if (!__runOriginal)
		{
			ErnestoChase.Instance.RespawnErnesto();
		}

		if (Locator.GetPlayerBody().GetComponentInChildren<PlayerMorphController>()?.IsMorphed() ?? false)
		{
			__instance._invincible = true;
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(ToolModeSwapper), nameof(ToolModeSwapper.EquipToolMode))]
	public static bool CancelToolEquip()
	{
		return !(Locator.GetPlayerBody()
			.GetComponentInChildren<PlayerMorphController>()?.IsMorphed() 
			?? false);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Flashback), nameof(Flashback.OnTriggerFlashback))]
	public static bool ErnestoEndScreen(Flashback __instance)
	{
		if (caughtErnestos.Count == 0 || !ErnestoChase.Instance.CustomEndScreen)
		{
			return true;
		}

		GameOverController controller = __instance.GetComponent<GameOverController>();
		controller._deathText.text = "ERNESTO BEAT YOU TO DEATH WITH A ROCK.";
		controller.SetupGameOverScreen(5f);

		return false;
	}

	public static void OnCaughtPlayer(ErnestoManager ernesto)
	{
		caughtErnestos.Add(ernesto);
	}

	private static int ernestoCamIndex = 0;

	[HarmonyPostfix]
	[HarmonyPatch(typeof(ProbeLauncher), nameof(ProbeLauncher.UpdatePreLaunch))]
	public static void ErnestoCam(ProbeLauncher __instance)
	{
		if (ErnestoChase.Instance.camErnestos.Count == 0 || (__instance.GetName() != ProbeLauncher.Name.Player
			&& __instance.GetName() != ProbeLauncher.Name.Ship))
		{
			return;
		}

		if (__instance.InPhotoMode())
		{
			if (OWInput.IsNewlyPressed(InputLibrary.toolActionSecondary))
			{
				DrawErnestoCam(__instance);
			}

			if (ErnestoChase.Instance.camErnestos.Count > 1)
			{
				if (OWInput.IsNewlyPressed(InputLibrary.toolOptionUp))
				{
					ernestoCamIndex++;
					if (ernestoCamIndex >= ErnestoChase.Instance.camErnestos.Count)
					{
						ernestoCamIndex = 0;
					}

					DrawErnestoCam(__instance);
				}
				else if (OWInput.IsNewlyPressed(InputLibrary.toolOptionDown))
				{
					ernestoCamIndex--;
					if (ernestoCamIndex < 0)
					{
						ernestoCamIndex = ErnestoChase.Instance.camErnestos.Count - 1;
					}

					DrawErnestoCam(__instance);
				}
			}
		}
	}

	public static void DrawErnestoCam(ProbeLauncher __instance)
	{
		RenderTexture renderTexture = ErnestoChase.Instance.camErnestos[ernestoCamIndex]
			.GetComponentInChildren<ErnestoCamera>().TakeSnapshot();
		__instance._lastSnapshot = renderTexture;
		__instance._effects.PlaySnapshotClip(true);

		ProbeLauncherUI ui;
		if (PlayerState.AtFlightConsole())
		{
			ui = Locator.GetShipBody().GetComponentInChildren<ShipCockpitUI>()
				.GetComponentInChildren<ProbeLauncherUI>();
		}
		else
		{
			ui = GameObject.Find("PlayerHUD").GetComponentInChildren<ProbeLauncherUI>();
		}

		if (ui._probeLauncher.GetName() == ProbeLauncher.Name.Player)
		{
			if (ui._nonSuitUI)
			{
				if (Locator.GetPlayerSuit().IsWearingSuit(true))
				{
					return;
				}

				ProbeLauncherUI.s_removeSnapshopPrompt.SetVisibility(true);
				ui.ActivateUI();
			}
			else if (!Locator.GetPlayerSuit().IsWearingSuit(true))
			{
				return;
			}
		}

		ui._snapshotTime = Time.time;
		if (ui._canvas != null)
		{
			ui._canvas.enabled = true;
		}

		ui._image.enabled = true;
		ui._image.material.SetTexture("_MainTex", ui._rearSnapshotOverlay);
		ui._image.material.SetTexture("_MainTex", renderTexture);
		ui._image.SetMaterialDirty();
	}

	public static bool ignoreNextMenuActivation = false;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu), nameof(Menu.Activate))]
	public static void RefreshMenuOnActivate(Menu __instance)
	{
		MenuManager menuManager = StartupPopupPatches.menuManager;
		IOptionsMenuManager OptionsMenuManager = menuManager.OptionsMenuManager;

		var menus = typeof(MenuManager).GetField("ModSettingsMenus", BindingFlags.Public
					| BindingFlags.NonPublic | BindingFlags.Static)
				?.GetValue(menuManager)
			as List<(IModBehaviour behaviour, Menu modMenu)>;

		for (int i = 0; i < menus.Count; i++)
		{
			if ((object)menus[i].behaviour == ErnestoChase.Instance
				&& menus[i].modMenu == __instance)
			{
				if (ignoreNextMenuActivation)
				{
					return;
				}

				__instance.OnActivateMenu += () => ErnestoChase.Instance.ModHelper.Events.Unity
					.FireOnNextUpdate(() => ECMenuManager.RedrawSettingsMenu());
				return;
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(MenuManager), nameof(MenuManager.ForceModOptionsOpen))]
	public static void PreventRepeatActivation(MenuManager __instance, bool force)
	{
		var menus = typeof(MenuManager).GetField("ModSettingsMenus", BindingFlags.Public
					| BindingFlags.NonPublic | BindingFlags.Static)
				?.GetValue(__instance)
			as List<(IModBehaviour behaviour, Menu modMenu)>;

		for (int i = 0; i < menus.Count; i++)
		{
			if ((object)menus[i].behaviour == ErnestoChase.Instance)
			{
				ignoreNextMenuActivation = force;
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(OWML.ModHelper.Menus.NewMenuSystem.Patches),
		nameof(OWML.ModHelper.Menus.NewMenuSystem.Patches.ResetToDefaultSettings))]
	public static void FixNumberValueReset()
	{
		MenuManager menuManager = StartupPopupPatches.menuManager;
		var menus = typeof(MenuManager).GetField("ModSettingsMenus", BindingFlags.Public
					| BindingFlags.NonPublic | BindingFlags.Static)?
				.GetValue(menuManager)
			as List<(IModBehaviour behaviour, Menu modMenu)>;

		Menu modMenu = null;
		for (int i = 0; i < menus.Count; i++)
		{
			if ((object)menus[i].behaviour == ErnestoChase.Instance)
			{
				modMenu = menus[i].modMenu;
			}
		}

		if (modMenu == null || !modMenu.IsMenuEnabled()) return;

		var settings = ErnestoChase.Instance.ModHelper.Config.Settings;
		var defaultSettings = ErnestoChase.Instance.ModHelper.DefaultConfig;
		var options = modMenu.GetMenuOptions().Where(x => x.name != "UIElement-GammaButton").ToArray();

		for (var i = 0; i < options.Length; i++)
		{
			var menuOption = options[i];

			if (menuOption.TryGetComponent(out IOWMLTextEntryElement textEntry))
			{
				textEntry.SetCurrentValue(defaultSettings.GetSettingsValue<double>(textEntry.ModSettingKey)
					.ToString(CultureInfo.InvariantCulture));
			}
		}

		//SettingExtensions.ResetCustomSettings();
	}
	
	[HarmonyPrefix]
	[HarmonyPatch(typeof(FirstPersonManipulator), nameof(FirstPersonManipulator.LateUpdate))]
	public static bool FixMorphRaycast(FirstPersonManipulator __instance)
	{
		var parent = __instance.GetComponentInParent<ControllableErnesto>();
		if (!parent) return true;

		var cam = parent.transform.Find("ScaleRoot/ErnestoCam");

		RaycastHit raycastHit;
		if (Physics.Raycast(cam.position, cam.forward, out raycastHit,
			75f, OWLayerMask.blockableInteractMask))
		{
			if (raycastHit.collider != __instance._lastHitCollider)
			{
				__instance._lastHitCollider = raycastHit.collider;
				if (__instance._observable != null)
				{
					__instance._observable.LoseFocus();
				}

				__instance._observable = __instance._lastHitCollider.GetComponent<IObservable>();
				if (__instance._interactReceiver != null)
				{
					__instance._interactReceiver.LoseFocus();
				}

				__instance._interactReceiver = __instance._lastHitCollider.GetComponent<IRaycastInteractable>();
				NomaiInterfaceOrb component = __instance._lastHitCollider.GetComponent<NomaiInterfaceOrb>();
				if (__instance._pendingOrb != null && component == null)
				{
					__instance._pendingOrb.OnLoseStartDragFocus();
				}

				__instance._pendingOrb = component;
			}
		}
		else
		{
			__instance._lastHitCollider = null;
			if (__instance._interactReceiver != null)
			{
				__instance._interactReceiver.LoseFocus();
				__instance._interactReceiver = null;
			}

			if (__instance._observable != null)
			{
				__instance._observable.LoseFocus();
				__instance._observable = null;
			}

			__instance._focusedRepairReceiver = null;
			__instance._focusedNomaiText = null;
			__instance._focusedItemSocket = null;
			__instance._focusedItem = null;
			if (__instance._pendingOrb != null)
			{
				__instance._pendingOrb.OnLoseStartDragFocus();
			}

			__instance._pendingOrb = null;
		}

		if (__instance._activeOrb != null)
		{
			RaycastHit raycastHit2;
			if (Physics.Raycast(cam.position, cam.forward,
				out raycastHit2, 75f, OWLayerMask.interactMask))
			{
				if (!__instance._activeOrb.UpdateDragFromPosition(cam.position, raycastHit2.point))
				{
					__instance._activeOrb.CancelDrag();
					__instance._activeOrb = null;
				}
			}
			else
			{
				__instance._activeOrb.CancelDrag();
				__instance._activeOrb = null;
			}
		}
		else if (__instance._pendingOrb != null && __instance._pendingOrb.StartDragFromPosition(cam.position))
		{
			__instance._activeOrb = __instance._pendingOrb;
		}

		if (__instance._observable != null)
		{
			__instance._observable.Observe(raycastHit, cam.position);
		}

		if (__instance._interactReceiver != null)
		{
			__instance._interactReceiver.Observe(raycastHit);
		}

		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PlayerSpawner), nameof(PlayerSpawner.FixedUpdate))]
	public static void FixDebugWarpForErnestoMorph(PlayerSpawner __instance)
	{
		if (!__instance._debugWarpNextUpdate) return;

		var ernesto = Locator.GetPlayerBody()?.GetComponentInParent<ControllableErnesto>();
		if (ernesto)
		{
			__instance._debugWarpNextUpdate = false;
			OWRigidbody owrigidbody = ernesto.GetComponent<OWRigidbody>();
			owrigidbody.WarpToPositionRotation(__instance._debugWarpPoint.transform.position,
				__instance._debugWarpPoint.transform.rotation);
			owrigidbody.SetVelocity(__instance._debugWarpPoint.GetPointVelocity());
			if (owrigidbody == __instance._playerBody)
			{
				__instance._debugWarpPoint.AddObjectToTriggerVolumes(Locator.GetPlayerDetector().gameObject);
				__instance._debugWarpPoint.AddObjectToTriggerVolumes(Locator.GetPlayerCamera()
					.GetComponentInChildren<FluidDetector>().gameObject);
				__instance._debugWarpPoint.OnSpawnPlayer();
				MonoBehaviour.print("DEBUG PLAYER WARP");
			}
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(DreamWorldController), nameof(DreamWorldController.ApplySunOverrides))]
	public static bool ForceSunDisableInSpectate(DreamWorldController __instance,
		SunLightController.SunOverrideSettings settings,
		ref SunLightController.SunOverrideSettings __result)
	{
		if (ErnestoChase.SpectateManager.IsSpectating && ErnestoChase.SpectateManager.loadedDreamWorld)
		{
			settings.sunIntensity = 0f;
			settings.ambientIntensity = 0f;
			settings.sunShadowStrength = 0f;
			__result = settings;
			return false;
		}

		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(CloakFieldController), nameof(CloakFieldController.OnSectorOccupantsUpdated))]
	public static bool PreventCloakExitInSpectate(CloakFieldController __instance)
	{
		return !ErnestoChase.SpectateManager.IsSpectating;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(DreamWarpVolume), nameof(DreamWarpVolume.OnEnterTriggerVolume))]
	public static bool PreventEnterDreamWarpInSpectate(GameObject hitObj)
	{
		return !hitObj.GetComponentInParent<SpectatorCamera>();
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(DreamWarpVolume), nameof(DreamWarpVolume.OnExitTriggerVolume))]
	public static bool PreventExitDreamWarpInSpectate(GameObject hitObj)
	{
		return !hitObj.GetComponentInParent<SpectatorCamera>();
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(DeathManager), nameof(DeathManager.CheckShouldWakeInDreamWorld))]
	public static bool PreventDreamRevival(ref bool __result)
	{
		if (caughtErnestos.Count > 0 || GameStateManager.StartingGame)
		{
			__result = false;
			return false;
		}

		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(DialogueNode), nameof(DialogueNode.EntryConditionsSatisfied))]
	public static bool DialogueEntryConditionsSatisfied(DialogueNode __instance, ref bool __result)
	{
		if (ErnestoChase.Instance.ModHelper.Interaction.ModExists("JohnCorby.VanillaFix") ||
			ErnestoChase.Instance.ModHelper.Interaction.ModExists("Etherpod.ShipEnhancements"))
		{
			return true;
		}

		bool flag = true;
		if (__instance._listEntryCondition.Count == 0)
		{
			__result = false;
			return false;
		}

		DialogueConditionManager sharedInstance = DialogueConditionManager.SharedInstance;
		for (int i = 0; i < __instance._listEntryCondition.Count; i++)
		{
			string text = __instance._listEntryCondition[i];
			// CHANGED: remove the !
			if (PlayerData.PersistentConditionExists(text))
			{
				if (!PlayerData.GetPersistentCondition(text))
				{
					flag = false;
				}
			}
			else if (sharedInstance.ConditionExists(text))
			{
				if (!sharedInstance.GetConditionState(text))
				{
					flag = false;
				}
			}
			else
			{
				flag = false;
			}
		}

		__result = flag;
		return false;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(DialogueConditionManager), nameof(DialogueConditionManager.ReadPlayerData))]
	public static void SetNonHostCondition(DialogueConditionManager __instance)
	{
		bool state = ErnestoChase.InMultiplayer && !ErnestoChase.QSBAPI.GetIsHost();
		ErnestoChase.WriteDebugMessage("non host: " + state);
		if (!__instance.AddCondition("EC_NON_HOST", state))
		{
			__instance.SetConditionState("EC_NON_HOST", state);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(CharacterDialogueTree), nameof(CharacterDialogueTree.InputDialogueOption))]
	public static bool ForceConditionChange(CharacterDialogueTree __instance, int optionIndex, ref bool __result)
	{
		if (optionIndex < 0) return true;
		
		var option = __instance._currentDialogueBox.OptionFromUIIndex(optionIndex);
		var stem = option._textID.Contains("SetupEC_RSSR_Configure_Remote")
			? "SetupEC_RSSR_Configure_Remote"
			: "SetupEC_RSSR_Configure";
		
		if (option._textID.Contains(stem) && option._textID.Contains(" - "))
		{
			string text = option._textID.Replace(stem, "");
			string name = text.Substring(0, text.IndexOf(" - "));
			bool state = GameStateManager.GetShipLogMode(name);
			GameStateManager.SetShipLogMode(name, !state);

			__instance._currentDialogueBox._optionsUIElements[optionIndex]
				.textElement.text = option.Text;
			__result = true;
			return false;
		}

		return true;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(CharacterDialogueTree), nameof(CharacterDialogueTree.ContinueToNextNode), typeof(DialogueOption))]
	public static void UpdateConditions(CharacterDialogueTree __instance)
	{
		ErnestoChase.Instance.OnInputDialogueOption(__instance);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(ShipLogController), nameof(ShipLogController.SetDamaged))]
	public static bool PreventShipLogEnable(bool damaged)
	{
		return GameStateManager.GetSelectedMinigame() != Minigame.RandomShipLog ||
			damaged || ErnestoChase.Instance.AllowShipLog;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(TextTranslation), nameof(TextTranslation.Translate))]
	public static void TextTranslation_Translate(TextTranslation __instance, string key, ref string __result)
	{
		var stem = key.Contains("SetupEC_RSSR_Configure_Remote")
			? "SetupEC_RSSR_Configure_Remote"
			: "SetupEC_RSSR_Configure";
		
		if (key.Contains(stem))
		{
			string text = key.Replace(stem, "");
			if (key.IndexOf(" - ") < 0) return;
			bool state = GameStateManager.GetShipLogMode(text.Substring(0, text.IndexOf(" - ")));

			__result = __result.Replace("STATE", state ? "Enabled" : "Disabled");
			return;
		}
		
		if (key.Contains("EC_SelectMinigame") || key.Contains("EC_SelectMinigame_Selected"))
		{
			string minigame;
			if (GameStateManager.GetSelectedMinigame() == Minigame.RandomShipLog)
			{
				minigame = "Random Ship Log";
			}
			else if (GameStateManager.GetSelectedMinigame() == Minigame.Survival)
			{
				minigame = "Survival";
			}
			else
			{
				minigame = "NONE";
			}

			__result = __result.Replace("CURRENT_MINIGAME", minigame);
		}
	}
}
