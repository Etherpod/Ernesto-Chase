using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using static ErnestoChase.ErnestoChase;
using Random = UnityEngine.Random;

namespace ErnestoChase;

public class MinigameManager : MonoBehaviour
{
	private readonly int _syncFrameDelay = 120;
	private int _frameDelay;
	private CountdownTimer _countdownTimer;
	private bool _syncingTimer;

	private ShipLogGameMode _currentShipLogMode;
	private ShipLogFact _selectedFact;
	private ShipLogEntry _selectedEntry;
	private ShipLogAstroObject _selectedPlanet;
	private RandomShipLogInfo _shipLogInfo;
	private int _numHintsUsed;
	private int _currentRound = 0;
	private int _currentRumorChain = 0;

	public enum ShipLogGameMode
	{
		Fact,
		Entry,
		Planet,
		Rumor
	}

	private void Awake()
	{
		GlobalMessenger.AddListener("WakeUp", OnWakeUp);
		GlobalMessenger<DeathType>.AddListener("PlayerDeath", OnPlayerDeath);
	}

	public void OnSceneUnloaded()
	{
		_countdownTimer = null;
		_syncingTimer = false;

		if (_selectedFact != null)
		{
			_selectedFact.OnFactRevealed -= OnFactRevealed;
			_selectedFact = null;
		}

		if (_selectedEntry != null)
		{
			_selectedEntry.GetExploreFacts()
				.ForEach(fact => fact.OnFactRevealed -= OnFactRevealed);
			_selectedEntry = null;
		}

		if (_selectedPlanet != null)
		{
			Locator.GetShipLogManager().GetEntriesByAstroBody(_selectedPlanet.GetID())
				.ForEach(entry => entry.GetExploreFacts()
					.ForEach(fact => fact.OnFactRevealed -= OnFactRevealed));
		}
		
		_shipLogInfo = null;
		_numHintsUsed = 0;
		_currentRound = 0;
		_currentRumorChain = 0;
	}

	private void OnWakeUp()
	{
		Instance.ModHelper.Events.Unity.FireInNUpdates(() =>
		{
			WriteDebugMessage("WAKE UP WAKE UP");
			if (_countdownTimer != null)
			{
				_countdownTimer.FadeIn(5f);
			}
			else if (_shipLogInfo != null)
			{
				WriteDebugMessage("gaba");
				_shipLogInfo.FadeIn(5f);
			}
		}, 10);
	}

	private void OnPlayerDeath(DeathType deathType)
	{
		if (_countdownTimer != null)
		{
			_countdownTimer.StopTimer();
			_countdownTimer.FadeOut(5f);
		}
		else if (_shipLogInfo != null)
		{
			_shipLogInfo.FadeOut(5f);
		}
	}
	
	public void SetUpMinigames()
	{
		if (ErnestoConditionManager.RandomShipLogEnabled &&
			(!InMultiplayer || QSBAPI.GetIsHost()))
		{
			SetUpRandomShipLog();
		}
		else if (ErnestoConditionManager.SurvivalEnabled &&
			(!InMultiplayer || QSBAPI.GetIsHost()))
		{
			SetUpSurvival();
		}
	}

	public void SetUpSurvival()
	{
		GameObject ui = LoadPrefab("Assets/ErnestoChase/CountdownHUD.prefab");
		_countdownTimer = Instantiate(ui).GetComponentInChildren<CountdownTimer>();
		_countdownTimer.SetTimerLength(Instance.SurvivalTimerLength);
		_countdownTimer.StartTimer();

		if (InMultiplayer)
		{
			_syncingTimer = true;
			foreach (var id in Players)
			{
				QSBCompat.SendSurvivalTimerSetup(id, Instance.SurvivalTimerLength);
			}
		}
	}
	
	public void SetUpSurvivalRemote(float timerLength)
	{
		ErnestoChase.WriteDebugMessage("Add UI remote");
		GameObject ui = LoadPrefab("Assets/ErnestoChase/CountdownHUD.prefab");
		_countdownTimer = Instantiate(ui).GetComponentInChildren<CountdownTimer>();
		_countdownTimer.SetTimerLength(timerLength);
		_countdownTimer.StartTimer();
	}

