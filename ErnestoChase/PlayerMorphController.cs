using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ErnestoChase;

public class PlayerMorphController : MonoBehaviour
{
	[SerializeField] private OWRigidbody _ernestoBody;
	[SerializeField] private ControllableErnesto _ernestoController;

	private OWRigidbody _playerBody;
	private bool _morphed;
	private bool _gameStopped;

	private void Awake()
	{
		_playerBody = gameObject.GetAttachedOWRigidbody();
		_ernestoBody.OnUnsuspendOWRigidbody += OnErnestoUnsuspended;
	}
	
	private void Start()
	{
		_morphed = false;
		_ernestoBody.Suspend(transform, _playerBody);
		_ernestoBody.transform.localPosition = Vector3.zero;
		_ernestoController.SetActive(false);
	}

	private void Update()
	{
		if (Keyboard.current.mKey.wasPressedThisFrame)
		{
			SetMorphed(!_morphed);
		}
	}

	public void SetMorphed(bool morphed)
	{
		if (morphed == _morphed ||
			(!_morphed && (OWInput.GetInputMode() != InputMode.Character ||
			!Locator.GetPlayerCamera().enabled)))
		{
			return;
		}
		
		if (morphed)
		{
			ErnestoMorph();
		}
		else
		{
			PlayerMorph();
		}
		
		if (ErnestoChase.InMultiplayer)
		{
			foreach (var id in ErnestoChase.Players)
			{
				QSBCompat.SendErnestoMorph(id, morphed);
			}
		}
	}

	public void ErnestoMorph()
	{
		_ernestoController.SetActive(true);
		_ernestoBody.Unsuspend(false);
	}

	private void OnErnestoUnsuspended(OWRigidbody body)
	{
		if (_gameStopped) return;
		
		_ernestoBody.SetRotation(Quaternion.LookRotation(Locator.GetPlayerCamera().transform.forward,
			Locator.GetPlayerCamera().transform.up));
		_ernestoBody._rigidbody.angularDrag = 0.94f;
		_ernestoController.AttachPlayer();
		foreach (var renderer in Locator.GetPlayerBody().GetComponentsInChildren<Renderer>())
		{
			renderer.forceRenderingOff = true;
		}
		_morphed = true;
	}

	public void PlayerMorph()
	{
		_ernestoController.DetachPlayer();
		foreach (var renderer in Locator.GetPlayerBody().GetComponentsInChildren<Renderer>())
		{
			renderer.forceRenderingOff = false;
		}
		_playerBody.SetRotation(Quaternion.LookRotation(_ernestoBody.transform.forward,
			_ernestoBody.transform.up));
		_ernestoBody.Suspend(transform, _playerBody);
		_ernestoController.SetActive(false);
		_morphed = false;
	}

	public bool IsMorphed() => _morphed;

	public void OnGameStopped()
	{
		if (_gameStopped) return;
		
		if (_morphed)
		{
			SetMorphed(false);
		}

		_gameStopped = true;
		enabled = false;
	}

	private void OnDestroy()
	{
		_ernestoBody.OnUnsuspendOWRigidbody -= OnErnestoUnsuspended;
	}
}