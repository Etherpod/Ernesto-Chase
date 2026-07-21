using UnityEngine;

namespace ErnestoChase.ErnestoAI;

public class SpaceLinearAction : ErnestoAction
{
	private float _speedAccumulationRate;
	
	public override Name GetName() => Name.SpaceLinear;

	public override void Initialize(ErnestoState state, ErnestoController controller, ErnestoEffects effects)
	{
		base.Initialize(state, controller, effects);
		_speedAccumulationRate = 2f;
	}

	public override float CalculateUtility()
	{
		if (_controller.GetRelativePlayerBody() != null &&
			_state.SpaceAccelerationType == "Linear")
		{
			return 30f;
		}

		return -100f;
	}

	protected override void OnEnterAction()
	{
		_controller.MoveRigidbodyToPlayer(ErnestoController.RigidbodyMode.Relative);
	}
	
	public override bool Update_Action()
	{
		return true;
	}

	public override void FixedUpdate_Action()
	{
		_controller.CurrentSpeed += _speedAccumulationRate * Time.fixedDeltaTime;
	}
}