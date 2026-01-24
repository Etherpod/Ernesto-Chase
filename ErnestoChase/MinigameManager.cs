using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using static ErnestoChase.ErnestoChase;

namespace ErnestoChase;

public class MinigameManager : MonoBehaviour
{
	private readonly int _syncFrameDelay = 120;
	private int _frameDelay;
	private CountdownTimer _countdownTimer;
	private bool _syncingTimer;

	private ShipLogFact _selectedFact;
	private RandomShipLogInfo _factInfo;
	private int _numHintsUsed;

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
			_selectedFact.OnFactRevealed += OnFactRevealed;
			_selectedFact = null;
		}
		_factInfo = null;
		_numHintsUsed = 0;
	}

	private void OnWakeUp()
	{
		Instance.ModHelper.Events.Unity.FireInNUpdates(() =>
		{
			WriteDebugMessage("woke up");
			if (_countdownTimer != null)
			{
				_countdownTimer.FadeIn(5f);
			}
			else if (_factInfo != null)
			{
				WriteDebugMessage("fade in");
				_factInfo.FadeIn(5f);
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
		else if (_factInfo != null)
		{
			_factInfo.FadeOut(5f);
		}
	}
	
	public void SetUpMinigames()
	{
		if (ErnestoConditionManager.RandomShipLogEnabled &&
			(!InMultiplayer || !Instance.GlobalFactGoals || QSBAPI.GetIsHost()))
		{
			SetUpRandomShipLog();
		}
		else if (ErnestoConditionManager.SurvivalEnabled &&
			(!InMultiplayer || QSBAPI.GetIsHost()))
		{
			SetUpSurvival();
		}
	}

	private void SetUpSurvival()
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
	
	private void SetUpRandomShipLog()
	{
		WriteDebugMessage("Setting up facts");
		var facts = Locator.GetShipLogManager()._factList
			.Where(fact => !fact.IsRevealed() && !fact.IsRumor()).ToArray();
		if (facts.Length == 0)
		{
			WriteDebugMessage("AAAH THERE'S NO FACTS TO FIND");
			return;
		}
		
		_selectedFact = facts[Random.Range(0, facts.Length)];
		_selectedFact.OnFactRevealed += OnFactRevealed;
		
		GameObject ui = LoadPrefab("Assets/ErnestoChase/RandomShipLogHUD.prefab");
		_factInfo = Instantiate(ui).GetComponentInChildren<RandomShipLogInfo>();
		_factInfo.AssignShipLogFact(_selectedFact);
		
		if (!Instance.AllowShipLog)
		{
			Locator.GetShipBody().GetComponentInChildren<ShipLogController>().SetDamaged(true);
		}

		if (InMultiplayer && Instance.GlobalFactGoals)
		{
			foreach (var id in Players)
			{
				QSBCompat.SendRandomShipLogFact(id, _selectedFact.GetID());
			}
		}
	}

	public void SetUpRandomShipLogRemote(string factID)
	{
		_selectedFact = Locator.GetShipLogManager().GetFact(factID);
		_selectedFact.OnFactRevealed += OnFactRevealed;
		
		GameObject ui = LoadPrefab("Assets/ErnestoChase/RandomShipLogHUD.prefab");
		_factInfo = Instantiate(ui).GetComponentInChildren<RandomShipLogInfo>();
		_factInfo.AssignShipLogFact(_selectedFact);
		
		if (!Instance.AllowShipLog)
		{
			Locator.GetShipBody().GetComponentInChildren<ShipLogController>().SetDamaged(true);
		}
	}

	public void OnFactRevealed()
	{
		if (_selectedFact == null) return;
		
		_factInfo.DisplayWinText(_numHintsUsed);
		_selectedFact.OnFactRevealed -= OnFactRevealed;
		_selectedFact = null;
	}

	public void OnGameStopped()
	{
		OnPlayerDeath(DeathType.Digestion);
	}
	
	private void Update()
	{
		if (_selectedFact != null)
		{
			if (Keyboard.current.hKey.wasPressedThisFrame)
			{
				if (_numHintsUsed == 0)
				{
					_factInfo.RevealFactOrigin();
				}
				else if (_numHintsUsed == 1)
				{
					_factInfo.RevealFactLocation();
				}

				_numHintsUsed = Mathf.Min(2, _numHintsUsed + 1);
			}

			if (Keyboard.current.pKey.wasPressedThisFrame)
			{
				_factInfo.ToggleTextHidden();
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