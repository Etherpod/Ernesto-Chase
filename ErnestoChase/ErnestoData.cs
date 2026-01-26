using System;

[Serializable]
public class ErnestoData
{
    public uint id = 0;
    public uint localid = 0;
    public float time = 0;
    public float MovementSpeed;
    public string SpeedAccumulationType;
    public float SpeedAccumulationRate;
    public float DistanceSpeedMultiplier;
    public string SpaceAccelerationType;
    public float SpaceSpeed;
    public float SpaceTimer;
    public float BrambleSpeedMultiplier;
    public float DreamWorldSpeedMultiplier;
    public float StartDelay;
    public bool StealthMode;
    public bool QuantumMode;
    public bool ErnestoCam;
    public bool ErnestoMusic;
    public bool DisableLight;
};
