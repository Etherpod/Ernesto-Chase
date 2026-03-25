using System;
using UnityEngine;

namespace ErnestoChase.PlayerErnesto;

public class PlayerTrackerTarget : MonoBehaviour
{
	[SerializeField]
	private UIParticleSystem _trackerParticles = null;
	
	private float _screenPixelSize;
	private float _targetScale;

	public void SetScreenSize(float pixelSize, float canvasScaleFactor)
	{
		_screenPixelSize = pixelSize;
		_targetScale = _screenPixelSize / (this.GetRequiredComponent<RectTransform>().rect.width * canvasScaleFactor);
		SetScale(_targetScale);
	}

	private void SetScale(float scale)
	{
		transform.localScale = Vector3.one * scale;
		//_trackerParticles.InvertParticleScale(scale);
	}
}