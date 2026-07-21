using UnityEngine;

namespace ErnestoChase.ErnestoAI;

public class BerserkerAction : ErnestoAction
{
	private bool _beserkerMode;
	
	public override Name GetName() => Name.Berserker;

	public override float CalculateUtility()
	{
		if (_controller.GetRemainingTargetCount() > 0 &&
			!_controller.GetCurrentTarget().isTeleport &&
			(_beserkerMode || _state.previousAction == Name.FollowStupid))
		{
			return 80f;
		}

		return -100f;
	}

	protected override void OnEnterAction()
	{
		_beserkerMode = true;
		_controller.MoveToNextTarget();
		
		_effects.PlayRoar();
		_effects.SetLoopsEnabled(true);
		_effects.SetMusicEnabled(true);
		_effects.SetLightEnabled(true);
	}

	public override bool Update_Action()
	{
		if (_controller.GetCurrentTarget().isTeleport)
		{
			return false;
		}

		return true;
	}

	public override void FixedUpdate_Action()
	{
		var targetProgress = _controller.GetRemainingTargetProgress();
		float cutoff = 20f;
		float scalar = 25f;
		float speedMult = 3f;
		var speedLerp = Mathf.LerpUnclamped(1f, speedMult, 
			Mathf.Max(0f, (targetProgress - cutoff) / (scalar - cutoff)));
		_controller.CurrentSpeed = _controller.BaseSpeed * speedLerp * 5f;
	}

	public override void OnArriveAtTarget()
	{
		if (_controller.GetCurrentTarget().isTeleport)
		{
			return;
		}
		
		_controller.MoveToNextTarget();
	}
}