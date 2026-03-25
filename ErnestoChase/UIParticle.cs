using UnityEngine;

namespace ErnestoChase;

public class UIParticle : MonoBehaviour
{
	public delegate void DestroyEvent();
	public event DestroyEvent OnDestroy;
	
	public float lifetime;
	public Gradient colorOverLifetime;

	private MeshRenderer _renderer;
	private Color _initialColor;
	private float _initialScale;
	private float _currentLifetime;

	private void Awake()
	{
		_renderer = GetComponent<MeshRenderer>();
		_initialColor = _renderer.material.color;
		_initialScale = transform.localScale.x;
	}

	private void Start()
	{
		_renderer.material.color = _initialColor * colorOverLifetime.Evaluate(0f);
	}

	public void Update()
	{
		if (_currentLifetime >= lifetime)
		{
			OnDestroy?.Invoke();
		}
		else
		{
			_currentLifetime += Time.deltaTime;
			_renderer.material.color = _initialColor * colorOverLifetime.Evaluate(_currentLifetime / lifetime);
		}
	}

	public void SetScale(float scale)
	{
		transform.localScale = Vector3.one * (_initialScale * scale);
	}
}