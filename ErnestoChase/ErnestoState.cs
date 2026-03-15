using System.Collections.Generic;
using UnityEngine;

namespace ErnestoChase;

public class ErnestoState : MonoBehaviour
{
    public delegate void DataChangedEvent(uint lastData);
    public event DataChangedEvent OnDataChanged;
    
    public uint RemoteID => DataStates[ActiveStateID].id;
    public uint LocalID => DataStates[ActiveStateID].localid;
    public float TimeOffset
    {
        get => DataStates[ActiveStateID].time;
        set => DataStates[ActiveStateID].time = value;
    }

    public readonly Dictionary<uint, ErnestoData> DataStates = [];
    public uint ActiveStateID { get; set; } = 1;

    // Stats
    public float MovementSpeedMultiplier => DataStates[ActiveStateID].MovementSpeed;
    public string SpeedAccumulationType => DataStates[ActiveStateID].SpeedAccumulationType;
    public float SpeedAccumulationRate => DataStates[ActiveStateID].SpeedAccumulationRate;
    public float DistanceSpeedMultiplier => DataStates[ActiveStateID].DistanceSpeedMultiplier;
    public string SpaceAccelerationType => DataStates[ActiveStateID].SpaceAccelerationType;
    public float SpaceSpeedMultiplier => DataStates[ActiveStateID].SpaceSpeed;
    public float SpaceTimer => DataStates[ActiveStateID].SpaceTimer;
    public float BrambleSpeedMultiplier => DataStates[ActiveStateID].BrambleSpeedMultiplier;
    public float DreamWorldSpeedMultiplier => DataStates[ActiveStateID].DreamWorldSpeedMultiplier;
    public float StartDelay => DataStates[ActiveStateID].StartDelay;
    public bool StealthMode => DataStates[ActiveStateID].StealthMode;
    public bool QuantumMode => DataStates[ActiveStateID].QuantumMode;
    public bool ErnestoCam => DataStates[ActiveStateID].ErnestoCam;
    public bool ErnestoMusic => DataStates[ActiveStateID].ErnestoMusic;
    public bool DisableLight => DataStates[ActiveStateID].DisableLight;

    // Data
    public bool ErnestoReleased { get; set; } = false;
    public bool AIEnabled { get; set; } = true;
    public bool UsingStoredTargets { get; set; } = false;
    public Vector3 LastPlayerPos { get; set; } = Vector3.zero;
    public bool FollowedPlayerToPlanet { get; set; } = true;
    public bool KillVolumeEnabled { get; set; } = false;
    public bool CaughtPlayer { get; set; } = false;

    public void InitializeStats(uint stateID, Dictionary<string, (object value, object property)> settings)
    {
        var data = new ErnestoData();
        
        //data.id = ErnestoChase.InMultiplayer ? ErnestoChase.QSBAPI.GetLocalPlayerID() : 0;
        data.id = 0;
        data.remoteid = ErnestoChase.InMultiplayer ? ErnestoChase.QSBAPI.GetLocalPlayerID() : 0;
        data.time = Time.fixedTime;
        data.MovementSpeed = (float)settings["groundMovementSpeed"].property;
        data.SpeedAccumulationType = (string)settings["speedAccumulationType"].property;
        data.SpeedAccumulationRate = (float)settings["speedAccumulationRate"].property;
        data.DistanceSpeedMultiplier = (float)settings["distanceSpeedMultiplier"].property;
        data.SpaceAccelerationType = (string)settings["spaceAccelerationType"].property;
        data.SpaceSpeed = (float)settings["spaceMovementSpeed"].property;
        data.SpaceTimer = (float)settings["spaceTimer"].property;
        data.BrambleSpeedMultiplier = (float)settings["brambleSpeedMultiplier"].property;
        data.DreamWorldSpeedMultiplier = (float)settings["dreamWorldSpeedMultiplier"].property;
        data.StartDelay = (float)settings["startDelay"].property;
        data.StealthMode = (bool)settings["enableStealthMode"].property;
        data.QuantumMode = (bool)settings["enableQuantumMode"].property;
        data.ErnestoCam = (bool)settings["ernestoCam"].property;
        data.ErnestoMusic = (bool)settings["ernestoMusic"].property;
        data.DisableLight = (bool)settings["disableLight"].property;
        
        DataStates[stateID] = data;
    }

    public ErnestoData GetData(uint stateID)
    {
        return DataStates.ContainsKey(stateID) ? DataStates[stateID] : null;
    }

    public void SetRemoteData(uint stateID, ErnestoData data)
    {
        data.time -= Time.fixedTime;
        data.id = data.remoteid;
        DataStates[stateID] = data;
    }

    public void SetRemoteData(Dictionary<uint, ErnestoData> dataStates)
    {
        foreach (var (id, state) in dataStates)
        {
            state.time -= Time.fixedTime;
            state.id = state.remoteid;
            DataStates[id] = state;
        }
    }

    public void SetActiveStateID(uint stateID)
    {
        uint lastData = ActiveStateID;
        ActiveStateID = stateID;
        OnDataChanged?.Invoke(lastData);
    }
}
