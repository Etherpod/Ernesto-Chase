using UnityEngine;

namespace ErnestoChase.Abilities;

public class AbilitiesManager : MonoBehaviour
{
	private Ability _primaryAbility;
	private Ability _secondaryAbility;
	private bool _isErnesto;

	private ScreenPrompt _primaryScreenPrompt;
	private ScreenPrompt _secondaryScreenPrompt;
	
	private void Awake()
	{
		_isErnesto = ErnestoChase.Instance.ErnestoMorph;
		_primaryScreenPrompt = new ScreenPrompt(InputLibrary.interact, "No ability selected");
		_secondaryScreenPrompt = new ScreenPrompt(InputLibrary.toolActionSecondary, "No ability selected");
	}

	private void Start()
	{
		Locator.GetPromptManager().AddScreenPrompt(_primaryScreenPrompt, PromptPosition.UpperRight);
		Locator.GetPromptManager().AddScreenPrompt(_secondaryScreenPrompt, PromptPosition.UpperRight);
	}

	private void Update()
	{
		UpdateAbility(_primaryAbility, _primaryScreenPrompt);
		UpdateAbility(_secondaryAbility, _secondaryScreenPrompt);
	}

	private void UpdateAbility(Ability ability, ScreenPrompt abilityPrompt)
	{
		if (ability != null)
		{
			abilityPrompt.SetVisibility(true);
			
			if (ability.OnCooldown() || ability.IsActivated() || !ability.CanActivate())
			{
				abilityPrompt.SetDisplayState(ScreenPrompt.DisplayState.GrayedOut);
			}
			else
			{
				abilityPrompt.SetDisplayState(ScreenPrompt.DisplayState.Normal);
				
				if (OWInput.IsNewlyPressed(InputLibrary.interact, InputMode.Character))
				{
					ability.Activate();
				}
			}
			
			ability.Update();
		}
		else
		{
			abilityPrompt.SetVisibility(false);
		}
	}

	public void SetPrimaryAbility(Ability ability)
	{
		_primaryAbility = ability;

		if (ability != null)
		{
			_primaryScreenPrompt.SetText("Activate " + ability.GetDisplayName());
		}
		else
		{
			_primaryScreenPrompt.SetText("No ability selected");
		}
	}
	
	public void SetSecondaryAbility(Ability ability)
	{
		_secondaryAbility = ability;
		
		if (ability != null)
		{
			_secondaryScreenPrompt.SetText("Activate " + ability.GetDisplayName());
		}
		else
		{
			_secondaryScreenPrompt.SetText("No ability selected");
		}
	}

	private void OnDestroy()
	{
		Locator.GetPromptManager().RemoveScreenPrompt(_primaryScreenPrompt, PromptPosition.UpperRight);
		Locator.GetPromptManager().RemoveScreenPrompt(_secondaryScreenPrompt, PromptPosition.UpperRight);
	}
}