	public void UpdateSurvivalTimerRemote(float timeLeft)
	{
		_countdownTimer?.SetCurrentTime(timeLeft);
	}
	
	public void SetUpRandomShipLog()
	{
		if (!_shipLogInfo)
		{
			GameObject ui = LoadPrefab("Assets/ErnestoChase/RandomShipLogHUD.prefab");
			_shipLogInfo = Instantiate(ui).GetComponentInChildren<RandomShipLogInfo>();
		}

		List<Action> actionPool = [];

		if (ErnestoConditionManager.GetShipLogMode("Fact Mode"))
		{
			actionPool.Add(SetRandomFact);
		}
		
		if (ErnestoConditionManager.GetShipLogMode("Entry Mode"))
		{
			actionPool.Add(SetRandomEntry);
		}

		if (ErnestoConditionManager.GetShipLogMode("Planet Mode"))
		{
			actionPool.Add(SetRandomPlanet);
		}
		
		if (ErnestoConditionManager.GetShipLogMode("Rumor Mode"))
		{
			actionPool.Add(SetRandomRumor);
		}

		if (actionPool.Count > 0)
		{
			ErnestoChase.WriteDebugMessage("pick rand");
			var rand = new System.Random();
			actionPool[rand.Next(0, actionPool.Count)].Invoke();
		}
		else
		{
			WriteDebugMessage("No gamemode selected!");
		}
		
		if (!Instance.AllowShipLog)
		{
			Locator.GetShipBody().GetComponentInChildren<ShipLogController>().SetDamaged(true);
		}

		_currentRound++;

		if (InMultiplayer && QSBAPI.GetIsHost())
		{
			foreach (var id in Players)
			{
				if (Instance.GlobalFactGoals)
				{
					QSBCompat.SendRandomShipLogFact(id, _selectedFact.GetID());
				}
				else
				{
					QSBCompat.SendRandomFactGenerate(id);
				}
			}
		}
	}

	private void SetRandomFact()
	{
		var facts = Locator.GetShipLogManager()._factList
			.Where(fact => !fact.IsRevealed() && !fact.IsRumor() &&
				(Instance.EnableDLCLogs || 
					Locator.GetShipLogManager()
						.GetEntry(fact.GetEntryID())
						.GetAstroObjectID() != "INVISIBLE_PLANET")
			).ToArray();
		
		if (facts.Length == 0)
		{
			WriteDebugMessage("AAAH THERE'S NO FACTS TO FIND");
			return;
		}
		
		_selectedFact = facts[Random.Range(0, facts.Length)];
		_selectedFact.OnFactRevealed += OnFactRevealed;

		_currentShipLogMode = ShipLogGameMode.Fact;
		_shipLogInfo.AssignShipLogFact(_selectedFact);
	}

	private void SetRandomEntry()
	{
		var entries = Locator.GetShipLogManager()._entryList
			.Where(entry => entry.GetExploreFacts().Any(fact => !fact.IsRevealed()) &&
				(Instance.EnableDLCLogs || entry.GetAstroObjectID() != "INVISIBLE_PLANET")
			).ToArray();
		
		if (entries.Length == 0)
		{
			WriteDebugMessage("AAAH THERE'S NO ENTRIES TO FIND");
			return;
		}
		
		_selectedEntry = entries[Random.Range(0, entries.Length)];
		_selectedEntry.GetExploreFacts()
			.Where(fact => !fact.IsRevealed())
			.ToList()
			.ForEach(fact => fact.OnFactRevealed += OnFactRevealed);
		
		_currentShipLogMode = ShipLogGameMode.Entry;
		_shipLogInfo.AssignShipLogEntry(_selectedEntry);
	}

