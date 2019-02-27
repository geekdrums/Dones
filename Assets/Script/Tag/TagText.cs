using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TagText : MonoBehaviour {

	public string Text { get { return TextComponent.text; } set { TextComponent.text = value; } }
	public Text TextComponent
	{
		get
		{
			if( textComponent_ == null )
			{
				textComponent_ = GetComponentInChildren<Text>();
			}
			return textComponent_;
		}
	}
	Text textComponent_;

	public Image BG
	{
		get
		{
			if( image_ == null )
			{
				image_ = GetComponent<Image>();
			}
			return image_;
		}
	}
	Image image_;

	public RectTransform Rect
	{
		get
		{
			if( rect_ == null )
			{
				rect_ = GetComponent<RectTransform>();
			}
			return rect_;
		}
	}
	RectTransform rect_;

	// Use this for initialization
	void Awake () {
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	public override string ToString()
	{
		return Text;
	}

	public void UpdateTextRect(LineField field)
	{
		int index = field.text.LastIndexOf(Text);
		float x = field.GetTextRectLength(index - 1);
		Rect.anchoredPosition = new Vector2(x + field.textComponent.rectTransform.offsetMin.x, 0);
		float width = field.GetTextRectLength(index + Text.Length - 1) - x;
		Rect.sizeDelta = new Vector2(width, field.RectHeight);
	}
}
