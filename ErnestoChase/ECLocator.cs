using ErnestoChase.PlayerErnesto;

namespace ErnestoChase;

public static class ECLocator
{
	private static PlayerMorphController _morphController = null;

	public static void Initialize()
	{
		if (ErnestoChase.Instance.ErnestoMorph)
		{
			_morphController = Locator.GetPlayerTransform().GetComponentInChildren<PlayerMorphController>();
		}
	}

	public static PlayerMorphController GetMorphController()
	{
		return _morphController;
	}
}