	private void SetRandomPlanet()
	{
		ShipLogMapMode mapMode = Locator.GetShipTransform().GetComponentInChildren<ShipLogMapMode>(true);
		WriteDebugMessage("map mode: " + mapMode);
		Instance.ModHelper.Events.Unity.RunWhen(
			() => mapMode._listItems != null,
			() =>
			{
				List<ShipLogAstroObject> planets = [];
				foreach (var list in mapMode._astroObjects)
				{
					foreach (var astro in list)
					{
						if (!Instance.EnableDLCLogs && astro.GetID() == "INVISIBLE_PLANET") continue;
						
						WriteDebugMessage("\nCHECK PLANET " + astro.name);
						var entries = Locator.GetShipLogManager()
							.GetEntriesByAstroBody(astro.GetID());
						foreach (var entry in entries)
						{
							WriteDebugMessage("checking entry " + entry._name);
							if (entry.GetExploreFacts().Any(fact => !fact.IsRevealed()))
							{
								WriteDebugMessage("has empty log");
								planets.Add(astro);
								break;
							}
						}
					}
				}
		
				if (planets.Count == 0)
				{
					WriteDebugMessage("AAAH THERE'S NO PLANETS TO FIND");
					return;
				}
		
				_selectedPlanet = planets[Random.Range(0, planets.Count)];
				Locator.GetShipLogManager().GetEntriesByAstroBody(_selectedPlanet.GetID())
					.ForEach(entry => entry.GetExploreFacts()
						.Where(fact => !fact.IsRevealed())
						.ToList()
						.ForEach(fact => fact.OnFactRevealed += OnFactRevealed));
		
				_currentShipLogMode = ShipLogGameMode.Planet;
				_shipLogInfo.AssignShipLogPlanet(_selectedPlanet);
			});
	}
	
	private void SetRandomRumor()
	{
		var rumors = Locator.GetShipLogManager()._factList
			.Where(fact => !fact.IsRevealed() && fact.IsRumor() &&
				(Instance.EnableDLCLogs ||
					Locator.GetShipLogManager()
						.GetEntry(fact.GetEntryID())
						.GetAstroObjectID() != "INVISIBLE_PLANET")
			).ToArray();
		
		if (rumors.Length == 0)
		{
			WriteDebugMessage("AAAH THERE'S NO RUMORS TO FIND");
			return;
		}
		
		_selectedFact = rumors[Random.Range(0, rumors.Length)];
		_selectedFact.OnFactRevealed += OnFactRevealed;

		_currentShipLogMode = ShipLogGameMode.Rumor;
		_currentRumorChain = 1;
		_shipLogInfo.AssignShipLogFact(_selectedFact);
	}

	private bool CanBranchRumor(ShipLogFact rumor)
	{
		if (!rumor.HasSource()) return false;

		var entry = Locator.GetShipLogManager().GetEntry(rumor.GetSourceID());
		return entry.GetRumorFacts().Count > 0 && 
			entry.GetRumorFacts().Any(fact => !fact.IsRevealed());
	}
	
	private void SetBranchingRumor(ShipLogFact baseRumor)
	{
		var entry = Locator.GetShipLogManager().GetEntry(baseRumor.GetSourceID());
		var rumors = entry.GetRumorFacts()
			.Where(fact => !fact.IsRevealed()).ToArray();
		
		if (rumors.Length == 0)
		{
			WriteDebugMessage("AAAH THERE'S NO RUMORS TO BRANCH TO");
			return;
		}
		
		_selectedFact = rumors[Random.Range(0, rumors.Length)];
		_selectedFact.OnFactRevealed += OnFactRevealed;

		_currentShipLogMode = ShipLogGameMode.Rumor;
		_currentRumorChain++;
		_shipLogInfo.AssignShipLogFact(_selectedFact);
	}

	// MAKE THIS WORK
	public void SetUpRandomShipLogRemote(string factID)
	{
		_selectedFact = Locator.GetShipLogManager().GetFact(factID);
		//_selectedFact.OnFactRevealed += OnFactRevealed;

		if (!_shipLogInfo)
		{
			GameObject ui = LoadPrefab("Assets/ErnestoChase/RandomShipLogHUD.prefab");
			_shipLogInfo = Instantiate(ui).GetComponentInChildren<RandomShipLogInfo>();
		}
		
		_shipLogInfo.AssignShipLogFact(_selectedFact);
		
		if (!Instance.AllowShipLog)
		{
			Locator.GetShipBody().GetComponentInChildren<ShipLogController>().SetDamaged(true);
		}
	}

