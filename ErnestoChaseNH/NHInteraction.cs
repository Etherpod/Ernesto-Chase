using ErnestoChase;
using NewHorizons.External;
using NewHorizons.Handlers;
using ErnestoChase.Interaction;
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