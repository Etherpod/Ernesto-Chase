using ErnestoChase.PlayerErnesto;
using UnityEngine;

namespace ErnestoChase.Spectating;

public class MorpherSpectatorCamera : SpectatorCamera
{
	public delegate void SpectatorCameraSwitchEvent(MorpherSpectatorCamera cam, OWCamera prevCam);
	public event SpectatorCameraSwitchEvent OnSwitchCamera;
	
	public uint PlayerID { get; private set; }
	
	public override OWCamera Camera => GetActiveCamera();
	public OWCamera ErnestoCamera => _owCamera;
	public OWCamera PlayerCamera => _playerCamera;
	
	public override SectorDetector Detector => GetActiveDetector();
	public SectorDetector ErnestoDetector => _sectorDetector;
	public SectorDetector PlayerDetector => _playerDetector;

	public override AudioListener AudioListener => GetActiveAudioListener();
	public AudioListener ErnestoAudioListener => _audioListener;
	public AudioListener PlayerAudioListener => _playerAudioListener;
	
	private ControllableErnestoRemote _ernestoController;
	private OWCamera _playerCamera;
	private SectorDetector _playerDetector;
	private AudioListener _playerAudioListener;
	private PlayerSectorTrackerRemote _sectorTracker;

	protected override void Start()
	{
		base.Start();
		_ernestoController = GetComponent<ControllableErnestoRemote>();
		_ernestoController.OnMorph += OnMorph;
		ErnestoChase.SpectateManager.ernestoSpectatorCams.Add(this);
		_sectorTracker = _ernestoController.GetComponent<PlayerSectorTrackerRemote>();
	}

	public void SetErnestoCamera(OWCamera cam, SectorDetector detector)
	{
		_owCamera = cam;
		_sectorDetector = detector;
		_audioListener = cam.GetComponent<AudioListener>();
	}

	public void SetPlayerCamera(uint id, OWCamera cam, SectorDetector detector)
	{
		PlayerID = id;
		_playerCamera = cam;
		_playerDetector = detector;
		_playerAudioListener = cam.GetComponent<AudioListener>();
	}

	private void OnMorph(bool morphed)
	{
		// I don't think this runs when the object is disabled. try putting this on the player body to prevent disabling
		
		ErnestoChase.WriteDebugMessage("morphed: " + morphed);
		_sectorTracker.SetSectorDetector(GetActiveDetector());
		OnSwitchCamera?.Invoke(this, morphed ? PlayerCamera : ErnestoCamera);
	}

	public void SetDetectorActive(bool active)
	{
		if (_ernestoController.IsMorphed())
		{
			ErnestoDetector.gameObject.SetActive(active);
		}
	}

	public OWCamera GetActiveCamera()
	{
		return _ernestoController.IsMorphed() ? ErnestoCamera : PlayerCamera;
	}

	public SectorDetector GetActiveDetector()
	{
		return _ernestoController.IsMorphed() ? ErnestoDetector : PlayerDetector;
	}

	public AudioListener GetActiveAudioListener()
	{
		return _ernestoController.IsMorphed() ? ErnestoAudioListener : PlayerAudioListener;
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