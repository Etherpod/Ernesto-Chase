using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ErnestoChase.TargetDataQueue;

namespace ErnestoChase;

public static class QSBCompat
{
    private static IQSBAPI api;

    [Serializable]
    private struct SerializedVector3(Vector3 vector)
    {
        public float x = vector.x;
        public float y = vector.y;
        public float z = vector.z;

        public readonly Vector3 Vector => new(x, y, z);
    }

    [Serializable]
    private struct SerializedTargetData(TargetData targetData)
    {
        public string parent = targetData.parent;
        public SerializedVector3 localPosition = new(targetData.localPosition);
        public SerializedVector3 worldPosition = new(targetData.worldPosition);
        public SerializedVector3 worldUp = new(targetData.worldUp);
        public float time = targetData.time;
        public bool isTeleportEnter = targetData.isTeleportEnter;
        public bool isTeleportExit = targetData.isTeleportExit;
        public bool isFinalTarget = targetData.isFinalTarget;
        public bool isInRingWorld = targetData.isInRingWorld;

        public readonly TargetData TargetData => new(parent, localPosition.Vector, 
            worldPosition.Vector, worldUp.Vector, time, isTeleportEnter, isTeleportExit, 
            isFinalTarget, isInRingWorld);
    }

    public static void Init(IQSBAPI qsbapi)
    {
        api = qsbapi;

        api.OnPlayerJoin().AddListener(OnPlayerJoin);

        api.RegisterHandler<string>("ernesto-data", ReceiveErnestoData);
        api.RegisterHandler<string>("controlled-ernesto-data", ReceiveControlledErnestoData);
        api.RegisterHandler<(uint, SerializedTargetData)>("target-data", ReceiveTargetData);
        api.RegisterHandler<(uint, bool)>("visibility-state", ReceiveVisibilityState);
        api.RegisterHandler<(uint, bool)>("size-change", ReceiveErnestoSizeChange);
        api.RegisterHandler<uint>("final-warp", ReceiveErnestoFinalWarp);
        api.RegisterHandler<bool>("ring-world-state", ReceiveRingWorldUpdate);
        api.RegisterHandler<bool>("refresh-ring-world", ReceiveRingWorldRefresh);
        api.RegisterHandler<bool>("refresh-dream-world", ReceiveDreamWorldRefresh);
        api.RegisterHandler<bool>("start-game", ReceiveStartGame);
        api.RegisterHandler<bool>("stop-game", ReceiveStopGame);
        api.RegisterHandler<float>("survival-timer-setup", ReceiveSurvivalTimerSetup);
        api.RegisterHandler<float>("survival-timer-sync", ReceiveSurvivalTimerSync);
        api.RegisterHandler<string>("random-fact", ReceiveRandomShipLogFact);
        api.RegisterHandler<bool>("generate-random-fact", ReceiveRandomFactGenerate);
        api.RegisterHandler<bool>("win-random-ship-log", ReceiveShipLogWin);
    }

    private static void OnPlayerJoin(uint id)
    {
        //ErnestoChase.Instance.StartCoroutine(ErnestoChase.Instance.AddCamToRemotePlayer(id));
    }

    public static void SendErnestoData(uint to, Dictionary<uint, ErnestoData> dataStates)
    {
        string json = JsonConvert.SerializeObject(dataStates);
        ErnestoChase.WriteDebugMessage("sending data:\n" + json);
        api.SendMessage("ernesto-data", json, to, false);
    }

    private static void ReceiveErnestoData(uint from, string json)
    {
        Dictionary<uint, ErnestoData> data = JsonConvert.DeserializeObject<Dictionary<uint, ErnestoData>>(json);
        if (data != null)
        {
            ErnestoChase.WriteDebugMessage("Receive Ernesto on " + 
                api.GetLocalPlayerID() + " - " + data[data.Keys.First()].id);
            ErnestoChase.Instance.StartCoroutine(ErnestoChase.Instance.SpawnErnestoRemote(data));
        }
    }

    public static void SendControlledErnestoData(uint to, ErnestoData data)
    {
        string json = JsonConvert.SerializeObject(data);
        api.SendMessage("controlled-ernesto-data", json, to, false);
    }

    private static void ReceiveControlledErnestoData(uint from, string json)
    {
        ErnestoData data = JsonConvert.DeserializeObject<ErnestoData>(json);
        if (data != null)
        {
            ErnestoChase.WriteDebugMessage("Receive controlled Ernesto on " + api.GetLocalPlayerID() + " - " + data.id);
            ErnestoChase.Instance.StartCoroutine(ErnestoChase.Instance.SpawnControlledErnestoRemote(data));
        }
    }

    public static void SendTargetData(uint to, uint localID, TargetData targetData)
    {
        api.SendMessage("target-data", (localID, new SerializedTargetData(targetData)), to, false);
    }

    private static void ReceiveTargetData(uint from, (uint localID, SerializedTargetData targetData) data)
    {
        ErnestoChase.Instance.AddTargetDataRemote(from, data.localID, data.targetData.TargetData);
    }

