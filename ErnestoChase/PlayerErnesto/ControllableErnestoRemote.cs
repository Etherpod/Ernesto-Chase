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
    private float _lastSize;
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
            SetSize(_shrinked, true);
            _ernesto.gameObject.SetActive(false);
            
            var ernestoRends = _ernesto.GetComponentsInChildren<Renderer>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)
                .Where(rend => !ernestoRends.Contains(rend)))
            {
                renderer.forceRenderingOff = false;
            }
        }
        else
        {
            var ernestoRends = _ernesto.GetComponentsInChildren<Renderer>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)
                .Where(rend => !ernestoRends.Contains(rend)))
            {
                renderer.forceRenderingOff = true;
            }
            
            _ernesto.gameObject.SetActive(true);
            
            _effects.CreateWhiteHole();

            /*if (!_state.ErnestoReleased)
            {
                _effects.CreateWhiteHole();
            }
            else
            {
                _effects.RefreshEffects();
            }*/
        }

        _morphed = morphed;
    }

    private void UpdateSize()
    {
        float timeLerp = Mathf.InverseLerp(_sizeStartTime, _sizeStartTime + _sizeChangeLength, Time.fixedTime);
        float scale = Mathf.SmoothStep(_lastSize, _shrinked ? _shrinkSize : 1f, timeLerp);
        _ernesto.localScale = Vector3.one * scale;
        float pitchMult = Mathf.Lerp(1f, 1.5f, Mathf.InverseLerp(1f, _shrinkSize, scale));
        _effects.SetAudioPitchMultiplier(pitchMult);

        if (timeLerp == 1)
        {
            _changingSize = false;
            enabled = false;

            ErnestoChase.WriteDebugMessage("Finish size change");
            if (!_shrinked && !ErnestoChase.Instance.ErnestoMorph)
            {
                ErnestoChase.WriteDebugMessage("Enable kill volume");
                KillVolumeEnabled = true;
            }
        }
    }

    public void SetSize(bool shrink, bool instant = false)
    {
        if (instant)
        {
            ErnestoChase.WriteDebugMessage("Set kill volume: " + (!_shrinked && !ErnestoChase.Instance.ErnestoMorph));
            KillVolumeEnabled = !_shrinked && !ErnestoChase.Instance.ErnestoMorph;
            float scale = _shrinked ? _shrinkSize : 1f;
            _ernesto.localScale = Vector3.one * scale;
            float pitchMult = _shrinked ? 1.5f : 1f;
            _effects.SetAudioPitchMultiplier(pitchMult);
            enabled = false;
        }
        else if (_shrinked != shrink)
        {
            // if not shrinked then going to shrink
            if (!_shrinked)
            {
                ErnestoChase.WriteDebugMessage("Disable kill volume");
                KillVolumeEnabled = false;
            }
            
            _lastSize = _ernesto.localScale.x;
            _sizeStartTime = Time.fixedTime;
            _changingSize = true;
            enabled = true;
        }
        
        _shrinked = shrink;
    }

    public bool CanSpectate(bool ernesto)
    {
        return ernesto == _morphed;
    }
}
