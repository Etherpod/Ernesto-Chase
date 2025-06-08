using UnityEngine;

namespace ErnestoChase;

public class ErnestoState : MonoBehaviour
{
    public bool ErnestoReleased { get; set; } = false;
    public Vector3 LastPlayerPos { get; set; } = Vector3.zero;
    public bool FollowedPlayerToPlanet { get; set; } = true;
    public bool KillVolumeEnabled { get; set; } = false;
}
