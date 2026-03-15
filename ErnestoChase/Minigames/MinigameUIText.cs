using UnityEngine;
using UnityEngine.UI;

namespace ErnestoChase.Minigames;

[RequireComponent(typeof(Text))]
public class MinigameUIText : MonoBehaviour
{
	protected Text _text;
	protected string _currentText = "";
	protected bool _textHidden;
	
	protected bool _fading;
	protected float _fadeLength;
	protected float _fadeStartTime;
	protected float _startFade;
	protected float _targetFade;

	protected virtual void Awake()
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
	
	protected virtual void LateUpdate()
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
			}
		}
	}

	public virtual void UpdateText()
	{
		_text.text = _textHidden ? "" : _currentText;
	}
	
	public void ToggleTextHidden() => SetTextHidden(!_textHidden);
	
	public void SetTextHidden(bool hide)
	{
		_textHidden = hide;
		UpdateText();
	}
	
	public void FadeIn(float time)
	{
		_fadeLength = time;
		_fadeStartTime = Time.time;
		_startFade = _text.color.a;
		_targetFade = 1f;
		_fading = true;
	}
	
	public void FadeOut(float time)
	{
		_fadeLength = time;
		_fadeStartTime = Time.time;
		_startFade = _text.color.a;
		_targetFade = 0f;
		_fading = true;
	}
}