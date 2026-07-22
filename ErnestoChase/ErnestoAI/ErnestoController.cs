using System;
using System.Collections.Generic;
using UnityEngine;

namespace ErnestoChase.ErnestoAI;

public class ErnestoController : MonoBehaviour
{
	private List<TargetData> _targets = [];
	private int _targetIndex;

	private readonly float _maxTargetDistanceThreshold = 2f;
	private readonly float _minTargetDistanceThreshold = 0.2f;
	private readonly float _maximumTargetSpawnDelay = 1f;
	private readonly float _minimumTargetSpawnDelay = 0.25f;
	private Vector3 _lastPlayerPos;
	private float _lastTargetSpawnTime;

	private OWRigidbody _relativePlayerBody;
	private ForceVolume _activePlayerGravity;
	private OWRigidbody _ernestoBody;
	private OWRigidbody _staticTransformBody;

	private Vector3 _lastPosition;
	private Quaternion _lastRotation;
	private float _lastTargetAdvanceTime;
	private float _adjustedTargetCount;
	private int _targetIndexDelta = 1;
	private bool _createTeleportOnBodyChange;

	private MovementType _activeMovementType;
	private RigidbodyMode _rigidbodyMode;

	public float BaseSpeed { get; private set; }
	public float CurrentSpeed;

	private enum MovementType
	{
		Interpolation,
		Rigidbody,
		None
	}

	public enum RigidbodyMode
	{
		Direct,
		Relative
	}

	private void Start()
	{
		PatchnestoClass.OnPlayerForceVolumeActivated += OnPlayerForceVolumeActivated;
		PatchnestoClass.OnPlayerForceVolumeDeactivated += OnPlayerForceVolumeDeactivated;
		PatchnestoClass.OnPlayerWarped += OnPlayerWarped;
		
		var body = ErnestoChase.LoadPrefab("Assets/ErnestoChase/ErnestoBody.prefab");
		_ernestoBody = Instantiate(body, Vector3.zero, Quaternion.identity).GetComponent<OWRigidbody>();
		GameObject targetsParent = ErnestoChase.LoadPrefab("Assets/ErnestoChase/SpaceTargetsParent.prefab");
		_staticTransformBody = Instantiate(targetsParent).GetComponent<OWRigidbody>();
		BaseSpeed = 5f;

		RecalculateRelativeBody();
		/*foreach (var vol in Locator.GetPlayerForceDetector()._activeVolumes)
		{
			OnPlayerForceVolumeActivated(vol as ForceVolume);
		}*/
	}

	private void FixedUpdate()
	{
		RecalculateRelativeBody();
		TryCreateTarget();

		if (_activeMovementType == MovementType.Interpolation)
		{
			InterpolateToNextTarget();
			
			_adjustedTargetCount = Mathf.MoveTowards(
				_adjustedTargetCount, 
				_targets.Count - 1 - _targetIndex, 
				Mathf.Abs(_targets.Count - 1 - _targetIndex - _adjustedTargetCount) * 
				Time.fixedDeltaTime / 1.5f);
		}
		else if (_activeMovementType == MovementType.Rigidbody)
		{
			UpdateRigidbody();
		}

		if (_relativePlayerBody != null)
		{
			// big jump when relative planet set?
			_lastPlayerPos = _relativePlayerBody.transform
				.InverseTransformPoint(Locator.GetPlayerTransform().position);
		}
	}

	private void TryCreateTarget()
	{
		if (_relativePlayerBody == null) return;
		
		// initialize targets
		if (_targets.Count == 0)
		{
			CreateTargetAtPlayer();
			_targetIndex = 0;
			return;
		}
		
		// get relative position to planet and transform to world
		var position = Locator.GetPlayerTransform().position;
		var distSqr = (position - _targets[_targets.Count - 1].worldPosition).sqrMagnitude;
		
		// player hasn't moved
		if (distSqr >= _minTargetDistanceThreshold * _minTargetDistanceThreshold &&
			Time.time >= _lastTargetSpawnTime + _maximumTargetSpawnDelay)
		{
			CreateTargetAtPlayer();
			return;
		}
		
		// player moved enough
		if (distSqr >= _maxTargetDistanceThreshold * _maxTargetDistanceThreshold &&
			Time.time >= _lastTargetSpawnTime + _minimumTargetSpawnDelay)
		{
			CreateTargetAtPlayer();
		}
	}

