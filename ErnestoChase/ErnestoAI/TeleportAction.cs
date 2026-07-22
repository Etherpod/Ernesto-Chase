using UnityEngine;

namespace ErnestoChase.ErnestoAI;

public class TeleportAction : ErnestoAction
{
	private bool _playingWarpEffects;
	
	public override Name GetName() => Name.Teleport;

	public override bool IsInterruptible()
	{
		return false;
	}

	public override float CalculateUtility()
	{
		if (_controller.GetCurrentTarget().isTeleport &&
			_controller.GetNextTarget().isTeleport)
		{
			return 100f;
		}

		return -100f;
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
		ErnestoChase.WriteDebugMessage("ENTER TELEPORT ACTION");
		_controller.DisableMovement();
		_effects.SetLoopsEnabled(false);
		_effects.SetMusicEnabled(false);
		_effects.BlackHoleCollapsed += OnBlackHoleCollapsed;
		_effects.MakeBlackHole();
		_playingWarpEffects = true;
	}

	private void OnBlackHoleCollapsed()
	{
		_effects.BlackHoleCollapsed -= OnBlackHoleCollapsed;
		
		_controller.SkipTargets(1);
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