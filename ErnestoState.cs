using System.Collections.Generic;
using UnityEngine;

namespace ErnestoChase;

public class ErnestoState : MonoBehaviour
{
    // Stats
    public float MovementSpeed { get; set; }
    public string SpaceAccelerationType { get; set; }
    public float SpaceSpeed { get; set; }
    public float SpaceTimer { get; set; }
    public float BrambleSpeedMultiplier { get; set; }
    public float DreamWorldSpeedMultiplier { get; set; }
    public bool StealthMode { get; set; }
    public bool QuantumMode { get; set; }
    public bool ErnestoCam { get; set; }

    // Data
    public bool ErnestoReleased { get; set; } = false;
    public Vector3 LastPlayerPos { get; set; } = Vector3.zero;
    public bool FollowedPlayerToPlanet { get; set; } = true;
    public bool KillVolumeEnabled { get; set; } = false;
    public bool CaughtPlayer { get; set; } = false;

    public void InitializeStats(Dictionary<string, (object value, object property)> settings)
    {
        MovementSpeed = (float)settings["movementSpeed"].property;
        SpaceAccelerationType = (string)settings["spaceAccelerationType"].property;
        SpaceSpeed = (float)settings["spaceSpeed"].property;
        SpaceTimer = (float)settings["spaceTimer"].property;
        BrambleSpeedMultiplier = (float)settings["brambleSpeedMultiplier"].property;
        DreamWorldSpeedMultiplier = (float)settings["dreamWorldSpeedMultiplier"].property;
        StealthMode = (bool)settings["enableStealthMode"].property;
        QuantumMode = (bool)settings["enableQuantumMode"].property;
        ErnestoCam = (bool)settings["ernestoCam"].property;
    }
}
