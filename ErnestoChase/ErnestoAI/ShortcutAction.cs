using UnityEngine;

namespace ErnestoChase.ErnestoAI;

public class ShortcutAction : ErnestoAction
{
	private readonly float _maxVisibilityDist = 200f;
	private readonly int _minTargetCount = 40;
	private readonly float _shortcutCooldown = 30f;
	private bool _hasShortcut;
	private bool _playingWarpEffects;
	
	public override Name GetName() => Name.Shortcut;
	
	public override bool IsInterruptible()
	{
		return false;
	}

	public override float CalculateUtility()
	{
		if (!_hasShortcut &&
			_controller.GetRelativePlayerBody() != null &&
			_controller.GetRemainingTargetCount() > _minTargetCount &&
			Time.time > _enterTime + _shortcutCooldown &&
			PlayerIsVisible())
		{
			return 100f;
		}

		return -100f;
	}

	private bool PlayerIsVisible()
	{
		var toPlayer = Locator.GetPlayerCamera().transform.position - _controller.transform.position;
		var dist = toPlayer.magnitude;

		if (dist > _maxVisibilityDist) return false;
		
		if (Physics.Raycast(_controller.transform.position, toPlayer,
			dist - 1f, OWLayerMask.physicalMask))
		{
			return false;
		}

		return true;
	}

	public override bool Update_Action()
	{
		if (_playingWarpEffects)
		{
			return true;
		}

		return false;
	}

	protected override void OnEnterAction()
	{
		ErnestoChase.WriteDebugMessage("enter shortcut action");
		_controller.DisableMovement();
		_effects.SetLoopsEnabled(false);
		_effects.SetMusicEnabled(false);
		_effects.BlackHoleCollapsed += OnBlackHoleCollapsed;
		_effects.MakeBlackHole();
		_playingWarpEffects = true;
		_hasShortcut = true;
	}

	private void OnBlackHoleCollapsed()
	{
		_effects.BlackHoleCollapsed -= OnBlackHoleCollapsed;

		var skipNum = Mathf.Max(_controller.GetRemainingTargetCount() - 10, 
			_controller.GetRemainingTargetCount() / 2);
		_controller.SkipTargets(skipNum);
		_controller.SnapToNextTarget();

		_effects.WhiteHoleCreated += OnWhiteHoleCreated;
		_effects.MakeWhiteHole();
	}

	private void OnWhiteHoleCreated()
	{
		_effects.WhiteHoleCreated -= OnWhiteHoleCreated;
		_playingWarpEffects = false;
	}
}