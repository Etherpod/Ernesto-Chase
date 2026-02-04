using UnityEngine;
using UnityEngine.UI;
using static ErnestoChase.MinigameManager;

namespace ErnestoChase;

public class RandomShipLogInfo : MinigameUIText
{
	[SerializeField] private RandomShipLogNotification _notification = null;
	[SerializeField] private AudioClip _notificationAudio1 = null;
	[SerializeField] private AudioClip _notificationAudio2 = null;
	
	private MinigameManager _minigameManager;
	private ShipLogFact _assignedFact;
	private ShipLogEntry _assignedEntry;
	private ShipLogAstroObject _assignedPlanet;
	
	private bool _originRevealed;
	private bool _locationRevealed;
	private bool _sourceRevealed;
	private readonly int _characterLimit = 125;
	private bool _textExpanded;

	protected override void Awake()
	{
		base.Awake();
		_minigameManager = ErnestoChase.MinigameManager;
	}

	public override void UpdateText()
	{
		string roundText = $"- Round {_minigameManager.GetShipLogRound()} -";
		
		if (_minigameManager.GetShipLogGameMode() == ShipLogGameMode.Fact && _assignedFact != null)
		{
			if (_textExpanded && _assignedFact.GetText().Length > _characterLimit)
			{
				_currentText = $"<i>(Press L to collapse)</i>\n\nFACT: {_assignedFact.GetText()}";
				base.UpdateText();
				return;
			}
			
			string goalText = "OBJECTIVE: Find and learn the specified fact.";
			string factText;
			if (_assignedFact.GetText().Length > _characterLimit)
			{
				factText = "FACT: " + _assignedFact.GetText()
					.Substring(0, _characterLimit).Trim() + 
					"...\n<i>(Press L to expand)</i>";
			}
			else
			{
				factText = "FACT: " + _assignedFact.GetText();
			}
			string origin = "PLANET: [HIDDEN]";
			string location = "LOCATION: [HIDDEN]";
		
			if (_originRevealed)
			{
				var entry = Locator.GetShipLogManager().GetEntry(_assignedFact.GetEntryID());
				string text = AstroObject.AstroObjectNameToString(
					AstroObject.StringIDToAstroObjectName(entry.GetAstroObjectID()));
				if (text.Length <= 0 && ErnestoChase.NHInteraction != null)
				{
					string nhName = ErnestoChase.NHInteraction.GetNameFromAstroID(entry.GetAstroObjectID());
					origin = "PLANET: " + (nhName.Length <= 0 ? "Unknown" : nhName);
				}
				else
				{
					origin = "PLANET: " + (text.Length <= 0 ? "Unknown" : text);
				}
			}

			if (_locationRevealed)
			{
				var entry = Locator.GetShipLogManager().GetEntry(_assignedFact.GetEntryID());
				location = "LOCATION: " + entry.GetName(false);
			}

			_currentText = $"{roundText}\n{goalText}\n\n{factText}\n\n{origin}\n\n{location}";
		}
		else if (_minigameManager.GetShipLogGameMode() == ShipLogGameMode.Entry && _assignedEntry != null)
		{
			string goalText = "OBJECTIVE: Learn any fact at the specified location.";
			string entryText = "LOCATION: " + _assignedEntry.GetName(false);
			string origin = "PLANET: [HIDDEN]";
			
			if (_originRevealed)
			{
				string text = AstroObject.AstroObjectNameToString(
					AstroObject.StringIDToAstroObjectName(_assignedEntry.GetAstroObjectID()));
				if (text.Length <= 0 && ErnestoChase.NHInteraction != null)
				{
					string nhName = ErnestoChase.NHInteraction.GetNameFromAstroID(_assignedEntry.GetAstroObjectID());
					origin = "PLANET: " + (nhName.Length <= 0 ? "Unknown" : nhName);
				}
				else
				{
					origin = "PLANET: " + (text.Length <= 0 ? "Unknown" : text);
				}
			}
			
			_currentText = $"{roundText}\n{goalText}\n\n{entryText}\n\n{origin}";
		}
		else if (_minigameManager.GetShipLogGameMode() == ShipLogGameMode.Planet && _assignedPlanet != null)
		{
			string goalText = "OBJECTIVE: Learn any fact that belongs to the specified planet.";
			string text = AstroObject.AstroObjectNameToString(
				AstroObject.StringIDToAstroObjectName(_assignedPlanet.GetID()));
			if (text.Length <= 0 && ErnestoChase.NHInteraction != null)
			{
				string nhName = ErnestoChase.NHInteraction.GetNameFromAstroID(_assignedPlanet.GetID());
				_currentText = $"{roundText}\n{goalText}\n\nPLANET: " + (nhName.Length <= 0 ? "Unknown" : nhName);
			}
			else
			{
				_currentText = $"{roundText}\n{goalText}\n\nPLANET: " + (text.Length <= 0 ? "Unknown" : text);
			}
		}
		else if (_minigameManager.GetShipLogGameMode() == ShipLogGameMode.Rumor && _assignedFact != null)
		{
			if (_textExpanded && _assignedFact.GetText().Length > _characterLimit)
			{
				_currentText = $"<i>(Press L to collapse)</i>\n\nRUMOR: {_assignedFact.GetText()}";
				base.UpdateText();
				return;
			}
			
			string goalText = "OBJECTIVE: Find the source of the specified rumor.";
			string rumorText;
			if (_assignedFact.GetText().Length > _characterLimit)
			{
				rumorText = "RUMOR: " + _assignedFact.GetText()
					.Substring(0, _characterLimit).Trim() + 
					"...\n<i>(Press L to expand)</i>";
			}
			else
			{
				rumorText = "RUMOR: " + _assignedFact.GetText();
			}
			string location = "SUBJECT: [HIDDEN]";
			string origin = "PLANET: [HIDDEN]";
			string source = "LOCATION: [HIDDEN]";
			
			// rumor reveals location first instead of origin
			if (_originRevealed)
			{
				var entry = Locator.GetShipLogManager().GetEntry(_assignedFact.GetEntryID());
				location = "SUBJECT: " + entry.GetName(false);
			}

			if (_assignedFact.HasSource())
			{
				if (_locationRevealed)
				{
					var entry = Locator.GetShipLogManager().GetEntry(_assignedFact.GetSourceID());
					string text = AstroObject.AstroObjectNameToString(
						AstroObject.StringIDToAstroObjectName(entry.GetAstroObjectID()));
					if (text.Length <= 0 && ErnestoChase.NHInteraction != null)
					{
						string nhName = ErnestoChase.NHInteraction.GetNameFromAstroID(entry.GetAstroObjectID());
						origin = "PLANET: " + (nhName.Length <= 0 ? "Unknown" : nhName);
					}
					else
					{
						origin = "PLANET: " + (text.Length <= 0 ? "Unknown" : text);
					}
				}

				if (_sourceRevealed)
				{
					var entry = Locator.GetShipLogManager().GetEntry(_assignedFact.GetSourceID());
					source = "LOCATION: " + entry.GetName(false);
				}
			}
			else
			{
				origin = "PLANET: Unknown or self";
				source = "LOCATION: Unknown or self";
			}

			if (ErnestoChase.Instance.MaxRumorChain > 1)
			{
				roundText += $" Chain #{_minigameManager.GetShipLogRumorChain()} -";
			}
			
			_currentText = $"{roundText}\n{goalText}\n\n{rumorText}\n\n{location}\n\n{origin}\n\n{source}";
		}
		
		base.UpdateText();
	}

