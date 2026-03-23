using System.Linq;
using UnityEngine;

namespace ErnestoChase.PlayerErnesto;

public class ControllableErnestoRemote : MonoBehaviour
{
    public bool KillVolumeEnabled { get; private set; }
    
    private Transform _ernesto;
    private ControllableErnestoEffectsRemote _effects;
    private bool _morphed;
    private bool _shrinked;
    private bool _changingSize;
    private float _sizeStartTime;
    private float _lastSize = 1f;
    private readonly float _sizeChangeLength = 1.5f;
    private readonly float _shrinkSize = 0.1f;

    private void Awake()
    {
        _effects = GetComponentInChildren<ControllableErnestoEffectsRemote>();
        _ernesto = _effects.transform;
    }

    private void Start()
    {
        _ernesto.gameObject.SetActive(false);
        ErnestoChase.Instance.ModHelper.Events.Unity.FireInNUpdates(() =>
        {
            _ernesto.localPosition = Vector3.zero;
            _ernesto.localRotation = Quaternion.identity;
        }, 10);
        enabled = false;
    }

    private void FixedUpdate()
    {
        if (_changingSize)
        {
            UpdateSize();
        }
    }

    public void SetMorphed(bool morphed)
    {
        ErnestoChase.WriteDebugMessage("SET MORPHED REMOTE: " + morphed);
        
        if (!morphed)
        {
            _effects.CreateBlackHole(PlayerMorph);
        }
        else
        {
            _ernesto.gameObject.SetActive(true);
            _effects.CreateWhiteHole(ErnestoMorph);
        }

        _morphed = morphed;
    }

    private void PlayerMorph()
    {
        SetSize(_shrinked, true);
        _ernesto.gameObject.SetActive(false);
            
        var ernestoRends = _ernesto.GetComponentsInChildren<Renderer>();
        foreach (var renderer in GetComponentsInChildren<Renderer>(true)
            .Where(rend => !ernestoRends.Contains(rend)))
        {
            renderer.forceRenderingOff = false;
        }
    }

    private void ErnestoMorph()
    {
        var ernestoRends = _ernesto.GetComponentsInChildren<Renderer>();
        foreach (var renderer in GetComponentsInChildren<Renderer>(true)
            .Where(rend => !ernestoRends.Contains(rend)))
        {
            renderer.forceRenderingOff = true;
        }
    }

    private void UpdateSize()
    {
        float timeLerp = Mathf.InverseLerp(_sizeStartTime, _sizeStartTime + _sizeChangeLength, Time.fixedTime);
        float scale = Mathf.SmoothStep(_lastSize, _shrinked ? _shrinkSize : 1f, timeLerp);
        _effects.GetScaleRoot().localScale = Vector3.one * scale;
        float pitchMult = Mathf.Lerp(1f, 1.5f, Mathf.InverseLerp(1f, _shrinkSize, scale));
        _effects.SetAudioPitchMultiplier(pitchMult);

        if (timeLerp == 1)
        {
            _changingSize = false;
            enabled = false;
            
            if (!_shrinked && !ErnestoChase.Instance.ErnestoMorph)
            {
                KillVolumeEnabled = true;
            }
        }
    }

    public void SetSize(bool shrink, bool instant = false)
    {
        if (instant)
        {
            KillVolumeEnabled = !_shrinked && !ErnestoChase.Instance.ErnestoMorph;
            float scale = _shrinked ? _shrinkSize : 1f;
            _effects.GetScaleRoot().localScale = Vector3.one * scale;
            float pitchMult = _shrinked ? 1.5f : 1f;
            _effects.SetAudioPitchMultiplier(pitchMult);
            enabled = false;
        }
        else if (_shrinked != shrink)
        {
            // if not shrinked then going to shrink
            if (!_shrinked)
            {
                KillVolumeEnabled = false;
            }
            
            float timeLerp = Mathf.InverseLerp(_sizeStartTime, _sizeStartTime + _sizeChangeLength, Time.fixedTime);
            float scale = Mathf.SmoothStep(_lastSize, _shrinked ? _shrinkSize : 1f, timeLerp);
            
            _lastSize = scale;
            _sizeStartTime = Time.fixedTime;
            _changingSize = true;
            enabled = true;
        }
        
        _shrinked = shrink;
    }

    public void OnWarpEvent(bool warpStart)
    {
        if (warpStart)
        {
            _effects.CreateBlackHole();
        }
        else
        {
            _effects.CreateWhiteHole();
        }
    }

    public bool CanSpectate(bool ernesto)
    {
        return ernesto == _morphed;
    }

    public ControllableErnestoEffectsRemote GetEffects() => _effects;
}
