using System;
using UnityEngine;
using HarmonyLib;
using OWML.ModHelper.Menus.NewMenuSystem;
using OWML.Common;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace ErnestoChase;

[HarmonyPatch]
public static class PatchnestoClass
{
    private static List<ErnestoManager> caughtErnestos = [];

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
        bool flag = (__instance.CompareTag("Player") && !PlayerState.IsInsideShip()) || (__instance.CompareTag("Ship") && PlayerState.IsInsideShip())
            || (__instance.CompareTag("ShipCockpit") && PlayerState.AtFlightConsole() && PlayerState.IsAttached());

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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DeathManager), nameof(DeathManager.KillPlayer))]
    public static void CheckPlayerDied(bool __runOriginal)
    {
        if (!__runOriginal)
        {
            ErnestoChase.Instance.RespawnErnesto();
        }
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
        RenderTexture renderTexture = ErnestoChase.Instance.camErnestos[ernestoCamIndex].GetComponentInChildren<ErnestoCamera>().TakeSnapshot();
        __instance._lastSnapshot = renderTexture;
        __instance._effects.PlaySnapshotClip(true);

        ProbeLauncherUI ui;
        if (PlayerState.AtFlightConsole())
        {
            ui = Locator.GetShipBody().GetComponentInChildren<ShipCockpitUI>().GetComponentInChildren<ProbeLauncherUI>();
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
            | BindingFlags.NonPublic | BindingFlags.Static).GetValue(menuManager)
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
                    .FireOnNextUpdate(() => ErnestoChase.Instance.RedrawSettingsMenu());
                return;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MenuManager), nameof(MenuManager.ForceModOptionsOpen))]
    public static void PreventRepeatActivation(MenuManager __instance, bool force)
    {
        var menus = typeof(MenuManager).GetField("ModSettingsMenus", BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.Static).GetValue(__instance)
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
            | BindingFlags.NonPublic | BindingFlags.Static).GetValue(menuManager)
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
                textEntry.SetCurrentValue(defaultSettings.GetSettingsValue<double>(textEntry.ModSettingKey).ToString(CultureInfo.InvariantCulture));
            }
        }

        //SettingExtensions.ResetCustomSettings();
    }
}
