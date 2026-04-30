using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System.Reflection;
using UnityEngine;
using ErnestoChase;
using QSB.Player;
using System.Linq;
using QSB.Player.TransformSync;
using ErnestoChase.Interaction;
using QSB.SectorSync.WorldObjects;
using QSB.WorldSync;

namespace ErnestoChaseQSB;

public class QSBInteraction : MonoBehaviour, IQSBInteraction
{
    public void Start()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        ErnestoChase.ErnestoChase.Instance.SetQSBInterface(this);
    }

    public GameObject GetRemoteFluidDetector(uint id)
    {
        var player = QSBPlayerManager.PlayerList.First(x => x.PlayerId == id);
        return player.FluidDetector.gameObject;
    }

    public bool GetPlayerInCloak(uint id)
    {
        var player = QSBPlayerManager.PlayerList.First(x => x.PlayerId == id);
        return player.IsInCloak;
    }

    public bool GetPlayerInDream(uint id)
    {
        var player = QSBPlayerManager.PlayerList.First(x => x.PlayerId == id);
        return player.InDreamWorld;
    }

    public bool GetLocalPlayerReady()
    {
        return PlayerTransformSync.LocalInstance != null;
    }

    public int SectorToID(Sector sector)
    {
        return sector.TryGetWorldObject(out QSBSector obj) ? obj.ObjectId : 0;
    }

    public Sector IDToSector(int id)
    {
        return id.GetWorldObject<QSBSector>()?.AttachedObject;
    }
}