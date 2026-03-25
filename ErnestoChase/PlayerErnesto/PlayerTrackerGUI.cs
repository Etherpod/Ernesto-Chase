using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ErnestoChase.PlayerErnesto;

public class PlayerTrackerGUI : MonoBehaviour
{
	[SerializeField]
	private Canvas _canvas = null;
	[SerializeField]
	private Transform _trackerParent = null;
	[SerializeField]
	private GameObject _playerTrackerPrefab = null;
	[SerializeField]
	private Transform _indicatorParent = null;
	[SerializeField]
	private GameObject _offScreenIndicatorPrefab = null;
	[SerializeField]
	private float _dampening = 1f;

	private OWCamera _activeCam;
	private bool _showVisuals;
	private readonly List<TrackedPlayerData> _trackedPlayers = [];

	private class TrackedPlayerData
	{
		public readonly uint playerID;
		public readonly GameObject body;
		public readonly PlayerTrackerTarget tracker;
		public readonly OffScreenIndicator indicator;
		public Vector3 currentTrackerPos;
		public Vector3 targetTrackerPos;

		public TrackedPlayerData(uint id, PlayerTrackerTarget trackerObj, OffScreenIndicator indicatorObj)
		{
			playerID = id;
			body = ErnestoChase.QSBAPI.GetPlayerBody(id);
			tracker = trackerObj;
			indicator = indicatorObj;
			targetTrackerPos = body.transform.position;
			currentTrackerPos = targetTrackerPos;
		}

		public void Disable()
		{
			Destroy(tracker.gameObject);
			Destroy(indicator.gameObject);
		}
	}

	private void Awake()
	{
		GlobalMessenger<OWCamera>.AddListener("SwitchActiveCamera", OnSwitchActiveCamera);
		GlobalMessenger<DeathType>.AddListener("PlayerDeath", OnPlayerDeath);
		GlobalMessenger.AddListener("ChangeGUIMode", OnChangeGUIMode);
		_playerTrackerPrefab.SetActive(false);
		_offScreenIndicatorPrefab.SetActive(false);
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

		UpdatePlayers();
		UpdateTrackers();
	}

	private void UpdatePlayers()
	{
		for (int i = 0; i < _trackedPlayers.Count; i++)
		{
			if (!ErnestoChase.AlivePlayers.Contains(_trackedPlayers[i].playerID))
			{
				_trackedPlayers[i].Disable();
				_trackedPlayers.RemoveAt(i);
			}
		}

		foreach (var id in ErnestoChase.AlivePlayers)
		{
			if (_trackedPlayers.All(data => data.playerID != id))
			{
				var tracker = Instantiate(_playerTrackerPrefab, 
					_trackerParent).GetComponent<PlayerTrackerTarget>();
				tracker.gameObject.SetActive(true);
				
				var indicator = Instantiate(_offScreenIndicatorPrefab, 
					_indicatorParent).GetComponent<OffScreenIndicator>();
				indicator.enabled = false;
				indicator.gameObject.SetActive(true);
				indicator.enabled = true;
				
				var data = new TrackedPlayerData(id, tracker, indicator);
				_trackedPlayers.Add(data);
			}
		}
	}

	
	// TODO: test the new non-recttransform parents to see if it fixes the particles not following the players
	private void UpdateTrackers()
	{
		if (_trackedPlayers.Count == 0) return;
		
		foreach (var player in _trackedPlayers)
		{
			if (player.body == null) continue;
			
			bool showTracker = false;
			player.targetTrackerPos = player.body.transform.position;
			
			player.tracker.SetScreenSize(GetScreenSize(player.body, _activeCam), _canvas.scaleFactor);

			var t = _dampening <= 0.01f ? 1f : Time.deltaTime / _dampening;
			var nextPos = Vector3.Lerp(player.currentTrackerPos, 
				player.targetTrackerPos, t);
			player.currentTrackerPos = nextPos;
			var canvasPos = _canvas.WorldToCanvasPosition(_activeCam, nextPos);
			player.tracker.GetRequiredComponent<RectTransform>().anchoredPosition = canvasPos;
			
			if (canvasPos.x >= 0f && canvasPos.x <= _canvas.pixelRect.width / _canvas.scaleFactor && 
				canvasPos.y >= 0f && canvasPos.y <= _canvas.pixelRect.height / _canvas.scaleFactor && canvasPos.z > 0f)
			{
				showTracker = true;
			}

			if (showTracker)
			{
				if (player.indicator != null)
				{
					player.indicator.gameObject.SetActive(false);
				}
				if (!player.tracker.gameObject.activeSelf)
				{
					player.tracker.gameObject.SetActive(true);
				}
			}
			else
			{
				if (player.indicator != null)
				{
					player.indicator.gameObject.SetActive(true);
					player.indicator.SetCanvasPosition(player.currentTrackerPos);
				}
				
				if (player.tracker.gameObject.activeSelf)
				{
					player.tracker.gameObject.SetActive(false);
				}
			}
		}
	}
	
	private void SetVisibility(bool visible)
	{
		_showVisuals = visible;
		if (!_showVisuals)
		{
			foreach (var player in _trackedPlayers)
			{
				player.indicator.gameObject.SetActive(false);
			}
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

	private float GetScreenSize(GameObject target, OWCamera camera)
	{
		float num = Vector3.Distance(target.transform.position, camera.transform.position);
		var radius = 5f;
		return Mathf.Clamp(radius * (1f / Mathf.Tan(camera.fieldOfView * 0.017453292f * 0.5f) / num), 
			0.125f, 0.5f) * camera.pixelHeight;
	}
	
	private void OnSwitchActiveCamera(OWCamera activeCam)
	{
		_activeCam = activeCam;
	}
	
	private void OnChangeGUIMode()
	{
		if (GUIMode.IsHiddenMode())
		{
			foreach (var player in _trackedPlayers)
			{
				player.tracker.gameObject.SetActive(false);
				player.indicator.gameObject.SetActive(false);
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