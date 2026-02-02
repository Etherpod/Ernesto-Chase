namespace ErnestoChase;

public class RandomShipLogNotification : MinigameUIText
{
	public void ShowNotification(string text)
	{
		_currentText = text;
		SetTextHidden(false);

		var c = _text.color;
		c.a = 1f;
		_text.color = c;
		FadeOut(4f);
	}
}