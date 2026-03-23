using System.Linq;
using UnityEngine;

namespace ErnestoChase.PlayerErnesto;

public class PlayerTrackerGUI : MonoBehaviour
{
	[SerializeField]
	private Canvas _canvas = null;
	[SerializeField]
	private Canvas _offScreenIndicatorCanvas = null;
	[SerializeField]
	private Transform _trackerRing = null;
	[SerializeField]
	private OffScreenIndicator _offScreenIndicator = null;

	private OWCamera _activeCam;
	private bool _showVisuals;
	private GameObject[] _guiObjects;

	private void Awake()
	{
		GlobalMessenger<OWCamera>.AddListener("SwitchActiveCamera", OnSwitchActiveCamera);
		GlobalMessenger<DeathType>.AddListener("PlayerDeath", OnPlayerDeath);
		GlobalMessenger.AddListener("ChangeGUIMode", OnChangeGUIMode);
		_guiObjects = new []
		{
			_trackerRing.gameObject,
			_offScreenIndicator.gameObject
		};
		SetVisibility(false);
	}
	
	private void Update()
	{
		UpdateVisibility();
		if (GUIMode.IsHiddenMode() || _activeCam == null || !_showVisuals ||
			!ErnestoChase.InMultiplayer)
		{
			return;
		}

		UpdateTrackerRing();
	}

	private void UpdateTrackerRing()
	{
		var players = ErnestoChase.AlivePlayers
			.Select(p => ErnestoChase.QSBAPI.GetPlayerBody(p)).ToArray();
		
		if (players.Length == 0 && _offScreenIndicator.gameObject.activeSelf)
		{
			_offScreenIndicator.gameObject.SetActive(false);
		}

		bool showTracker = false;
		if (players.Length > 0 && players[0] != null)
		{
			var canvasPos = _canvas.WorldToCanvasPosition(_activeCam, players[0].transform.position);
			_trackerRing.GetRequiredComponent<RectTransform>().anchoredPosition = canvasPos;
			if (canvasPos.x >= 0f && canvasPos.x <= _canvas.pixelRect.width / _canvas.scaleFactor && 
				canvasPos.y >= 0f && canvasPos.y <= _canvas.pixelRect.height / _canvas.scaleFactor && canvasPos.z > 0f)
			{
				showTracker = true;
			}

			if (showTracker)
			{
				if (_offScreenIndicator != null)
				{
					_offScreenIndicator.gameObject.SetActive(false);
				}
				if (!_trackerRing.gameObject.activeSelf)
				{
					_trackerRing.gameObject.SetActive(true);
				}
			}
			else
			{
				if (_offScreenIndicator != null)
				{
					_offScreenIndicator.gameObject.SetActive(true);
					_offScreenIndicator.SetCanvasPosition(players[0].transform.position);
				}
				
				if (_trackerRing.gameObject.activeSelf)
				{
					_trackerRing.gameObject.SetActive(false);
				}
			}
		}
	}
	
	private void SetVisibility(bool visible)
	{
		_showVisuals = visible;
		if (!_showVisuals)
		{
			_offScreenIndicator.gameObject.SetActive(false);
		}
		_canvas.enabled = visible;
	}

	private void UpdateVisibility()
	{
		bool flag = ECLocator.GetMorphController()?.IsMorphed() ?? false;
		if (_showVisuals != flag)
		{
			SetVisibility(flag);
		}
	}
	
	private void OnSwitchActiveCamera(OWCamera activeCam)
	{
		_activeCam = activeCam;
	}
	
	private void OnChangeGUIMode()
	{
		if (GUIMode.IsHiddenMode() && _guiObjects != null)
		{
			foreach (var obj in _guiObjects)
			{
				obj.SetActive(false);
			}
		}
	}
	
	private void OnPlayerDeath(DeathType type)
	{
		enabled = false;
		_activeCam = null;
	}
	
	private void OnEnable()
	{
		UpdateVisibility();
	}
	
	private void OnDisable()
	{
		UpdateVisibility();
	}
	
	private void OnDestroy()
	{
		GlobalMessenger<OWCamera>.RemoveListener("SwitchActiveCamera", OnSwitchActiveCamera);
		GlobalMessenger<DeathType>.RemoveListener("PlayerDeath", OnPlayerDeath);
		GlobalMessenger.RemoveListener("ChangeGUIMode", OnChangeGUIMode);
	}
}