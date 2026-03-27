namespace ErnestoChase.Spectating;

public class PlayerSpectatorCamera : SpectatorCamera
{
	public uint PlayerID { get; private set; }
	
	public void AssignPlayerID(uint id)
	{
		PlayerID = id;
		_sectorDetector = ErnestoChase.QSBInteraction.GetRemoteFluidDetector(PlayerID).GetAddComponent<SectorDetector>();
		_sectorDetector.SetOccupantType(DynamicOccupant.Player);
	}
	
	public override bool CanSpectate()
	{
		return !ErnestoChase.QSBAPI.GetPlayerDead(PlayerID);
	}
}