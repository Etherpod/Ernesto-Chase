using UnityEngine;
using UnityEngine.UI;
using static ErnestoChase.MinigameManager;

namespace ErnestoChase;

[RequireComponent(typeof(Text))]
public class RandomShipLogInfo : MonoBehaviour
{
	private MinigameManager _minigameManager;
	private ShipLogFact _assignedFact;
	private ShipLogEntry _assignedEntry;
	private ShipLogAstroObject _assignedPlanet;
	
	private bool _originRevealed;
	private bool _locationRevealed;
	
	private Text _text;
	private string _currentText = "";
	private bool _textHidden;
	
	private bool _fading;
	private float _fadeLength;
	private float _fadeStartTime;
	private float _startFade;
	private float _targetFade;

	private void Awake()
	{
		_minigameManager = ErnestoChase.MinigameManager;
		_text = gameObject.GetRequiredComponent<Text>();
		
		var font = (Font)Resources.Load(@"fonts\english - latin\HVD Fonts - BrandonGrotesque-Bold_Dynamic");
		if (font != null)
		{
			_text.font = font;
		}
		
		var color = _text.color;
		color.a = 0f;
		_text.color = color;
	}
	
	private void LateUpdate()
	{
		if (_fading)
		{
			float lerp = Mathf.InverseLerp(_fadeStartTime, _fadeStartTime + _fadeLength, Time.time);
			var color = _text.color;
			color.a = Mathf.Lerp(_startFade, _targetFade, lerp * lerp);
			_text.color = color;

			if (lerp >= 1f)
			{
				_fading = false;
			}
		}
	}

	private void UpdateText()
	{
		ErnestoChase.WriteDebugMessage("update");
		if (_minigameManager.GetShipLogGameMode() == ShipLogGameMode.Fact && _assignedFact != null)
		{
			ErnestoChase.WriteDebugMessage("fact");
			
			string factText = "FACT: " + _assignedFact.GetText();
			string origin = "ORIGIN: [HIDDEN]";
			string location = "LOCATION: [HIDDEN]";
		
			if (_originRevealed)
			{
				var entry = Locator.GetShipLogManager().GetEntry(_assignedFact.GetEntryID());
				string text = AstroObject.AstroObjectNameToString(
					AstroObject.StringIDToAstroObjectName(entry.GetAstroObjectID()));
				if (text.Length <= 0 && ErnestoChase.NHInteraction != null)
				{
					string nhName = ErnestoChase.NHInteraction.GetNameFromAstroID(entry.GetAstroObjectID());
					origin = "ORIGIN: " + (nhName.Length <= 0 ? "Unknown" : nhName);
				}
				else
				{
					origin = "ORIGIN: " + (text.Length <= 0 ? "Unknown" : text);
				}
			}

			if (_locationRevealed)
			{
				var entry = Locator.GetShipLogManager().GetEntry(_assignedFact.GetEntryID());
				location = "LOCATION: " + entry.GetName(false);
			}

			_currentText = $"{factText}\n\n{origin}\n\n{location}\n\n";
		}
		else if (_minigameManager.GetShipLogGameMode() == ShipLogGameMode.Entry && _assignedEntry != null)
		{
			ErnestoChase.WriteDebugMessage("entry");
			
			string entryText = "LOCATION: " + _assignedEntry.GetName(false);
			string origin = "ORIGIN: HIDDEN";
			
			if (_originRevealed)
			{
				string text = AstroObject.AstroObjectNameToString(
					AstroObject.StringIDToAstroObjectName(_assignedEntry.GetAstroObjectID()));
				if (text.Length <= 0 && ErnestoChase.NHInteraction != null)
				{
					string nhName = ErnestoChase.NHInteraction.GetNameFromAstroID(_assignedEntry.GetAstroObjectID());
					origin = "ORIGIN: " + (nhName.Length <= 0 ? "Unknown" : nhName);
				}
				else
				{
					origin = "ORIGIN: " + (text.Length <= 0 ? "Unknown" : text);
				}
			}
			
			_currentText = $"{entryText}\n\n{origin}";
		}
		else if (_minigameManager.GetShipLogGameMode() == ShipLogGameMode.Planet && _assignedPlanet != null)
		{
			ErnestoChase.WriteDebugMessage("planet");
			
			string text = AstroObject.AstroObjectNameToString(
				AstroObject.StringIDToAstroObjectName(_assignedPlanet.GetID()));
			if (text.Length <= 0 && ErnestoChase.NHInteraction != null)
			{
				string nhName = ErnestoChase.NHInteraction.GetNameFromAstroID(_assignedPlanet.GetID());
				_currentText = "ORIGIN: " + (nhName.Length <= 0 ? "Unknown" : nhName);
			}
			else
			{
				_currentText = "ORIGIN: " + (text.Length <= 0 ? "Unknown" : text);
			}
		}
		
		_text.text = _textHidden ? "" : _currentText;
	}

	public void AssignShipLogFact(ShipLogFact fact)
	{
		_originRevealed = false;
		_locationRevealed = false;
		_textHidden = false;
		
		_assignedFact = fact;
		UpdateText();
	}

	public void AssignShipLogEntry(ShipLogEntry entry)
	{
		_originRevealed = false;
		_locationRevealed = false;
		_textHidden = false;

		_assignedEntry = entry;
		UpdateText();
	}
	
	public void AssignShipLogPlanet(ShipLogAstroObject planet)
	{
		_originRevealed = false;
		_locationRevealed = false;
		_textHidden = false;

		_assignedPlanet = planet;
		UpdateText();
	}

	public bool AdvanceHint()
	{
		if (!_originRevealed && _minigameManager.GetShipLogGameMode() is
			ShipLogGameMode.Fact or ShipLogGameMode.Entry)
		{
			_originRevealed = true;
			UpdateText();
			return true;
		}

		if (!_locationRevealed && _minigameManager.GetShipLogGameMode() is
			ShipLogGameMode.Fact)
		{
			_locationRevealed = true;
			UpdateText();
			return true;
		}

		return false;
	}

	/*public void RevealFactOrigin()
	{
		_originRevealed = true;
		UpdateText();
	}

	public void RevealFactLocation()
	{
		_locationRevealed = true;
		UpdateText();
	}*/

	public void ToggleTextHidden() => SetTextHidden(!_textHidden);
	
	public void SetTextHidden(bool hide)
	{
		_textHidden = hide;
		_text.text = hide ? "" : _currentText;
	}
	
	public void FadeIn(float time)
	{
		ErnestoChase.WriteDebugMessage(_currentText);
		_fadeLength = time;
		_fadeStartTime = Time.time;
		_startFade = _text.color.a;
		_targetFade = 1f;
		_fading = true;
	}
	
	public void FadeOut(float time)
	{
		_fadeLength = time;
		_fadeStartTime = Time.time;
		_startFade = _text.color.a;
		_targetFade = 0f;
		_fading = true;
	}

	public void DisplayWinText(int hintsUsed)
	{
		_currentText = $"YOU WON!!!!!\nHints used: {hintsUsed}";
		SetTextHidden(false);
		_assignedFact = null;
	}
}