	public ShipLogGameMode GetShipLogGameMode()
	{
		return _currentShipLogMode;
	}

	public void WinRandomShipLogRemote()
	{
		if (!_shipLogInfo)
		{
			return;
		}
		
		if (_selectedFact != null)
		{
			_selectedFact.OnFactRevealed -= OnFactRevealed;
			_selectedFact = null;
		}

		if (_selectedEntry != null)
		{
			_selectedEntry.GetExploreFacts()
				.ForEach(fact => fact.OnFactRevealed -= OnFactRevealed);
			_selectedEntry = null;
		}

		if (_selectedPlanet != null)
		{
			Locator.GetShipLogManager().GetEntriesByAstroBody(_selectedPlanet.GetID())
				.ForEach(entry => entry.GetExploreFacts()
					.ForEach(fact => fact.OnFactRevealed -= OnFactRevealed));
		}
		
		_shipLogInfo.DisplayWinText(_numHintsUsed);
		foreach (var e in Instance.ernestos)
		{
			e.GetComponent<ErnestoManager>().OnGameStopped();
		}
	}

	public void OnFactRevealed()
	{
		if (!_shipLogInfo)
		{
			return;
		}

		var lastFact = _selectedFact;
		bool canBranch = _currentShipLogMode == ShipLogGameMode.Rumor &&
			_currentRumorChain < Instance.MaxRumorChain && CanBranchRumor(_selectedFact);
		
		if (_selectedFact != null)
		{
			_selectedFact.OnFactRevealed -= OnFactRevealed;
			_selectedFact = null;
		}

		if (_selectedEntry != null)
		{
			_selectedEntry.GetExploreFacts()
				.ForEach(fact => fact.OnFactRevealed -= OnFactRevealed);
			_selectedEntry = null;
		}

		if (_selectedPlanet != null)
		{
			Locator.GetShipLogManager().GetEntriesByAstroBody(_selectedPlanet.GetID())
				.ForEach(entry => entry.GetExploreFacts()
					.ForEach(fact => fact.OnFactRevealed -= OnFactRevealed));
		}

		if (canBranch)
		{
			_shipLogInfo.OnBranchRumor();
			SetBranchingRumor(lastFact);
			return;
		}
		
		_shipLogInfo.OnObjectiveCompleted();
		
		if (_currentRound >= Instance.ShipLogRounds)
		{
			_shipLogInfo.DisplayWinText(_numHintsUsed);
			foreach (var e in Instance.ernestos)
			{
				e.GetComponent<ErnestoManager>().OnGameStopped();
			}

			if (InMultiplayer && QSBAPI.GetIsHost() && Instance.GlobalFactGoals)
			{
				foreach (var id in Players)
				{
					QSBCompat.SendShipLogWin(id);
				}
			}
		}
		else
		{
			SetUpRandomShipLog();
		}
	}

	public void OnGameStopped()
	{
		OnPlayerDeath(DeathType.Digestion);
	}
	
	private void Update()
	{
		if (_shipLogInfo)
		{
			if (Keyboard.current.hKey.wasPressedThisFrame &&
				_shipLogInfo.AdvanceHint())
			{
				_numHintsUsed++;
			}

			if (Keyboard.current.oKey.wasPressedThisFrame)
			{
				_shipLogInfo.ToggleTextHidden();
			}

			if (Keyboard.current.lKey.wasPressedThisFrame)
			{
				_shipLogInfo.ToggleTextExpanded();
			}
		}
	}
	
	private void FixedUpdate()
	{
		if (!_syncingTimer || !InMultiplayer)
		{
			return;
		}
		
		if (_frameDelay <= 0)
		{
			foreach (var id in Players)
			{
				QSBCompat.SendSurvivalTimerSync(id, _countdownTimer.GetCurrentTime());
			}

			_frameDelay = _syncFrameDelay;
		}
		else
		{
			_frameDelay--;
		}
	}
}