using UnityEngine;

namespace ErnestoChase.Abilities;

public abstract class Ability
{
	protected bool _activated;
	protected float _activateLength;
	protected float _activateT;
	protected float _cooldownLength;
	protected float _cooldownT;

	public Ability()
	{
		_activateLength = 10f;
		_cooldownLength = 10f;
	}
	
	public abstract string GetDisplayName();

	public bool OnCooldown() => _cooldownT > 0f;
	
	public bool IsActivated() => _activateT > 0f;

	public virtual bool CanActivate() => true;

	public virtual void Activate()
	{
		if (OnCooldown()) return;
		
		_activateT = _activateLength;
		_activated = true;
	}

	public virtual void Deactivate()
	{
		_activated = false;
		_cooldownT = _cooldownLength;
	}

	public virtual void Update()
	{
		if (_activated)
		{
			if (_activateT > 0f)
			{
				_activateT -= Time.deltaTime;
			}
			else
			{
				Deactivate();
			}
		}
		else if (_cooldownT > 0f)
		{
			_cooldownT -= Time.deltaTime;
		}
	}
}

public class PlayerTrackerAbility : Ability
{
	private PlayerTrackerGUI _trackerGUI;
	
	public PlayerTrackerAbility(PlayerTrackerGUI trackerGUI)
	{
		_trackerGUI = trackerGUI;
		_activateLength = 30f;
		_cooldownLength = 60f;
	}
	
	public override string GetDisplayName() => "Player Tracker";

	public override bool CanActivate()
	{
		return ECLocator.GetMorphController() != null && 
			ECLocator.GetMorphController().IsMorphed();
	}

	public override void Activate()
	{
		base.Activate();
		_trackerGUI.SetActivated(true);
	}

	public override void Deactivate()
	{
		base.Deactivate();
		_trackerGUI.SetActivated(false);
	}
}