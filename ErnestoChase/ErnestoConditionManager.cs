using System.Collections.Generic;

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

	public static readonly string[] Minigames =
	[
		"EC_RSSR_MODE",
		"EC_SURVIVAL_MODE"
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

	public static void Reset()
	{
		StartingGame = false;
		GameStarted = false;
	}

	public static void DeselectMinigame()
	{
		foreach (var name in Minigames)
		{
			PlayerData.SetPersistentCondition(name, false);
		}
	}
}