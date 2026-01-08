using UnityEngine;

namespace ErnestoChase;

public class ControllableErnestoBody : OWRigidbody
{
	public override void SetPosition(Vector3 worldPosition)
	{
		if (PlayerState.IsAttached() && Locator.GetPlayerBody().GetComponentInParent<ControllableErnesto>())
		{
			base.SetPosition(worldPosition);
			GlobalMessenger.FireEvent("PlayerRepositioned");
			return;
		}
		base.SetPosition(worldPosition);
	}
}