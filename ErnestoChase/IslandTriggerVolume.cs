using UnityEngine;

namespace ErnestoChase;

public class IslandTriggerVolume : MonoBehaviour
{
	private OWTriggerVolume _triggerVolume;
	private OWRigidbody _rigidbody;

	private void Start()
	{
		_triggerVolume = GetComponent<OWTriggerVolume>();
		_rigidbody = gameObject.GetAttachedOWRigidbody();
		if (_rigidbody != null)
		{
			_triggerVolume.OnEntry += OnEntry;
			_triggerVolume.OnExit += OnExit;
		}
	}

	private void OnEntry(GameObject hitObj)
	{
		if (hitObj.CompareTag("PlayerDetector"))
		{
			ErnestoChase.ActiveIslands.Insert(0, _rigidbody);
		}
	}

	private void OnExit(GameObject hitObj)
	{
		if (hitObj.CompareTag("PlayerDetector") && 
			ErnestoChase.ActiveIslands.Contains(_rigidbody))
		{
			ErnestoChase.ActiveIslands.Remove(_rigidbody);
		}
	}

	private void OnDestroy()
	{
		if (_rigidbody != null)
		{
			if (ErnestoChase.ActiveIslands.Contains(_rigidbody))
			{
				ErnestoChase.ActiveIslands.Remove(_rigidbody);
			}
			
			_triggerVolume.OnEntry -= OnEntry;
			_triggerVolume.OnExit -= OnExit;
		}
	}
}