	public TargetData CreateTargetAtPlayer()
	{
		var worldPos = Locator.GetPlayerTransform().position;
		var data = new TargetData(_relativePlayerBody.transform, worldPos);
		_targets.Add(data);
		_lastTargetSpawnTime = Time.time;
		return data;
	}

	public void MoveToNextTarget()
	{
		ErnestoChase.WriteDebugMessage("advance target");
		
		if (_activeMovementType != MovementType.Interpolation)
		{
			SetMovementType(MovementType.Interpolation);
		}

		_targetIndexDelta = 1;
		AdvanceTarget();
	}
	
	public void MoveToPreviousTarget()
	{
		ErnestoChase.WriteDebugMessage("rewind target");
		
		if (_activeMovementType != MovementType.Interpolation)
		{
			SetMovementType(MovementType.Interpolation);
		}

		_targetIndexDelta = -1;
		AdvanceTarget();
	}

	public void SkipTargets(int numTargets)
	{
		_targetIndex = Mathf.Clamp(_targetIndex + _targetIndexDelta * numTargets, 
			0, _targets.Count - 1);
	}

	public void SnapToNextTarget()
	{
		var nextIndex = _targetIndex + _targetIndexDelta;
		if (nextIndex < 0 || nextIndex > _targets.Count - 1)
		{
			return;
		}
		
		_targetIndex = nextIndex;
		var target = _targets[_targetIndex];
		if (target.relativeParent != transform.parent)
		{
			transform.parent = target.relativeParent;
		}
		transform.localPosition = target.localPosition;

		_lastPosition = transform.localPosition;
		_lastRotation = transform.rotation;
		_lastTargetAdvanceTime = Time.time;

		_adjustedTargetCount = _targets.Count - 1 - _targetIndex;
		
		GetComponent<ErnestoManager>().OnArriveAtTarget();
	}

	private void InterpolateToNextTarget()
	{
		var nextIndex = _targetIndex + _targetIndexDelta;
		if (nextIndex < 0 || nextIndex > _targets.Count - 1)
		{
			SetMovementType(MovementType.None);
			return;
		}

		var target = _targets[nextIndex];
		
		Vector3 targetPos;
		if (target.relativeParent == transform.parent)
		{
			targetPos = target.localPosition;
		}
		else
		{
			targetPos = transform.parent.InverseTransformPoint(target.worldPosition);
		}
        
        float dist = (targetPos - _lastPosition).magnitude;
        float speed = CurrentSpeed;

        if (nextIndex > 1 && nextIndex < _targets.Count - 1)
        {
	        var lookaheadTarget = _targets[nextIndex + _targetIndexDelta];
	        var toCurrent = targetPos - _lastPosition;
	        Vector3 lookaheadPos;
	        if (lookaheadTarget.relativeParent == transform.parent)
	        {
		        lookaheadPos = lookaheadTarget.localPosition;
	        }
	        else
	        {
		        lookaheadPos = transform.parent.InverseTransformPoint(lookaheadTarget.worldPosition);
	        }

	        var toLookahead = targetPos - lookaheadPos;
	        var dot = Vector3.Dot(toCurrent.normalized, toLookahead.normalized);
	        var angleSpeedMult = Mathf.Lerp(1f, 0.4f, Mathf.Sqrt((dot + 1f) / 2f));
	        speed *= angleSpeedMult;
        }

        float positionLerp = Mathf.InverseLerp(_lastTargetAdvanceTime, 
	        _lastTargetAdvanceTime + dist / speed, Time.time);

        if (dist < 0.01f)
        {
            positionLerp = 1f;
        }

        if (positionLerp < 1f)
        {
            transform.localPosition = Vector3.Lerp(_lastPosition, targetPos, positionLerp);

            Quaternion nextRotation = Quaternion.LookRotation(
	            gameObject.GetAttachedOWRigidbody().transform.TransformPoint(targetPos) - transform.position,
                -_activePlayerGravity?
	                .CalculateForceAccelerationAtPoint(transform.position) ?? 
                Locator.GetPlayerTransform().up);
            transform.rotation = Quaternion.Slerp(_lastRotation, nextRotation, positionLerp);
        }
        else
        {
	        GetComponent<ErnestoManager>().OnArriveAtTarget();
	        //SetMovementType(MovementType.None);
        }
	}
	
