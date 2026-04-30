using ErnestoChase.ErnestoAI;
using UnityEngine;

namespace ErnestoChase.Spectating;

public class ErnestoSpectatorCamera : SpectatorCamera
{
	[SerializeField]
	private ErnestoManager _ernestoManager;

	protected override void Start()
	{
		base.Start();
		ErnestoChase.SpectateManager.ernestoSpectatorCams.Add(this);
	}

	public override bool CanSpectate()
	{
		return _ernestoManager != null && _ernestoManager.CanSpectate();
	}
}