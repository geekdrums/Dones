using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class TabButton : UnityEngine.UI.Button, IDragHandler, IBeginDragHandler, IEndDragHandler
{
	#region params

	public TreeNote BindedNote;
	
	public bool IsOn
	{
		get { return isOn_; }
		set
		{
			isOn_ = value;
			transition = isOn_ ? Transition.None : Transition.ColorTint;

			if( isOn_ )
			{
				GameContext.Window.OnTreeActivated(BindedNote);
			}
			else
			{
				BindedNote.OnTabDeselected();
			}
			BindedNote.gameObject.SetActive(isOn_);
			BindedNote.LogNote.gameObject.SetActive(isOn_);
			if( BindedNote.LogNote.Tab != null )
				BindedNote.LogNote.Tab.gameObject.SetActive(isOn_ && BindedNote.LogNote.IsOpended);
			if( isOn_ )
			{
				BindedNote.OnTabSelected();
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
		}
	}
	Text textComponent_;

	public Color Background { get { return image_.canvasRenderer.GetColor(); } set { image_.color = value; image_.CrossFadeColor(Color.white, 0.0f, true, true); } }
	Image image_;

	public float Width
	{
		get { return desiredWidth_; }
		set
		{
			desiredWidth_ = value;
			if( updateTransform_ == false )
			{
				updateTransform_ = true;
				StartCoroutine(UpdateTransformCoroutine());
			}
		}
	}
	float desiredWidth_ = 0;

	public Vector2 TargetPosition
	{
		get { return targetPosition_; }
		set
		{
			targetPosition_ = value;
			if( updateTransform_ == false )
			{
				updateTransform_ = true;
				StartCoroutine(UpdateTransformCoroutine());
			}
		}
	}
	Vector2 targetPosition_;

	bool updateTransform_ = false;

	RectTransform rect_;

	#endregion


	#region unity functions

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

	#endregion


	#region click, close

	public void OnClick()
	{
		if( IsOn == false )
			IsOn = true;
	}

	public void Close()
	{
		if( BindedNote.IsEdited )
		{
			GameContext.Window.ModalDialog.Show(BindedNote.TitleText + "ファイルへの変更を保存しますか？", this.CloseConfirmCallback);
			return;
		}

		if( BindedNote.LogNote.IsEdited )
		{
			GameContext.Window.ModalDialog.Show(BindedNote.LogNote.TitleText + "ログファイルへの変更を保存しますか？", this.CloseConfirmCallback);
			return;
		}

		DoClose();
	}

	void DoClose()
	{
		if( IsOn )
			IsOn = false;

		GameContext.Window.OnTreeClosed(BindedNote);
		Destroy(this.gameObject);
	}

	void CloseConfirmCallback(ModalDialog.DialogResult result)
	{
		switch(result)
		{
		case ModalDialog.DialogResult.Yes:
			BindedNote.Save();
			DoClose();
			break;
		case ModalDialog.DialogResult.No:
			DoClose();
			break;
		case ModalDialog.DialogResult.Cancel:
			// do nothing
			break;
		}
	}

	void UpdateColor()
	{
		Background = isOn_ ? ColorManager.Theme.Bright : ColorManager.Base.Front;
		textComponent_.color = isOn_ ? ColorManager.Base.Front : GameContext.Config.TextColor;
	}

	IEnumerator UpdateTransformCoroutine()
	{
		yield return new WaitForEndOfFrame();

		/*
		TextGenerator gen = textComponent_.cachedTextGenerator;

		float charLength = gen.characters[gen.characters.Count - 1].cursorPos.x - gen.characters[0].cursorPos.x;
		charLength /= textComponent_.pixelsPerUnit;

		rect_.sizeDelta = new Vector2(charLength + 50, rect_.sizeDelta.y);
		*/

		rect_.sizeDelta = new Vector2(desiredWidth_, rect_.sizeDelta.y);
		rect_.anchoredPosition = targetPosition_;

		updateTransform_ = false;
	}

	#endregion


	#region drag

	public void OnBeginDrag(PointerEventData eventData)
	{
		GameContext.Window.OnBeginTabDrag(this);
	}

	public void OnDrag(PointerEventData eventData)
	{
		GameContext.Window.OnTabDragging(this, eventData);
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		GameContext.Window.OnEndTabDrag(this);
	}

	#endregion
}
