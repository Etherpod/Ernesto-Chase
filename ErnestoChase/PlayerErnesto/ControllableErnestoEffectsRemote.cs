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
	private SingularityWarpEffect _blackHolePrefab;
	[SerializeField]
	private SingularityWarpEffect _whiteHolePrefab;
	
	private SkinnedMeshRenderer _ernestoRenderer;
	private SingularityWarpEffect _blackHole;
	private SingularityWarpEffect _whiteHole;
	private float _baseLightRange;
	private float _baseLightIntensity;
	private Texture _bulbTexture;

	private float _filterLerp = 1f;

	private bool _stealthMode;
	private bool _quantumMode;
	private bool _disableLight;

	private void Awake()
	{
		_ernestoRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
		_baseLightRange = _anglerLight.range;
		_baseLightIntensity = _anglerLight.intensity;
		_bulbTexture = _ernestoRenderer.material.GetTexture("_EmissionMap");

		Files.AssetBundleUtilities.ReplaceShaders(_blackHolePrefab.gameObject);
		Files.AssetBundleUtilities.ReplaceShaders(_whiteHolePrefab.gameObject);
		_blackHolePrefab._warpedObjectGeometry = _ernestoMesh;
		_whiteHolePrefab._warpedObjectGeometry = _ernestoMesh;
		_blackHole = Instantiate(_blackHolePrefab);
		_whiteHole = Instantiate(_whiteHolePrefab);

		_ernestoMesh.transform.localScale = Vector3.zero;
		_anglerLight.range = _baseLightRange * _ernestoMesh.transform.localScale.x *
			_scaleRoot.transform.localScale.x;
	}
	
	private void Start()
	{
		if (_disableLight)
		{
			_anglerLight.intensity = 0f;
			_ernestoRenderer.material.SetTexture("_EmissionMap", _noBulbTexture);
		}
        
		//StartCoroutine(InitAudioFiles(state.DataStates.Keys.ToArray()));
	}
	
	private void Update()
	{
		if (!_disableLight)
		{
			_anglerLight.range = _baseLightRange * _ernestoMesh.transform.localScale.x *
				_scaleRoot.transform.localScale.x;
		}
        
		UpdateMuffle();
	}
	
	public void CreateWhiteHole()
	{
		_whiteHole.transform.parent = transform.parent;
		_whiteHole.transform.localPosition = transform.localPosition;
		_whiteHole.WarpObjectIn(2f);
		_whiteHole.singularityController.OnCreation += OnWhiteHoleCreated;
	}

	private void OnWhiteHoleCreated()
	{
		_whiteHole.singularityController.OnCreation -= OnWhiteHoleCreated;
		
		_animator.SetTrigger("Impulse");
		_oneShotAudio.PlayOneShot(AudioType.DBAnglerfishDetectTarget, 0.8f);
		_loopingAudio.AssignAudioLibraryClip(AudioType.DBAnglerfishChasing_LP);
		if (!_quantumMode && !_stealthMode)
		{
			_loopingAudio.FadeIn(1f);
			_musicAudio.Play();
		}
		UpdateMuffle(true);
	}
	
	public void CreateBlackHole()
	{
		_blackHole.transform.parent = transform.parent;
		_blackHole.transform.localPosition = transform.localPosition;
		_blackHole.WarpObjectOut(2f);
		_blackHole.singularityController.OnCollapse += OnBlackHoleCollapse;
		_loopingAudio.FadeOut(1f);
		_musicAudio.FadeOut(1f, OWAudioSource.FadeOutCompleteAction.PAUSE);
	}
	
	private void OnBlackHoleCollapse()
	{
		_blackHole.singularityController.OnCollapse -= OnBlackHoleCollapse;
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
		_musicAudio.pitch = mult;
	}
	
	public void RefreshEffects()
	{
		_oneShotAudio.PlayOneShot(AudioType.DBAnglerfishDetectTarget, 0.8f);
        
		if (!_stealthMode)
		{
			_loopingAudio.Play();
			_musicAudio.Play();
		}

		_animator.enabled = true;
		_animator.SetTrigger("Impulse");
	}

	public Transform GetScaleRoot()
	{
		return _scaleRoot.transform;
	}
}