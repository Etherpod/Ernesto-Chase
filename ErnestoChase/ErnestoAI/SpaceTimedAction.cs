namespace ErnestoChase.ErnestoAI;

public class SpaceTimedAction : ErnestoAction
{
	public override Name GetName() => Name.SpaceTimed;
	
	public override float CalculateUtility()
	{
		if (_controller.GetRelativePlayerBody() != null &&
			_state.SpaceAccelerationType == "Timed")
		{
			return 30f;
		}

		return -100f;
	}
	
	protected override void OnEnterAction()
	{
		_controller.CurrentSpeed = _controller.GetDistanceToPlayer() / _state.SpaceTimer;
		_controller.MoveRigidbodyToPlayer(ErnestoController.RigidbodyMode.Relative);
	}
	
	public override bool Update_Action()
	{
		return true;
	}
}