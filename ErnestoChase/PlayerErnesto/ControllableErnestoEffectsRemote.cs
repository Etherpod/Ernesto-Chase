using System.Collections.Generic;
using UnityEngine;

namespace ErnestoChase.PlayerErnesto;

public class ControllableErnestoEffectsRemote : MonoBehaviour
{
	[SerializeField]
	private OWAudioSource _loopingAudio = null;
	[SerializeField]
	private OWAudioSource _oneShotAudio = null;
	[SerializeField]
	private OWAudioSource _musicAudio = null;
	[SerializeField]
	private AudioLowPassFilter[] _lowPassFilters = [];
	[SerializeField]
	private Animator _animator = null;
	[SerializeField]
	private GameObject _scaleRoot = null;
	[SerializeField]
	private GameObject _ernestoMesh = null;
	[SerializeField]
	private Light _anglerLight = null;
	[SerializeField]
	private Texture2D _noBulbTexture = null;
	[SerializeField]
	private SingularityWarpEffect _blackHole;
	[SerializeField]
	private SingularityWarpEffect _whiteHole;
	
	private SkinnedMeshRenderer _ernestoRenderer;
	private float _baseLightRange;
	private float _baseLightIntensity;
	private Texture _bulbTexture;
	
	private readonly List<SingularityController.SingularityEffectEvent> _whiteHoleListeners = [];
	private readonly List<SingularityController.SingularityEffectEvent> _blackHoleListeners = [];

	private float _filterLerp = 1f;

	private bool _lightEnabled;
	private bool _stealthEnabled;
	private bool _quantumMode;

	private void Awake()
	{
		_ernestoRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
		_baseLightRange = _anglerLight.range;
		_baseLightIntensity = _anglerLight.intensity;
		_bulbTexture = _ernestoRenderer.material.GetTexture("_EmissionMap");

		_whiteHole.transform.parent = transform.parent;
		_blackHole.transform.parent = transform.parent;
		_whiteHole.gameObject.SetActive(true);
		_blackHole.gameObject.SetActive(true);

		_ernestoMesh.transform.localScale = Vector3.zero;
		_anglerLight.range = _baseLightRange * _ernestoMesh.transform.localScale.x *
			_scaleRoot.transform.localScale.x;
	}
	
	private void Start()
	{
		if (_lightEnabled)
		{
			_anglerLight.intensity = 0f;
			_ernestoRenderer.material.SetTexture("_EmissionMap", _noBulbTexture);
		}
        
		//StartCoroutine(InitAudioFiles(state.DataStates.Keys.ToArray()));
	}
	
	private void Update()
	{
		if (!_lightEnabled)
		{
			_anglerLight.range = _baseLightRange * _ernestoMesh.transform.localScale.x *
				_scaleRoot.transform.localScale.x;
		}
        
		UpdateMuffle();
	}
	
	public void CreateWhiteHole(SingularityController.SingularityEffectEvent createAction = null)
	{
		_whiteHole.WarpObjectIn(2f);
		_whiteHole.singularityController.OnCreation += OnWhiteHoleCreated;
		
		if (createAction != null)
		{
			_whiteHole.singularityController.OnCreation += createAction;
			_whiteHoleListeners.Add(createAction);
		}
	}

	private void OnWhiteHoleCreated()
	{
		_whiteHole.singularityController.OnCreation -= OnWhiteHoleCreated;
		_whiteHoleListeners.ForEach(a => 
			_whiteHole.singularityController.OnCreation -= a);
		_whiteHoleListeners.Clear();
		
		_animator.SetTrigger("Impulse");
		_oneShotAudio.PlayOneShot(AudioType.DBAnglerfishDetectTarget, 0.8f);
		_loopingAudio.AssignAudioLibraryClip(AudioType.DBAnglerfishChasing_LP);
		if (!_quantumMode && !_stealthEnabled)
		{
			_loopingAudio.FadeIn(1f);
			_musicAudio.FadeIn(0f);
		}
		UpdateMuffle(true);
	}
	
	public void CreateBlackHole(SingularityController.SingularityEffectEvent createAction = null)
	{
		_blackHole.WarpObjectOut(2f);
		_blackHole.singularityController.OnCreation += OnBlackHoleCreated;
		
		if (createAction != null)
		{
			_blackHole.singularityController.OnCreation += createAction;
			_blackHoleListeners.Add(createAction);
		}
		
		_loopingAudio.FadeOut(1f);
		_musicAudio.FadeOut(1f, OWAudioSource.FadeOutCompleteAction.PAUSE);
	}
	
	private void OnBlackHoleCreated()
	{
		_blackHole.singularityController.OnCollapse -= OnBlackHoleCreated;
		_blackHoleListeners.ForEach(a => 
			_blackHole.singularityController.OnCreation -= a);
		_blackHoleListeners.Clear();
	}
	
	private void UpdateMuffle(bool instant = false)
	{
		var toPlayer = Locator.GetPlayerTransform().position - transform.position;
		float distMult = Mathf.InverseLerp(50f * 50f, 500f * 500f, toPlayer.sqrMagnitude);
		bool muffle = false;
		if (distMult < 1f && Locator.GetAudioMixer()._playerInReverbVolume)
		{
			muffle = Physics.Raycast(transform.position, toPlayer, toPlayer.magnitude - 1f, 
				OWLayerMask.physicalMask);
		}

		if (instant)
		{
			_filterLerp = muffle ? 1f : 0f;
		}
		else
		{
			_filterLerp = Mathf.MoveTowards(_filterLerp, muffle ? 1f : 0f, Time.deltaTime / 4f);
		}
        
		foreach (var filter in _lowPassFilters)
		{
			filter.cutoffFrequency = Mathf.Lerp(22000, 3000, Mathf.Lerp(Mathf.Sqrt(_filterLerp), 1f, distMult));
		}
	}
	
	public void SetAudioPitchMultiplier(float mult)
	{
		_loopingAudio.pitch = mult;
		_oneShotAudio.pitch = mult;
		//_musicAudio.pitch = mult;
	}
	
	public void SetLightEnabled(bool lightEnabled)
	{
		_lightEnabled = lightEnabled;
		_anglerLight.intensity = lightEnabled ? _baseLightIntensity : 0f;
		_ernestoRenderer.material.SetTexture("_EmissionMap", 
			lightEnabled ? _bulbTexture : _noBulbTexture);
	}

	public void SetStealthEnabled(bool stealthEnabled)
	{
		_stealthEnabled = stealthEnabled;
		if (stealthEnabled)
		{
			_loopingAudio.FadeOut(2f);
			_musicAudio.FadeOut(2f, OWAudioSource.FadeOutCompleteAction.PAUSE);
		}
		else
		{
			_oneShotAudio.PlayOneShot(AudioType.DBAnglerfishDetectTarget, 0.8f);
			_loopingAudio.FadeIn(1f);
			_musicAudio.FadeIn(0f);
		}
	}
	
	public void RefreshEffects()
	{
		_oneShotAudio.PlayOneShot(AudioType.DBAnglerfishDetectTarget, 0.8f);
        
		if (!_stealthEnabled)
		{
			_loopingAudio.FadeIn(0f);
			_musicAudio.FadeIn(0f);
		}

		_animator.enabled = true;
		_animator.SetTrigger("Impulse");
	}

	public Transform GetScaleRoot()
	{
		return _scaleRoot.transform;
	}
}