	public void MoveRigidbodyToPlayer(RigidbodyMode mode)
	{
		SetMovementType(MovementType.Rigidbody);
		_rigidbodyMode = mode;
	}

	private void UpdateRigidbody()
	{
		transform.LookAt(Locator.GetPlayerTransform(), Locator.GetPlayerTransform().up);
		
		if (_rigidbodyMode == RigidbodyMode.Relative)
		{
			_ernestoBody.SetVelocity(transform.forward * CurrentSpeed + 
				Locator.GetPlayerBody().GetVelocity());
		}
		else if (_rigidbodyMode == RigidbodyMode.Direct)
		{
			_ernestoBody.AddForce(transform.forward * CurrentSpeed);
		}
	}

	public void DisableMovement()
	{
		SetMovementType(MovementType.None);
	}
	
	private void SetMovementType(MovementType type)
	{
		_activeMovementType = type;
		
		if (type == MovementType.Interpolation)
		{
			transform.parent = _targets[_targetIndex].relativeParent;
		}
		
		if (type == MovementType.Rigidbody)
		{
			_ernestoBody.gameObject.SetActive(true);
			_ernestoBody.SetPosition(transform.position);
			_ernestoBody.SetVelocity(gameObject.GetAttachedOWRigidbody().GetVelocity());
			transform.parent = _ernestoBody.transform;
		}
		else
		{
			_ernestoBody.gameObject.SetActive(false);
		}
	}

	public void AdvanceTarget()
	{
		var nextIndex = _targetIndex + _targetIndexDelta;
		if (nextIndex < 0 || nextIndex > _targets.Count - 1) return;
		
		_lastPosition = transform.localPosition;
		_lastRotation = transform.rotation;
		_lastTargetAdvanceTime = Time.time;
		_targetIndex = nextIndex;
	}

	private void OnPlayerForceVolumeActivated(ForceVolume vol)
	{
		ErnestoChase.WriteDebugMessage("force activated: " + vol);
		
		if (TrySetActiveGravity(vol))
		{
			return;
		}
		
		if (_activePlayerGravity != null && TrySetActiveGravity(_activePlayerGravity))
		{
			return;
		}
		
		if (vol is ZeroGVolume zeroGVol && TrySetActiveGravity(zeroGVol))
		{
			return;
		}

		RecalculateRelativeBody();
	}
	
	private void OnPlayerForceVolumeDeactivated(ForceVolume vol)
	{
		ErnestoChase.WriteDebugMessage("remove vol " + vol.name);
		if (vol == _activePlayerGravity)
		{
			RecalculateRelativeBody();
		}
	}

	private bool TrySetActiveGravity(ForceVolume vol)
	{
		if (!vol.GetAffectsAlignment(Locator.GetPlayerBody()))
		{
			return false;
		}

		SetActiveGravity(vol);

		return false;
	}
	
	private bool TrySetActiveGravity(ZeroGVolume vol)
	{
		var attachedBody = vol.GetAttachedOWRigidbody();
		if (attachedBody.IsKinematic())
		{
			SetActiveGravity(vol);
			return true;
		}

		return false;
	}

	private void SetActiveGravity(ForceVolume vol)
	{
		var attachedBody = vol.GetAttachedOWRigidbody();
		if (attachedBody.IsKinematic())
		{
			bool newBody = _relativePlayerBody != attachedBody;
			_relativePlayerBody = attachedBody;
			_activePlayerGravity = vol;
			if (newBody && _createTeleportOnBodyChange)
			{
				var data = CreateTargetAtPlayer();
				data.isTeleport = true;
				CreateTargetAtPlayer();
				ErnestoChase.WriteDebugMessage("second half created on body " + data.relativeParent.name);
				_createTeleportOnBodyChange = false;
			}
		}
	}
	
