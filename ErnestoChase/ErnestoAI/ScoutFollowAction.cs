using UnityEngine;

namespace ErnestoChase.ErnestoAI;

public class ScoutFollowAction : ErnestoAction
{
	private float _utilityDelay;
	
	public override Name GetName() => Name.FollowStupid;

	public override float CalculateUtility()
	{
		if (_controller.GetRelativePlayerBody() != null &&
			(IsVisibleToPlayer() || Time.time < _enterTime + _utilityDelay))
		{
			return 50f;
		}

		return -100f;
	}

	private bool IsVisibleToPlayer()
	{
		Bounds meshBounds = _controller.GetComponentInChildren<SkinnedMeshRenderer>().bounds;
		var camera = Locator.GetPlayerCamera();
		Plane[] camPlanes = camera.GetFrustumPlanes();
		float dot = Vector3.Dot(camera.transform.forward,
			_controller.transform.position - camera.transform.position);
		return dot > 0 && GeometryUtility.TestPlanesAABB(camPlanes, meshBounds) &&
			_controller.GetDistanceToPlayer() < 50f;
	}

	protected override void OnEnterAction()
	{
		_utilityDelay = Random.Range(60f, 90f);
		
		_controller.CurrentSpeed = _controller.BaseSpeed * 5f;
		_controller.MoveToPreviousTarget();
		
		_effects.PlayRoar();
		_effects.SetLoopsEnabled(true);
		_effects.SetLightEnabled(true);
	}
	
	public override bool Update_Action()
	{
		if (IsVisibleToPlayer() || Time.time < _enterTime + _utilityDelay)
		{
			return true;
		}

		return false;
	}

	public override void OnArriveAtTarget()
	{
		_controller.MoveToPreviousTarget();
	}
}