using System.Collections.Generic;
using System.Linq;

namespace ErnestoChase;

public static class ErnestoConditionManager
{
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
	
	public static bool RandomShipLogEnabled
	{
		get => GetCondition("EC_RSSR_MODE");
		set
		{
			if (value)
			{
				ChangeMinigame("EC_RSSR_MODE");
			}
			else
			{
				PlayerData.SetPersistentCondition("EC_RSSR_MODE", false);
			}
		}
	}

	public static bool SurvivalEnabled
	{
		get => GetCondition("EC_SURVIVAL_MODE");
		set
		{
			if (value)
			{
				ChangeMinigame("EC_SURVIVAL_MODE");
			}
			else
			{
				PlayerData.SetPersistentCondition("EC_SURVIVAL_MODE", false);
			}
		}
	}

	public static bool ShipLogFactMode
	{
		get => GetCondition(ShipLogModes[0]);
		set => SetShipLogMode(0, value);
	}
	
	public static bool ShipLogEntryMode
	{
		get => GetCondition(ShipLogModes[1]);
		set => SetShipLogMode(1, value);
	}
	
	public static bool ShipLogPlanetMode
	{
		get => GetCondition(ShipLogModes[2]);
		set => SetShipLogMode(2, value);
	}
	
	public static bool ShipLogRumorMode
	{
		get => GetCondition(ShipLogModes[3]);
		set => SetShipLogMode(3, value);
	}

	public static readonly string[] Minigames =
	[
		"EC_RSSR_MODE",
		"EC_SURVIVAL_MODE"
	];

	public static readonly string[] ShipLogModes =
	[
		"EC_RSSR_FACT_MODE",
		"EC_RSSR_ENTRY_MODE",
		"EC_RSSR_PLANET_MODE",
		"EC_RSSR_RUMOR_MODE"
	];

	private static bool GetCondition(string name) => PlayerData.PersistentConditionExists(name) &&
		PlayerData.GetPersistentCondition(name);

	private static void ChangeMinigame(string newName)
	{
		foreach (var name in Minigames)
		{
			PlayerData.SetPersistentCondition(name, false);
		}
		
		PlayerData.SetPersistentCondition(newName, true);
	}
	
	public static void DeselectMinigame()
	{
		foreach (var name in Minigames)
		{
			PlayerData.SetPersistentCondition(name, false);
		}
	}

	private static void SetShipLogMode(int mode, bool state)
	{
		SetShipLogMode(ShipLogModes[mode], state);
	}

	public static void SetShipLogMode(string mode, bool state)
	{
		if (state || ShipLogModes.Where(name => name != mode).Any(GetCondition))
		{
			PlayerData.SetPersistentCondition(mode, state);
		}
	}

	public static void Reset()
	{
		StartingGame = false;
		GameStarted = false;

		if (ShipLogModes.All(mode => !GetCondition(mode)))
		{
			SetShipLogMode(0, true);
		}
	}
}