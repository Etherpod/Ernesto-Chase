using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ErnestoChase.PlayerErnesto;

public class PlayerMorphController : MonoBehaviour
{
	[SerializeField] private OWRigidbody _ernestoBody;
	[SerializeField] private ControllableErnesto _ernestoController;

	private OWRigidbody _playerBody;
	private KeyInfoPromptController _keyInfo;
	private bool _morphed;
	private bool _gameStopped;

	private void Awake()
	{
		_playerBody = gameObject.GetAttachedOWRigidbody();
		_ernestoBody.OnUnsuspendOWRigidbody += OnErnestoUnsuspended;
	}
	
	private void Start()
	{
		GlobalMessenger.AddListener("EC_GameStopped", OnGameStopped);
		
		_keyInfo = GameObject.FindWithTag("Global").GetComponent<KeyInfoPromptController>();
		_morphed = false;
		_ernestoBody.Suspend(transform, _playerBody);
		_ernestoBody.transform.localPosition = Vector3.zero;
		_ernestoController.SetActive(false);
	}

	private void Update()
	{
		if (Keyboard.current.mKey.wasPressedThisFrame && !_ernestoController.IsWarping())
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
			_ernestoController.GetEffects().CreateWhiteHole(ErnestoMorph);
		}
		else
		{
			_ernestoController.GetEffects().CreateBlackHole(PlayerMorph);
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
		
		Locator.GetPlayerController().LockMovement();
		Locator.GetFlashlight().TurnOff(false);
		
		if (_keyInfo._displayCodePrompt)
		{
			_keyInfo._codePrompt.SetVisibility(false);
			_keyInfo._displayCodePrompt = false;
		}
		
		_ernestoController.GetEffects().CreateWhiteHole();
		
		_morphed = true;
	}

	public void PlayerMorph()
	{
		if (_ernestoController.IsWarping())
		{
			_ernestoController.CancelWarp();
		}
		
		_ernestoController.DetachPlayer();
		foreach (var renderer in Locator.GetPlayerBody().GetComponentsInChildren<Renderer>())
		{
			renderer.forceRenderingOff = false;
		}
		_playerBody.SetRotation(Quaternion.LookRotation(_ernestoBody.transform.forward,
			_ernestoBody.transform.up));
		_ernestoBody.Suspend(transform, _playerBody);
		_ernestoController.SetActive(false);
		Locator.GetPlayerController().UnlockMovement();
		_morphed = false;
		
		Locator.GetFlashlight().TurnOn(false);
	}

	public bool IsMorphed() => _morphed;

	private void OnGameStopped()
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
		GlobalMessenger.RemoveListener("EC_GameStopped", OnGameStopped);
		_ernestoBody.OnUnsuspendOWRigidbody -= OnErnestoUnsuspended;
	}
}