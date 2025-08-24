using Newtonsoft.Json;
using System;
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
        public float time = targetData.time;

        public readonly TargetData TargetData => new(parent, localPosition.Vector, worldPosition.Vector, time);
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
    }

    private static void OnPlayerJoin(uint id)
    {
        //ErnestoChase.Instance.StartCoroutine(ErnestoChase.Instance.AddCamToRemotePlayer(id));
    }

    public static void SendErnestoData(uint to, ErnestoData data)
    {
        string json = JsonConvert.SerializeObject(data);
        api.SendMessage("ernesto-data", json, to, false);
    }

    private static void ReceiveErnestoData(uint from, string json)
    {
        ErnestoData data = JsonConvert.DeserializeObject<ErnestoData>(json);
        if (data != null)
        {
            ErnestoChase.WriteDebugMessage("Receive Ernesto on " + api.GetLocalPlayerID() + " - " + data.id);
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
}
