using UnityEngine;

namespace ErnestoChase;

public interface IQSBInteraction
{
    public GameObject GetRemoteFluidDetector(uint id);

    public bool GetPlayerInCloak(uint id);

    public bool GetPlayerInDream(uint id);

    public bool GetLocalPlayerReady();
}
