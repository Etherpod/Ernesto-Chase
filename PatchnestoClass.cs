using System;
using UnityEngine;
using HarmonyLib;

namespace ErnestoChase;

[HarmonyPatch]
public static class PatchnestoClass
{
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
    public static void OnWarpPlayer(OWRigidbody __instance, Vector3 worldPosition)
    {
        bool flag = __instance.CompareTag("Player") || (__instance.CompareTag("Ship") && PlayerState.IsInsideShip() && ErnestoChase.Instance.inFogWarp);
        if (!flag || !ErnestoChase.Instance.playerDetectorReady || !TimeLoop.IsTimeFlowing())
        {
            //ErnestoChase.WriteDebugMessage("In fog warp: " + ErnestoChase.Instance.inFogWarp);
            return;
        }
        ErnestoChase.Instance.inFogWarp = false;
        if ((worldPosition - Locator.GetPlayerTransform().position).magnitude > 50f)
        {
            ErnestoChase.Instance.ernesto.OnWarpPlayer();
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DreamWorldController), nameof(DreamWorldController.ExitDreamWorld), [typeof(DeathType)])]
    public static bool ExitDreamWorld(DreamWorldController __instance, DeathType deathType)
    {
        if (deathType != DeathType.Digestion || !ErnestoChase.Instance.caughtPlayer || !TimeLoop.IsTimeFlowing())
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
    [HarmonyPatch(typeof(DeathManager), nameof(DeathManager.FinishDeathSequence))]
    public static bool AddErnestoEndScreen()
    {
        if (!ErnestoChase.Instance.caughtPlayer)
        {
            return true;
        }

        ErnestoChase.Instance.TriggerEndScreen();

        return false;
    }
}
