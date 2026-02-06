using UnityEngine;

namespace ErnestoChase;

public class ErnestoFluidDetector : DynamicFluidDetector
{
	public override void AddDrag(FluidVolume fluidVolume, float fractionSubmerged)
	{
		if (fluidVolume.AllowShipAutoroll())
		{
			Vector3 vector = fluidVolume.GetAttachedOWRigidbody().GetPosition() - _owRigidbody.GetPosition();
			Vector3 forward = _owRigidbody.transform.forward;
			float num = Vector3.Angle(vector, forward);
			num = 1f - Mathf.Abs(num - 90f) / 90f;
			Vector3 vector2 = Vector3.Cross(forward, vector);
			Vector3 right = _owRigidbody.transform.right;
			float num2 = Vector3.Angle(vector2, right) * Mathf.Sign(Vector3.Dot(_owRigidbody.transform.up, vector2));
			num2 = Mathf.Min(num2, 90f) * 0.015f * num;
			_netAngularAcceleration += forward * num2;
		}
		base.AddDrag(fluidVolume, fractionSubmerged);
	}
}