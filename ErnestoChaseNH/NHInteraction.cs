using ErnestoChase;
using NewHorizons.Handlers;
using UnityEngine;

namespace ErnestoChaseNH;

public class NHInteraction : MonoBehaviour, INHInteraction
{
	private void Start()
	{
		ErnestoChase.ErnestoChase.Instance.SetNHInterface(this);
	}
	
	public string GetNameFromAstroID(string astroID) => 
		ShipLogHandler.GetNameFromAstroID(astroID);
}