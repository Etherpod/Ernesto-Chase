using UnityEngine;

namespace ErnestoChase.PlayerErnesto;

public class AlignWithWarpTarget : AlignWithDirection
{
	public override Vector3 GetAlignmentDirection()
	{
		if (Locator.GetReferenceFrame() == null)
		{
			return _currentDirection;
		}

		return Locator.GetReferenceFrame().GetPosition() - _owRigidbody.GetWorldCenterOfMass();
	}

	public override bool CheckAlignmentRequirements()
	{
		return true;
	}
}