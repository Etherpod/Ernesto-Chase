using System;
using System.Collections.Generic;
using System.Linq;

namespace ErnestoChase;

[Serializable]
public enum Minigame
{
	None,
	Survival,
	RandomShipLog
}

[Serializable]
public enum RandomShipLogMode
{
	Planet,
	Entry,
	Fact,
	Rumor
}

public static class GameStateManager
{
	private static Minigame _selectedMinigame;
	private static readonly List<RandomShipLogMode> _selectedShipLogModes = [];
	
	public static bool StartingGame
	{
		get => GetCondition("EC_STARTING_GAME");
		set => PlayerData.SetPersistentCondition("EC_STARTING_GAME", value);
	}
	
	public static bool GameStarted
	{
		get => GetCondition("EC_GAME_STARTED");
		set => PlayerData.SetPersistentCondition("EC_GAME_STARTED", value);
	}

	public static void LoadSaveData(SaveDataJson saveData)
	{
		_selectedMinigame = saveData.SelectedMinigame;
		_selectedShipLogModes.Clear();
		_selectedShipLogModes.AddRange(saveData.SelectedShipLogModes);
	}

	public static void SelectMinigame(Minigame minigame)
	{
		_selectedMinigame = minigame;
	}
	
	public static Minigame GetSelectedMinigame() => _selectedMinigame;

	private static readonly Dictionary<string, RandomShipLogMode> NameToShipLogMode = new()
	{
		{ "Planet Mode", RandomShipLogMode.Planet },
		{ "Entry Mode", RandomShipLogMode.Entry },
		{ "Fact Mode", RandomShipLogMode.Fact },
		{ "Rumor Mode", RandomShipLogMode.Rumor }
	};

	private static bool GetCondition(string name) => PlayerData.PersistentConditionExists(name) &&
		PlayerData.GetPersistentCondition(name);

	public static void SetShipLogMode(string name, bool value)
	{
		if (NameToShipLogMode.ContainsKey(name))
		{
			var mode = NameToShipLogMode[name]; 
			if (value && !_selectedShipLogModes.Contains(mode))
			{
				_selectedShipLogModes.Add(mode);
			}
			else if (!value && _selectedShipLogModes.Contains(mode))
			{
				_selectedShipLogModes.Remove(mode);
			}
		}
	}
	
	public static bool GetShipLogMode(string name)
	{
		return NameToShipLogMode.ContainsKey(name) && 
			_selectedShipLogModes.Contains(NameToShipLogMode[name]);
	}

	public static RandomShipLogMode[] GetSelectedShipLogModes() => 
		_selectedShipLogModes.ToArray();

	public static void Reset()
	{
		StartingGame = false;
		GameStarted = false;
	}
}