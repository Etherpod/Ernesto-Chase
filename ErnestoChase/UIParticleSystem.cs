using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEditor;

namespace ErnestoChase;

public class UIParticleSystem : MonoBehaviour
{
	[SerializeField]
	private Canvas _canvas = null;
	[SerializeField]
	private Mesh _particleMesh = null;
	[SerializeField]
	private Material _particleMat = null;
	[SerializeField]
	private int _textureSubdivisions = 1;
	[SerializeField]
	private float _spawnRadius = 1f;
	[SerializeField]
	[Range(0f, 1f)]
	private float _phase = 0f;
	[SerializeField]
	private float _particleScale = 1f;
	[SerializeField]
	private float _spawnRate = 1f;
	[SerializeField]
	private float _lifetime = 5f;
	[SerializeField]
	private int _maxParticles = 10;
	[SerializeField]
	private Gradient _colorOverLifetime = null;
	[SerializeField]
	[Range(0f, 1f)]
	private float _alpha = 1f;
	[SerializeField]
	private float _noiseStrength = 1f;
	[SerializeField]
	private float _noiseScale = 1f;
	[SerializeField]
	private float _noiseScrollSpeed = 1f;
	/*[SerializeField]
	private MeshRenderer _noiseDisplayRenderer = null;*/
	
	//private Texture2D _noiseTexture = null;
	
	private readonly List<UIParticle> _activeParticles = [];
	private float _spawnDelay;
	private Vector3 _noiseOffset;
	private float _lastParentScale = 1f;
	
	private void Start()
	{
		if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
		//_spawnDelay = 1f / _spawnRate;
	}
	
	public void Update()
	{
		if (_particleMesh == null || _particleMat == null) return;
		
		if (_spawnDelay <= 0f)
		{
			if (_activeParticles.Count < _maxParticles)
			{
				var newParticle = new GameObject("UIParticle");
				newParticle.transform.parent = transform;
				newParticle.transform.localPosition = Random.insideUnitCircle * (_spawnRadius * 2f);
				newParticle.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-360f, 360f) * _phase);
				newParticle.transform.localScale = Vector3.one * _particleScale;

				var mesh = new Mesh();
				mesh.vertices = _particleMesh.vertices;
				mesh.triangles = _particleMesh.triangles;
				mesh.uv = _particleMesh.uv;
				var newUvs = new Vector2[mesh.uv.Length];

				int numCells = (int)Mathf.Pow(2, _textureSubdivisions);
				var offset = new Vector2(Random.Range(0, numCells), Random.Range(0, numCells));
				for (int i = 0; i < newUvs.Length; i++)
				{
					newUvs[i] = (mesh.uv[i] + offset) / numCells;
				}

				mesh.uv = newUvs;
				newParticle.AddComponent<MeshFilter>().mesh = mesh;

				var mat = new Material(_particleMat);
				mat.color = Random.ColorHSV(0f, 1f, 0.3f, 0.5f, _alpha, _alpha);
				newParticle.AddComponent<MeshRenderer>().material = mat;

				var p = newParticle.AddComponent<UIParticle>();
				p.lifetime = _lifetime;
				p.colorOverLifetime = _colorOverLifetime;
				p.SetScale(1f / _lastParentScale);
				p.OnDestroy += () =>
				{
					_activeParticles.Remove(p);
					Destroy(p.gameObject);
				};
			
				_activeParticles.Add(p);
			}

			_spawnDelay = 1f / _spawnRate;
		}
		else
		{
			_spawnDelay -= Time.deltaTime;
		}

		/*if (_noiseTexture == null)
		{
			_noiseTexture = new Texture2D(256, 256);
			_noiseDisplayRenderer.material.mainTexture = _noiseTexture;
		}

		Color[] pixels = new Color[_noiseTexture.GetPixels().Length];
		for (int x = 0; x < _noiseTexture.width; x++)
		{
			for (int y = 0; y < _noiseTexture.height; y++)
			{
				float xCoord = x / (float)_noiseTexture.width * _noiseScale;
				float yCoord = y / (float)_noiseTexture.height * _noiseScale;
				float sample = Perlin.Noise(xCoord + _noiseOffset.x, yCoord + _noiseOffset.x, _noiseOffset.z);
				pixels[y * _noiseTexture.width + x] = new Color(sample, sample, sample, 1f);
			}
		}
		
		_noiseTexture.SetPixels(pixels);
		_noiseTexture.Apply();*/

		var scale = _canvas.transform.localScale.x / _canvas.scaleFactor;
		foreach (var p in _activeParticles)
		{
			var noiseX = Perlin.Noise(p.transform.localPosition.x * scale * _noiseScale + 
				_noiseOffset.x, _noiseOffset.y, _noiseOffset.z);
			var noiseY = Perlin.Noise(_noiseOffset.x, p.transform.localPosition.x * scale * _noiseScale +
				_noiseOffset.y, _noiseOffset.z);
			var offset = new Vector3(noiseX, noiseY) * (_noiseStrength * 10f * Time.deltaTime);
			p.transform.localPosition += offset;
		}

		_noiseOffset -= _noiseScrollSpeed * Vector3.one / 4f * Time.deltaTime;
	}

	public void SetSpawnRadius(float radius)
	{
		_spawnRadius = radius;
	}

	public void InvertParticleScale(float parentScale)
	{
		foreach (var p in _activeParticles)
		{
			p.SetScale(1f / parentScale);
		}

		_lastParentScale = parentScale;
	}
	
	private void OnDrawGizmosSelected()
	{
		Handles.color = Color.yellow;
		var scale = _canvas.transform.localScale.x / _canvas.scaleFactor;
		Handles.DrawWireDisc(transform.position, transform.forward, _spawnRadius * scale);
	}
}