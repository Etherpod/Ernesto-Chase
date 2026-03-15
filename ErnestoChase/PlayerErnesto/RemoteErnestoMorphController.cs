using System.Linq;
using UnityEngine;
using ErnestoChase.ErnestoAI;

namespace ErnestoChase.PlayerErnesto;

public class RemoteErnestoMorphController : MonoBehaviour
{
    private Transform _ernesto;
    private ErnestoState _state;
    private ErnestoEffects _effects;
    private bool _shrinked;
    private bool _changingSize;
    private float _sizeStartTime;
    private float _lastSize;
    private readonly float _sizeChangeLength = 1.5f;
    private readonly float _shrinkSize = 0.1f;

    private void Awake()
    {
        _ernesto = GetComponentInChildren<ErnestoManager>().transform;
        _state = _ernesto.GetComponent<ErnestoState>();
        _effects = _ernesto.GetComponent<ErnestoEffects>();
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

            if (!_state.ErnestoReleased)
            {
                _effects.CreateWhiteHole();
            }
            else
            {
                _effects.DebugRefresh();
            }
        }
    }

    private void UpdateSize()
    {
        float timeLerp = Mathf.InverseLerp(_sizeStartTime, _sizeStartTime + _sizeChangeLength, Time.fixedTime);
        float scale = Mathf.SmoothStep(_lastSize, _shrinked ? _shrinkSize : 1f, timeLerp);
        _ernesto.localScale = Vector3.one * scale;
        float pitchMult = Mathf.Lerp(1f, 1.5f, timeLerp);
        _effects.SetAudioPitchMultiplier(pitchMult);

        if (timeLerp == 1)
        {
            _changingSize = false;
            enabled = false;

            if (!_shrinked && !ErnestoChase.Instance.ErnestoMorph)
            {
                _state.KillVolumeEnabled = true;
            }
        }
    }

    public void SetSize(bool shrink, bool instant = false)
    {
        if (instant)
        {
            _state.KillVolumeEnabled = !_shrinked && !ErnestoChase.Instance.ErnestoMorph;
            float scale = _shrinked ? _shrinkSize : 1f;
            _ernesto.localScale = Vector3.one * scale;
            float pitchMult = _shrinked ? 1.5f : 1f;
            _effects.SetAudioPitchMultiplier(pitchMult);
            enabled = false;
        }
        else if (_shrinked != shrink)
        {
            if (_shrinked)
            {
                _state.KillVolumeEnabled = false;
            }
            
            _lastSize = _ernesto.localScale.x;
            _sizeStartTime = Time.fixedTime;
            _changingSize = true;
            enabled = true;
        }
        
        _shrinked = shrink;
    }
}