    public static void SendVisibilityState(uint to, uint localID, bool visible)
    {
        api.SendMessage("visibility-state", (localID, visible), to, false);
    }

    private static void ReceiveVisibilityState(uint from, (uint localID, bool visible) data)
    {
        if (ErnestoChase.TryGetRemoteErnesto(0, data.localID, out GameObject remoteErnesto))
        {
            remoteErnesto.GetComponent<ErnestoMovement>().UpdateVisibilityRemote(from, data.visible);
        }
    }

    public static void SendErnestoSizeChange(uint to, uint localID, bool shrink)
    {
        api.SendMessage("size-change", (localID, shrink), to, false);
    }

    private static void ReceiveErnestoSizeChange(uint from, (uint localID, bool shrink) data)
    {
        if (ErnestoChase.TryGetRemoteErnesto(from, data.localID, out GameObject remoteErnesto))
        {
            remoteErnesto.GetComponent<RemoteSizeChanger>()?.SetSize(data.shrink);
        }
    }

    public static void SendErnestoFinalWarp(uint to, uint localID)
    {
        api.SendMessage("final-warp", localID, to, false);
    }

    private static void ReceiveErnestoFinalWarp(uint from, uint localID)
    {
        if (ErnestoChase.TryGetRemoteErnesto(from, localID, out GameObject remoteErnesto))
        {
            remoteErnesto.GetComponent<ErnestoManager>()?.OnPlayerDeathRemote();
        }
    }

    public static void SendRingWorldUpdate(uint to, bool state)
    {
        api.SendMessage("ring-world-state", state, to);
    }

    private static void ReceiveRingWorldUpdate(uint from, bool state)
    {
        ErnestoChase.Instance.UpdateRingWorldState(from, state);
    }

    public static void SendRingWorldRefresh(uint to)
    {
        api.SendMessage("refresh-ring-world", false, to);
    }

    private static void ReceiveRingWorldRefresh(uint from, bool b)
    {
        if (ErnestoChase.Instance.IsSpectating && !ErnestoChase.Instance.SpectateTarget.IsErnestoCam &&
            ErnestoChase.Instance.SpectateTarget.PlayerID == from)
        {
            ErnestoChase.Instance.SwitchToSpectatorCam(ErnestoChase.Instance.SpectateTarget);
        }
    }
    
    public static void SendDreamWorldRefresh(uint to)
    {
        api.SendMessage("refresh-dream-world", false, to);
    }

    private static void ReceiveDreamWorldRefresh(uint from, bool b)
    {
        if (ErnestoChase.Instance.IsSpectating && !ErnestoChase.Instance.SpectateTarget.IsErnestoCam &&
            ErnestoChase.Instance.SpectateTarget.PlayerID == from)
        {
            ErnestoChase.Instance.SwitchToSpectatorCam(ErnestoChase.Instance.SpectateTarget);
        }
    }

    public static void SendStartGame(uint to)
    {
        api.SendMessage("start-game", false, to);
    }

    private static void ReceiveStartGame(uint from, bool b)
    {
        ErnestoConditionManager.StartingGame = true;
        Locator.GetDeathManager().KillPlayer(DeathType.Meditation);
        DialogueConditionManager.SharedInstance.SetConditionState("EC_START_GAME", false);
        DialogueConditionManager.SharedInstance.SetConditionState("EC_GAME_STARTED", true);
    }
    
    public static void SendStopGame(uint to)
    {
        api.SendMessage("stop-game", false, to);
    }

    private static void ReceiveStopGame(uint from, bool b)
    {
        ErnestoChase.Instance.StopGameRemote();
    }

    public static void SendSurvivalTimerSetup(uint to, float timerLength)
    {
        api.SendMessage("survival-timer-setup", timerLength, to);
    }

    private static void ReceiveSurvivalTimerSetup(uint from, float timerLength)
    {
        ErnestoChase.MinigameManager.SetUpSurvivalRemote(timerLength);
    }
    
    public static void SendSurvivalTimerSync(uint to, float timeLeft)
    {
        api.SendMessage("survival-timer-sync", timeLeft, to);
    }

    private static void ReceiveSurvivalTimerSync(uint from, float timeLeft)
    {
        ErnestoChase.MinigameManager.SetUpSurvivalRemote(timeLeft);
    }

    public static void SendRandomShipLogFact(uint to, string factID)
    {
        api.SendMessage("random-fact", factID, to);
    }

    private static void ReceiveRandomShipLogFact(uint from, string factID)
    {
        ErnestoChase.MinigameManager.SetUpRandomShipLogRemote(factID);
    }
    
    public static void SendRandomFactGenerate(uint to)
    {
        api.SendMessage("generate-random-fact", false, to);
    }

    private static void ReceiveRandomFactGenerate(uint from, bool b)
    {
        ErnestoChase.MinigameManager.SetUpRandomShipLog();
    }

    public static void SendShipLogWin(uint to)
    {
        api.SendMessage("win-random-ship-log", false, to);
    }

    private static void ReceiveShipLogWin(uint from, bool b)
    {
        ErnestoChase.MinigameManager.WinRandomShipLogRemote();
    }
}
