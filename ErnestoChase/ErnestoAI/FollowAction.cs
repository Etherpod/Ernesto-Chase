using UnityEngine;

namespace ErnestoChase.ErnestoAI;

public class FollowAction : ErnestoAction
{
	public override Name GetName() => Name.Follow;

	public override float CalculateUtility()
	{
		if (_controller.GetRemainingTargetCount() > 0 &&
			!_controller.GetCurrentTarget().isTeleport)
		{
			return 30f;
		}

		return -100f;
	}

	protected override void OnEnterAction()
	{
		_controller.MoveToNextTarget();
		
		_effects.SetLoopsEnabled(false);
		_effects.SetMusicEnabled(false);
		_effects.SetLightEnabled(false);
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
		_controller.CurrentSpeed = _controller.BaseSpeed * speedLerp;
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