	public void AssignShipLogFact(ShipLogFact fact)
	{
		_originRevealed = false;
		_locationRevealed = false;
		_sourceRevealed = false;
		_textHidden = false;
		_textExpanded = false;
		
		_assignedFact = fact;
		UpdateText();
	}

	public void AssignShipLogEntry(ShipLogEntry entry)
	{
		_originRevealed = false;
		_locationRevealed = false;
		_sourceRevealed = false;
		_textHidden = false;
		_textExpanded = false;

		_assignedEntry = entry;
		UpdateText();
	}
	
	public void AssignShipLogPlanet(ShipLogAstroObject planet)
	{
		_originRevealed = false;
		_locationRevealed = false;
		_sourceRevealed = false;
		_textHidden = false;
		_textExpanded = false;

		_assignedPlanet = planet;
		UpdateText();
	}

	public bool AdvanceHint()
	{
		bool hasSourceRumor = _minigameManager.GetShipLogGameMode() == ShipLogGameMode.Rumor &&
			_assignedFact.HasSource();
		
		if (!_originRevealed && _minigameManager.GetShipLogGameMode() is
			ShipLogGameMode.Fact or ShipLogGameMode.Entry or ShipLogGameMode.Rumor)
		{
			_originRevealed = true;
			UpdateText();
			return true;
		}

		if (!_locationRevealed && (_minigameManager.GetShipLogGameMode() is
			ShipLogGameMode.Fact || hasSourceRumor))
		{
			_locationRevealed = true;
			UpdateText();
			return true;
		}

		if (!_sourceRevealed && hasSourceRumor)
		{
			_sourceRevealed = true;
			UpdateText();
			return true;
		}

		return false;
	}

	public void SetCurrentHints(bool origin, bool location, bool source)
	{
		_originRevealed = origin;
		_locationRevealed = location;
		_sourceRevealed = source;
		UpdateText();
	}

	public (bool origin, bool location, bool source) GetCurrentHints()
	{
		return (_originRevealed, _locationRevealed, _sourceRevealed);
	}

	public void ToggleTextExpanded() => SetTextExpanded(!_textExpanded);

	public void SetTextExpanded(bool expand)
	{
		_textExpanded = expand;
		UpdateText();
	}

	public void OnObjectiveCompleted()
	{
		Locator.GetPlayerAudioController()._oneShotExternalSource.PlayOneShot(_notificationAudio1, 1f);
		_notification.ShowNotification("Objective Completed!");
	}
	
	public void OnBranchRumor()
	{
		Locator.GetPlayerAudioController()._oneShotExternalSource.PlayOneShot(_notificationAudio2, 1f);
		_notification.ShowNotification("Choosing Next Rumor...");
	}

	public void DisplayWinText(int hintsUsed)
	{
		_currentText = $"YOU WON!!!!!\nHints used: {hintsUsed}";
		_text.text = _currentText;
		_assignedFact = null;
		_assignedEntry = null;
		_assignedPlanet = null;
	}
}