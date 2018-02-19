using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TagText : MonoBehaviour {

	public string Text { get { return textComponent_.text; } set { textComponent_.text = value; } }
	public Text TextComponent { get { return textComponent_; } }
	Text textComponent_;

	public Image BG { get { return image_; } }
	Image image_;

	public RectTransform Rect { get { return rect_; } }
	RectTransform rect_;

	// Use this for initialization
	void Awake () {
		textComponent_ = GetComponentInChildren<Text>();
		image_ = GetComponent<Image>();
		rect_ = GetComponent<RectTransform>();
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	public override string ToString()
	{
		return Text;
	}
}
