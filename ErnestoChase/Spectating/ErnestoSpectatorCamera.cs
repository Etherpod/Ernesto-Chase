using ErnestoChase.ErnestoAI;
using UnityEngine;

namespace ErnestoChase.Spectating;

public class ErnestoSpectatorCamera : SpectatorCamera
{
	[SerializeField]
	private ErnestoManager _ernestoManager;

	public override bool CanSpectate()
	{
		return _ernestoManager != null && _ernestoManager.CanSpectate();
	}
}