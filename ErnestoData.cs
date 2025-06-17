using System;

[Serializable]
public record ErnestoData(
    uint id,
    uint localid,
    float MovementSpeed,
    string SpaceAccelerationType,
    float SpaceSpeed,
    float SpaceTimer,
    float BrambleSpeedMultiplier,
    float DreamWorldSpeedMultiplier,
    float StartDelay,
    bool StealthMode,
    bool QuantumMode,
    bool ErnestoCam,
    bool ErnestoMusic
);
