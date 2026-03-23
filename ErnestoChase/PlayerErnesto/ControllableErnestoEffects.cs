using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

namespace ErnestoChase.PlayerErnesto;

public class ControllableErnestoEffects : MonoBehaviour
{
	[SerializeField]
	private Transform _scaleRoot = null;
	[SerializeField]
	private OWAudioSource _loopingAudio = null;
	[SerializeField]
	private OWAudioSource _oneShotAudio = null;
	[SerializeField]
	private OWAudioSource _musicAudio = null;
	[SerializeField]
	private Light _anglerLight = null;
	[SerializeField]
	private Texture2D _noBulbTexture = null;
	[SerializeField]
	private Animator _animator;
	[SerializeField]
	private SingularityWarpEffect _blackHole;
	[SerializeField]
	private SingularityWarpEffect _whiteHole;

	private SkinnedMeshRenderer _ernestoRenderer;
	private float _baseLightRange;
	private float _baseLightIntensity;
	private Texture _bulbTexture;
	private readonly List<AudioClip> _musicClips = [];

	private readonly List<SingularityController.SingularityEffectEvent> _whiteHoleListeners = [];
	private readonly List<SingularityController.SingularityEffectEvent> _blackHoleListeners = [];

	private bool _stealthEnabled;
	private bool _quantumMode;
	private bool _lightEnabled = true;

	private void Awake()
	{
		_ernestoRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
		_baseLightRange = _anglerLight.range;
		_baseLightIntensity = _anglerLight.intensity;
		_bulbTexture = _bulbTexture = _ernestoRenderer.material.GetTexture("_EmissionMap");

		// inactive in editor to spare my eyes
		_whiteHole.gameObject.SetActive(true);
		_blackHole.gameObject.SetActive(true);
		_anglerLight.range = _baseLightRange * _scaleRoot.transform.localScale.x;
	}

	private void Start()
	{
		StartCoroutine(InitAudioFiles());
	}
	
	private void Update()
	{
		if (OWInput.IsNewlyPressed(InputLibrary.flashlight))
		{
			SetLightEnabled(!_lightEnabled);
		}

		if (Keyboard.current.hKey.wasPressedThisFrame)
		{
			SetStealthEnabled(!_stealthEnabled);
		}
		
		if (_lightEnabled)
		{
			_anglerLight.range = _baseLightRange * _scaleRoot.transform.localScale.x;
		}
	}
	
	private IEnumerator InitAudioFiles()
	{
		string filePath = Path.Combine(ErnestoChase.Instance.ModHelper.Manifest.ModFolderPath, "ErnestoMusic");
		string[] fileTypes = [".mp3", ".ogg", ".wav"];
        
		List<string> files = [];
            
		foreach (var type in fileTypes)
		{
			var foundFiles = Directory.GetFiles(filePath, $"ErnestoMorph_*{type}", SearchOption.AllDirectories);
			if (foundFiles.Length > 0)
			{
				files.AddRange(foundFiles);
				continue;
			}
            
			//files.AddRange(Directory.GetFiles(filePath, $"*{type}", SearchOption.AllDirectories));
		}

		if (files.Count > 0)
		{
			foreach (var file in files)
			{
				var request = UnityWebRequestMultimedia.GetAudioClip("file:///" + file, UnityEngine.AudioType.UNKNOWN);
				yield return request.SendWebRequest();

				if (request.isDone && !request.isNetworkError)
				{
					_musicClips.Add(DownloadHandlerAudioClip.GetContent(request));
				}
			}
        
			SelectRandomMusicClip();
		}
		else
		{
			yield return null;
		}
	}
	
	private void SelectRandomMusicClip()
	{
		var rand = new System.Random();
		int index = rand.Next(0, _musicClips.Count);
		var clip = _musicClips[index];
        
		_musicAudio.clip = clip;
	}
	
	public void CreateWhiteHole(SingularityController.SingularityEffectEvent createAction = null)
	{
		if (_whiteHole.singularityController.enabled) return;
		
		var length = Mathf.Max(2f - (_whiteHole._singularityCreationLength + 
			_whiteHole._singularityCollapseLength), 0f);
		_whiteHole.singularityController.CreateWithLifetime(length);
		
		if (createAction != null)
		{
			_whiteHole.singularityController.OnCreation += createAction;
			_whiteHoleListeners.Add(createAction);
		}
		_whiteHole.singularityController.OnCreation += OnWhiteHoleCreated;
	}

	private void OnWhiteHoleCreated()
	{
		_whiteHole.singularityController.OnCreation -= OnWhiteHoleCreated;
		_whiteHoleListeners.ForEach(a => 
			_whiteHole.singularityController.OnCreation -= a);
		_whiteHoleListeners.Clear();
		
		//_animator.SetTrigger("Impulse");
		_oneShotAudio.PlayOneShot(AudioType.DBAnglerfishDetectTarget, 0.8f);
		_loopingAudio.AssignAudioLibraryClip(AudioType.DBAnglerfishChasing_LP);
		if (!_quantumMode && !_stealthEnabled)
		{
			_loopingAudio.FadeIn(1f);
			_musicAudio.FadeIn(0f);
		}
	}
	
	public void CreateBlackHole(SingularityController.SingularityEffectEvent createAction = null)
	{
		if (_blackHole.singularityController.enabled) return;
		
		var length = Mathf.Max(2f - (_blackHole._singularityCreationLength + 
			_blackHole._singularityCollapseLength), 0f);
		_blackHole.singularityController.CreateWithLifetime(length);
		
		if (createAction != null)
		{
			_blackHole.singularityController.OnCreation += createAction;
			_blackHoleListeners.Add(createAction);
		}
		_blackHole.singularityController.OnCreation += OnBlackHoleCreated;
		
		_loopingAudio.FadeOut(1f);
		_musicAudio.FadeOut(1f, OWAudioSource.FadeOutCompleteAction.PAUSE);
	}
	
	private void OnBlackHoleCreated()
	{
		_blackHole.singularityController.OnCreation -= OnBlackHoleCreated;
		_blackHoleListeners.ForEach(a => 
			_blackHole.singularityController.OnCreation -= a);
		_blackHoleListeners.Clear();
		_blackHole.singularityController.CollapseImmediate();
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

		if (ErnestoChase.InMultiplayer)
		{
			foreach (var id in ErnestoChase.Players)
			{
				QSBCompat.SendMorphEffectsInput(id, lightEnabled, toggleLight: true);
			}
		}
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
		
		if (ErnestoChase.InMultiplayer)
		{
			foreach (var id in ErnestoChase.Players)
			{
				QSBCompat.SendMorphEffectsInput(id, stealthEnabled, toggleStealth: true);
			}
		}
	}

	public Transform GetScaleRoot() => _scaleRoot;
}