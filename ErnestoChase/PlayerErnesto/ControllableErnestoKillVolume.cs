using System;
using UnityEngine;

namespace ErnestoChase.PlayerErnesto;

[RequireComponent(typeof(OWTriggerVolume))]
public class ControllableErnestoKillVolume : MonoBehaviour
{
	private OWTriggerVolume _killVolume;
	private ControllableErnestoRemote _controller;
	private bool _playerCollided;
	
	private void Start()
	{
		_controller = GetComponentInParent<ControllableErnestoRemote>();
		_killVolume = gameObject.GetRequiredComponent<OWTriggerVolume>();
		
		_killVolume.OnEntry += OnEntry;
		_killVolume.OnExit += OnExit;
	}

	private void FixedUpdate()
	{
		if (_controller.KillVolumeEnabled && _playerCollided &&
			!PlayerState.IsDead())
		{
			Locator.GetDeathManager().KillPlayer(DeathType.Digestion);
		}
	}

	private void OnEntry(GameObject hitObj)
	{
		if (hitObj.CompareTag("PlayerDetector"))
		{
			_playerCollided = true;
		}
	}
	
	private void OnExit(GameObject hitObj)
	{
		if (hitObj.CompareTag("PlayerDetector"))
		{
			_playerCollided = false;
		}
	}

	private void OnDestroy()
	{
		_killVolume.OnEntry -= OnEntry;
		_killVolume.OnExit -= OnExit;
	}
}