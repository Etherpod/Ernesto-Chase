using UnityEngine;

namespace ErnestoChase.PlayerErnesto;

public class ErnestoForceDetector : PriorityDetector
{
    public override void AddVolume(EffectVolume eVol)
    {
        if (eVol as ForceVolume != null)
        {
            base.AddVolume(eVol);
        }
    }

    public override void RemoveVolume(EffectVolume eVol)
    {
        if (eVol as ForceVolume != null)
        {
            base.RemoveVolume(eVol);
        }
    }

    public override void OnVolumeActivated(PriorityVolume eVol)
    {
        if (eVol as ForceVolume != null)
        {
            base.OnVolumeActivated(eVol);
        }
    }

    public override void OnVolumeDeactivated(PriorityVolume eVol)
    {
        if (eVol as ForceVolume != null)
        {
            base.OnVolumeDeactivated(eVol);
        }
    }
}