	public void RecalculateRelativeBody()
	{
		ForceVolume gravVol = null;
		ZeroGVolume zeroGVol = null;

		AlignmentForceDetector detector = Locator.GetPlayerForceDetector();
		if (detector._trackedLayers.Count > 0)
		{
			foreach (int num in detector._trackedLayers.Keys)
			{
				if (detector._trackedLayers[num].volumes.Count == 0) continue;

				foreach (PriorityVolume priorityVolume in detector._trackedLayers[num].volumes)
				{
					if (priorityVolume is ForceVolume volume && volume.GetAttachedOWRigidbody().IsKinematic())
					{
						if (gravVol == null && volume.GetAffectsAlignment(Locator.GetPlayerBody()))
						{
							gravVol = volume;
						}
						else if (zeroGVol == null && volume is ZeroGVolume zg)
						{
							zeroGVol = zg;
						}
					}
				}

				ForceVolume activeVol = gravVol ?? zeroGVol;
				if (activeVol != null)
				{
					SetActiveGravity(activeVol);
					return;
				}
			}
		}

		if (PlayerState.InBrambleDimension())
		{
			_activePlayerGravity = null;
		}

		if (!_createTeleportOnBodyChange && gravVol == null && _relativePlayerBody != null)
		{
			CreateTargetAtPlayer();
			CreateTargetAtPlayer().isTeleport = true;
			ErnestoChase.WriteDebugMessage("teleport created for entering space");
			_createTeleportOnBodyChange = true;
		}

		_relativePlayerBody = null;
	}

	private void OnPlayerWarped()
	{
		if (_relativePlayerBody == null || _createTeleportOnBodyChange) return;
		
		CreateTargetAtPlayer();
		var data = CreateTargetAtPlayer();
		data.isTeleport = true;
		ErnestoChase.WriteDebugMessage("teleport created on body " + data.relativeParent.name);
		_createTeleportOnBodyChange = true;
	}

	public OWRigidbody GetRelativePlayerBody()
	{
		return _relativePlayerBody;
	}

	public float GetDistanceToPlayer()
	{
		return (Locator.GetPlayerTransform().position - transform.position).magnitude;
	}

	public TargetData GetCurrentTarget()
	{
		if (_targets.Count == 0 || _targetIndex < 0 || 
			_targetIndex >= _targets.Count)
		{
			return null;
		}

		return _targets[_targetIndex];
	}
	
	public TargetData GetNextTarget()
	{
		if (_targets.Count == 0 || _targetIndex < 0 || 
			_targetIndex >= _targets.Count - 1)
		{
			return null;
		}

		return _targets[_targetIndex + _targetIndexDelta];
	}

	public int GetRemainingTargetCount()
	{
		return _targets.Count - 1 - _targetIndex;
	}

	public float GetRemainingTargetProgress()
	{
		float interpolationProgress = 0f;
		
		if (_activeMovementType == MovementType.Interpolation &&
			_targetIndex > 0 && _targetIndex < _targets.Count - 1)
		{
			var target = _targets[_targetIndex + _targetIndexDelta];
			Vector3 targetPos;
			if (target.relativeParent == transform.parent)
			{
				targetPos = target.localPosition;
			}
			else
			{
				targetPos = transform.parent.InverseTransformPoint(target.worldPosition);
			}
			float dist = (targetPos - _lastPosition).magnitude;
			float speed = CurrentSpeed;
			interpolationProgress = 1 - Mathf.InverseLerp(_lastTargetAdvanceTime, 
				_lastTargetAdvanceTime + (dist / speed), Time.time);
		}
		
		return _adjustedTargetCount + interpolationProgress - 1;
	}

	private void OnDestroy()
	{
		PatchnestoClass.OnPlayerForceVolumeActivated -= OnPlayerForceVolumeActivated;
		PatchnestoClass.OnPlayerForceVolumeDeactivated -= OnPlayerForceVolumeDeactivated;
		PatchnestoClass.OnPlayerWarped -= OnPlayerWarped;
	}
}

public class TargetData
{
	public Vector3 worldPosition => relativeParent.TransformPoint(localPosition);
	public Vector3 localPosition;
	public Transform relativeParent;
	public int[] sectors;
	public bool isTeleport;
	public SpeedOverrideData speedOverride;
	// action change event?

	public class SpeedOverrideData
	{
		public float speedOverride;
		public bool reset;

		public SpeedOverrideData()
		{
			reset = true;
		}
			
		public SpeedOverrideData(float speed)
		{
			speedOverride = speed;
		}
	}

	public TargetData(Transform parent, Vector3 worldPos, int[] sectorIDs = null, 
		bool teleport = false, SpeedOverrideData speedOverrideData = null)
	{
		relativeParent = parent;
		localPosition = parent.InverseTransformPoint(worldPos);
		sectors = sectorIDs;
		isTeleport = teleport;
		speedOverride = speedOverrideData;
	}
}