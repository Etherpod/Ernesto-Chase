using UnityEngine;
using QSB.RespawnSync;
using HarmonyLib;
using System.Collections.Generic;
using QSB.Player;

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
}
