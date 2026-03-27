using UnityEngine;

namespace ErnestoChase.Interaction;

public interface IQSBInteraction
{
    public GameObject GetRemoteFluidDetector(uint id);

    public bool GetPlayerInCloak(uint id);

    public bool GetPlayerInDream(uint id);

    public bool GetLocalPlayerReady();

    public int SectorToID(Sector sector);

    public Sector IDToSector(int id);
}
