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

	public Note BindedNote;
	
	public bool IsOn
	{
		get { return isOn_; }
		set
		{
			isOn_ = value;
			transition = isOn_ ? Transition.None : Transition.ColorTint;

			if( isOn_ )
			{
				OwnerTabGroup.OnTabActivated(this);
			}
			else
			{
				BindedNote.OnTabDeselected();
			}
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

	public TabGroup OwnerTabGroup { get; set; }

	bool updateTransform_ = false;

	RectTransform rect_;

	#endregion


	#region unity events

	protected override void Awake()
	{
		base.Awake();
		image_ = GetComponent<Image>();
		rect_ = GetComponent<RectTransform>();
	}

	protected override void Start()
	{
		base.Start();
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

	public void TryClose()
	{
		DoClose();
	}

	public void DoClose()
	{
		if( IsOn )
			IsOn = false;

		BindedNote.OnTabClosed();
		OwnerTabGroup.OnTabClosed(this);
		Destroy(this.gameObject);
	}

	public void UpdateColor()
	{
		if( BindedNote is TreeNote )
		{
			TreeNote treeNote = BindedNote as TreeNote;
			Background = isOn_ ? (treeNote.LogNote.IsFullArea ? GameContext.Config.DoneColor : GameContext.Config.ThemeColor) : Color.white;
			if( isOn_ )
			{
				OwnerTabGroup.UnderBar.color = treeNote.LogNote.IsFullArea ? GameContext.Config.DoneColor : GameContext.Config.ThemeColor;
			}
		}
		if( BindedNote is DiaryNote )
		{
			Background = isOn_ ? GameContext.Config.DiaryColor : Color.white;
			if( isOn_ )
			{
				OwnerTabGroup.UnderBar.color = GameContext.Config.DiaryColor;
			}
		}
		if( textComponent_ != null )
		{
			textComponent_.color = isOn_ ? Color.white : GameContext.Config.TextColor;
		}
	}

	public void UpdateTitleText()
	{
		Text = BindedNote.TitleText;
	}

	IEnumerator UpdateTransformCoroutine()
	{
		yield return new WaitForEndOfFrame();

		rect_.sizeDelta = new Vector2(desiredWidth_, rect_.sizeDelta.y);
		rect_.anchoredPosition = targetPosition_;

		updateTransform_ = false;
	}

	#endregion


	#region drag

	public void OnBeginDrag(PointerEventData eventData)
	{
		OwnerTabGroup.OnBeginTabDrag(this);
	}

	public void OnDrag(PointerEventData eventData)
	{
		OwnerTabGroup.OnTabDragging(this, eventData);
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		OwnerTabGroup.OnEndTabDrag(this);
	}

	#endregion
}
