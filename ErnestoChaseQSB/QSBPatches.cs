using UnityEngine;
using QSB.RespawnSync;
using HarmonyLib;
using System.Collections.Generic;
using QSB.Player;
using QSB.DeathSync;
using QSB.Player.TransformSync;
using QSB.ShipSync;
using QSB.Localization;
using ErnestoChase;
using OWML.Common;
using QSB;
using QSB.DeathSync.Patches;
using QSB.Messaging;
using QSB.Player.Messages;
using QSB.Utility;

namespace ErnestoChaseQSB;

[HarmonyPatch]
public class QSBPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RespawnManager), nameof(RespawnManager.OnPlayerDeath))]
    public static void KeepDeadPlayersEmtpy(List<PlayerInfo> ____playersPendingRespawn)
    {
        ____playersPendingRespawn.Clear();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SectorStreaming), nameof(SectorStreaming.FixedUpdate))]
    public static bool SectorStreaming_FixedUpdate(SectorStreaming __instance)
    {
        if (ErnestoChase.ErnestoChase.SpectateManager == null ||
            !ErnestoChase.ErnestoChase.SpectateManager.IsSpectating || 
            ErnestoChase.ErnestoChase.SpectateManager.SpectateTarget == null) return true;

        var playerInSoftRadius =
            (ErnestoChase.ErnestoChase.SpectateManager.SpectateTarget.transform.position 
            - __instance._sector.transform.position).sqrMagnitude < __instance._softLoadRadius * __instance._softLoadRadius;

        if (PlayerState.OnQuantumMoon() && Locator.GetQuantumMoon().IsPlayerInsideShrine() 
            && __instance._sector.GetName() != Sector.Name.QuantumMoon)
        {
            playerInSoftRadius = false;
        }

        if (!__instance._playerInSoftLoadRadius && playerInSoftRadius)
        {
            __instance._streamingGroup.RequestRequiredAssets(0);
        }
        else if (__instance._playerInSoftLoadRadius && !playerInSoftRadius)
        {
            __instance._streamingGroup.ReleaseRequiredAssets();
        }

        if (__instance._probeInSoftLoadRadius)
        {
            __instance._streamingGroup.ReleaseRequiredAssets();
        }

        __instance._playerInSoftLoadRadius = playerInSoftRadius;
        __instance._probeInSoftLoadRadius = false;

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ShipLODTrigger), nameof(ShipLODTrigger.FixedUpdate))]
    public static bool ShipLODTrigger_FixedUpdate(ShipLODTrigger __instance)
    {
        if (ErnestoChase.ErnestoChase.SpectateManager == null ||
            !ErnestoChase.ErnestoChase.SpectateManager.IsSpectating || 
            ErnestoChase.ErnestoChase.SpectateManager.SpectateTarget == null) return true;

        var playerInRadius = __instance._playerInRadius;
        var probeInRadius = __instance._probeInRadius;

        if (__instance._playerTransform != null)
        {
            __instance._playerInRadius =
                Vector3.SqrMagnitude(ErnestoChase.ErnestoChase.SpectateManager.SpectateTarget.transform.position 
                - __instance._playerTransform.position) < __instance._radius * __instance._radius;
        }

        if (__instance._probeTransform != null)
        {
            __instance._probeInRadius =
                __instance._probe != null
                && __instance._probe.IsLaunched()
                && Vector3.SqrMagnitude(__instance._transform.position - __instance._probeTransform.position) 
                < __instance._radius * __instance._radius;
        }

        if (playerInRadius != __instance._playerInRadius
            || probeInRadius != __instance._probeInRadius)
        {
            __instance.OnTriggerUpdated.Invoke();

        }

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RespawnOnDeath), "OnGUI")]
    public static bool ReplaceDeathLabel(GUIStyle ____deadTextStyle)
    {
        if (PlayerTransformSync.LocalInstance == null || ShipManager.Instance.ShipCockpitUI == null)
        {
            return true;
        }

        if (QSBPlayerManager.LocalPlayer.IsDead)
        {
            GUI.contentColor = Color.white;

            var width = 200;
            var height = 100;

            // it is good day to be not dead

            var secondText = "Spectate players or wait until next loop to respawn";

            GUI.Label(
                new Rect((Screen.width / 2) - (width / 2), (Screen.height / 2) - (height / 2) + (height * 2), width, height),
                $"{QSBLocalization.Current.YouAreDead}\n{secondText}",
                ____deadTextStyle);
        }

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerJoinMessage), nameof(PlayerJoinMessage.OnReceiveRemote))]
    public static bool PreventJoinIfGameStarted(PlayerJoinMessage __instance)
    {
        if (QSBCore.IsHost && GameStateManager.GameStarted)
        {
            DebugLog.ToConsole($"Error - Ernesto Chase game has started already!", MessageType.Error);
            new PlayerKickMessage(__instance.From,
                    "A game of Ernesto Chase has already started!\nPlease wait for the game to end before joining.")
                .Send();
            return false;
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapPatches), nameof(MapPatches.MapController_EnterMapView))]
    public static void AdjustRevealTime(MapController __0)
    {
        __0._revealLength = 5f;
    }
}