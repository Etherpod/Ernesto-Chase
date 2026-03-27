using ErnestoChase.PlayerErnesto;

namespace ErnestoChase.Spectating;

public class MorpherSpectatorCamera : SpectatorCamera
{
	public delegate void SpectatorCameraSwitchEvent(bool toErnesto);
	public event SpectatorCameraSwitchEvent OnSwitchCamera;
	
	public uint PlayerID { get; private set; }
	public OWCamera PlayerCamera => _playerCamera;
	
	private ControllableErnestoRemote _ernestoController;
	private OWCamera _playerCamera;

	protected override void Awake()
	{
		base.Awake();
		_ernestoController = GetComponent<ControllableErnestoRemote>();
		_ernestoController.OnMorph += OnMorph;
	}

	public void SetPlayerCamera(uint id, OWCamera cam)
	{
		PlayerID = id;
		_playerCamera = cam;
	}

	private void OnMorph(bool morphed)
	{
		OnSwitchCamera?.Invoke(morphed);
	}

	public override bool CanSpectate()
	{
		return _ernestoController != null && _ernestoController.CanSpectate();
	}

	private void OnDestroy()
	{
		_ernestoController.OnMorph -= OnMorph;
	}
}