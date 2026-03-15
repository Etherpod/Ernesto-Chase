using UnityEngine;
using UnityEngine.UI;

namespace ErnestoChase;

public class CountdownTimer : MinigameUIText
{
	private float _timerLength;
	private float _currentTime;
	private bool _timerEnabled;

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

		_currentText = result;
		UpdateText();

		if (_currentTime == 0f)
		{
			ErnestoChase.Instance.StopGame();
			_timerEnabled = false;
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

	public float GetCurrentTime() => _currentTime;

	public void SetCurrentTime(float time) => _currentTime = time;
}