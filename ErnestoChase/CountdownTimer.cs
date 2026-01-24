using UnityEngine;
using UnityEngine.UI;

namespace ErnestoChase;

[RequireComponent(typeof(Text))]
public class CountdownTimer : MonoBehaviour
{
	private Text _text;
	private float _timerLength;
	private float _currentTime;
	private bool _timerEnabled;
	
	private bool _fading;
	private float _fadeLength;
	private float _fadeStartTime;
	private float _startFade;
	private float _targetFade;

	private void Awake()
	{
		_text = gameObject.GetRequiredComponent<Text>();
		
		var font = (Font)Resources.Load(@"fonts\english - latin\HVD Fonts - BrandonGrotesque-Bold_Dynamic");
		if (font != null)
		{
			_text.font = font;
		}
		
		var color = _text.color;
		color.a = 0f;
		_text.color = color;
	}

	private void Update()
	{
		if (!_timerEnabled) return;
		
		_currentTime = Mathf.Max(0f, _currentTime - Time.deltaTime);

		int hours = (int)(_currentTime / 3600f);
		int minutes = (int)(_currentTime / 60f) % 60;
		float seconds = (int)(_currentTime % 60f);
		string result = "";
		
		if (hours > 0f)
		{
			result += $"{hours}";
		}

		result += $"{minutes:00}:{seconds:00}";

		_text.text = result;

		if (_currentTime == 0f)
		{
			ErnestoChase.Instance.OnCountdownComplete();
			FadeOut(5f);
		}
	}

	private void LateUpdate()
	{
		if (_fading)
		{
			float lerp = Mathf.InverseLerp(_fadeStartTime, _fadeStartTime + _fadeLength, Time.time);
			var color = _text.color;
			color.a = Mathf.Lerp(_startFade, _targetFade, lerp * lerp);
			_text.color = color;

			if (lerp >= 1f)
			{
				_fading = false;
				if (_currentTime == 0f) enabled = false;
			}
		}
	}
	
	public void SetTimerLength(float minutes)
	{
		_timerLength = minutes * 60f;
		_currentTime = _timerLength;
	}

	public void StartTimer()
	{
		if (_timerLength <= 0f) return;
		
		_timerEnabled = true;
	}

	public void StopTimer()
	{
		_timerEnabled = false;
	}

	public void FadeIn(float time)
	{
		_fadeLength = time;
		_fadeStartTime = Time.time;
		_startFade = _text.color.a;
		_targetFade = 1f;
		_fading = true;
		enabled = true;
	}
	
	public void FadeOut(float time)
	{
		_fadeLength = time;
		_fadeStartTime = Time.time;
		_startFade = _text.color.a;
		_targetFade = 0f;
		_fading = true;
		enabled = true;
	}

	public float GetCurrentTime() => _currentTime;

	public void SetCurrentTime(float time) => _currentTime = time;
}