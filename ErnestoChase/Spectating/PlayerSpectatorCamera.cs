namespace ErnestoChase.Spectating;

public class PlayerSpectatorCamera : SpectatorCamera
{
	public uint PlayerID { get; private set; }
	
	public void AssignPlayer(uint id, SectorDetector detector)
	{
		PlayerID = id;
		_sectorDetector = detector;
	}
	
	public override bool CanSpectate()
	{
		return !ErnestoChase.QSBAPI.GetPlayerDead(PlayerID);
	}
}