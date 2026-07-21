using UnityEngine;

namespace ErnestoChase.ErnestoAI;

public class IdleAction : ErnestoAction
{
	public override Name GetName() => Name.Idle;

	public override float CalculateUtility()
	{
		return 0;
	}

	public override bool Update_Action()
	{
		return true;
	}
}