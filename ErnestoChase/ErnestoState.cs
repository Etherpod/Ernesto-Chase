using System.Collections.Generic;
using UnityEngine;

namespace ErnestoChase;

public class ErnestoState : MonoBehaviour
{
    public uint RemoteID { get; set; } = 0;
    public uint LocalID { get; set; }
    public float TimeOffset { get; set; }

    // Stats
    public float MovementSpeedMultiplier { get; set; }
    public string SpeedAccumulationType { get; set; }
    public float SpeedAccumulationRate { get; set; }
    public float DistanceSpeedMultiplier { get; set; }
    public string SpaceAccelerationType { get; set; }
    public float SpaceSpeedMultiplier { get; set; }
    public float SpaceTimer { get; set; }
    public float BrambleSpeedMultiplier { get; set; }
    public float DreamWorldSpeedMultiplier { get; set; }
    public float StartDelay { get; set; }
    public bool StealthMode { get; set; }
    public bool QuantumMode { get; set; }
    public bool ErnestoCam { get; set; }
    public bool ErnestoMusic { get; set; }
    public bool DisableLight { get; set; }

    // Data
    public bool ErnestoReleased { get; set; } = false;
    public bool AIEnabled { get; set; } = true;
    public bool UsingStoredTargets { get; set; } = false;
    public Vector3 LastPlayerPos { get; set; } = Vector3.zero;
    public bool FollowedPlayerToPlanet { get; set; } = true;
    public bool KillVolumeEnabled { get; set; } = false;
    public bool CaughtPlayer { get; set; } = false;

    public void InitializeStats(Dictionary<string, (object value, object property)> settings)
    {
        MovementSpeedMultiplier = (float)settings["groundMovementSpeed"].property;
        SpeedAccumulationType = (string)settings["speedAccumulationType"].property;
        SpeedAccumulationRate = (float)settings["speedAccumulationRate"].property;
        DistanceSpeedMultiplier = (float)settings["distanceSpeedMultiplier"].property;
        SpaceAccelerationType = (string)settings["spaceAccelerationType"].property;
        SpaceSpeedMultiplier = (float)settings["spaceMovementSpeed"].property;
        SpaceTimer = (float)settings["spaceTimer"].property;
        BrambleSpeedMultiplier = (float)settings["brambleSpeedMultiplier"].property;
        DreamWorldSpeedMultiplier = (float)settings["dreamWorldSpeedMultiplier"].property;
        StartDelay = (float)settings["startDelay"].property;
        StealthMode = (bool)settings["enableStealthMode"].property;
        QuantumMode = (bool)settings["enableQuantumMode"].property;
        ErnestoCam = (bool)settings["ernestoCam"].property;
        ErnestoMusic = (bool)settings["ernestoMusic"].property;
        DisableLight = (bool)settings["disableLight"].property;
    }

    public ErnestoData GetData()
    {
        uint id = ErnestoChase.QSBAPI != null ? ErnestoChase.QSBAPI.GetLocalPlayerID() : 0;
        return new ErnestoData(id, LocalID, Time.fixedTime, MovementSpeedMultiplier, SpeedAccumulationType, SpeedAccumulationRate, 
            DistanceSpeedMultiplier, SpaceAccelerationType, SpaceSpeedMultiplier, SpaceTimer, BrambleSpeedMultiplier, 
            DreamWorldSpeedMultiplier, StartDelay, StealthMode, QuantumMode, ErnestoCam, ErnestoMusic);
    }

    public void SetData(ErnestoData data)
    {
        MovementSpeedMultiplier = data.MovementSpeed;
        SpeedAccumulationType = data.SpeedAccumulationType;
        SpeedAccumulationRate = data.SpeedAccumulationRate;
        DistanceSpeedMultiplier = data.DistanceSpeedMultiplier;
        SpaceAccelerationType = data.SpaceAccelerationType;
        SpaceSpeedMultiplier = data.SpaceSpeed;
        SpaceTimer = data.SpaceTimer;
        BrambleSpeedMultiplier = data.BrambleSpeedMultiplier;
        DreamWorldSpeedMultiplier = data.DreamWorldSpeedMultiplier;
        StartDelay = data.StartDelay;
        StealthMode = data.StealthMode;
        QuantumMode = data.QuantumMode;
        ErnestoCam = data.ErnestoCam;
        ErnestoMusic = data.ErnestoMusic;

        RemoteID = data.id;
        LocalID = data.localid;
        TimeOffset = data.time - Time.fixedTime;
    }
}
