using UnityEngine;
using UnityEngine.UI;

namespace ErnestoChase;

[RequireComponent(typeof(Text))]
public class CountdownTimer : MonoBehaviour
{
	private Text _text;
	private float _timerLength;
	private float _currentTime;

	private void Awake()
	{
		_text = gameObject.GetRequiredComponent<Text>();
		
		var font = (Font)Resources.Load(@"fonts\english - latin\HVD Fonts - BrandonGrotesque-Bold_Dynamic");
		if (font != null)
		{
			_text.font = font;
		}
	}

	private void Update()
	{
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
			enabled = false;
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
		
		enabled = true;
	}

	public void StopTimer()
	{
		enabled = false;
	}
}