using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class TabButton : UnityEngine.UI.Button {

	public Tree BindedTree;
	
	public bool IsOn
	{
		get { return isOn_; }
		set
		{
			isOn_ = value;
			transition = isOn_ ? Transition.None : Transition.ColorTint;

			if( isOn_ )
			{
				GameContext.Window.OnTreeActivated(BindedTree);
			}
			else
			{
				BindedTree.OnDeactivated();
			}
			BindedTree.transform.SetParent(isOn_ ? GameContext.Window.TreeParent.transform : this.transform);
			BindedTree.transform.localPosition = GameContext.Window.TreePrefab.transform.localPosition;
			BindedTree.gameObject.SetActive(isOn_);
			if( isOn_ )
			{
				BindedTree.OnActivated();
			}

			if( image_ != null )
			{
				UpdateColor();
			}
		}
	}
	bool isOn_ = false;
	
	public string Text
	{
		get { return textComponent_.text; }
		set
		{
			if( textComponent_ == null )
				textComponent_ = GetComponentInChildren<Text>();
			textComponent_.text = value;
			StartCoroutine(UpdateSizeCoroutine());
		}
	}
	Text textComponent_;

	public Color Background { get { return image_.canvasRenderer.GetColor(); } set { image_.color = value; image_.CrossFadeColor(Color.white, 0.0f, true, true); } }
	Image image_;

	RectTransform rect_;

	protected override void Start()
	{
		base.Start();
		image_ = GetComponent<Image>();
		rect_ = GetComponent<RectTransform>();
		textComponent_ = GetComponentInChildren<Text>();
		UpdateColor();
	}

	// Update is called once per frame
	void Update () {
		
	}

	public void OnClick()
	{
		if( IsOn == false )
			IsOn = true;
	}

	public void Close()
	{
		// todo: 保存していない場合、保存するか確認するウィンドウを出す

		if( IsOn )
			IsOn = false;

		GameContext.Window.OnTreeClosed(BindedTree);
		Destroy(this.gameObject);
	}

	void UpdateColor()
	{
		Background = isOn_ ? ColorManager.Theme.Bright : ColorManager.Base.Middle;
		textComponent_.color = isOn_ ? ColorManager.Base.Front : GameContext.Config.TextColor;
	}


	IEnumerator UpdateSizeCoroutine()
	{
		yield return new WaitForEndOfFrame();

		TextGenerator gen = textComponent_.cachedTextGenerator;

		float charLength = gen.characters[gen.characters.Count - 1].cursorPos.x - gen.characters[0].cursorPos.x;
		charLength /= textComponent_.pixelsPerUnit;

		rect_.sizeDelta = new Vector2(charLength + 50, rect_.sizeDelta.y);
	}
}
