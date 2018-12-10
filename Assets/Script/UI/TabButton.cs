using System;
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

	public Note BindedNote { get; private set; }
	public NoteViewParam ViewParam { get; private set; }

	public bool IsSelected
	{
		get { return isSelected_; }
		set
		{
			if( value != isSelected_ )
			{
				isSelected_ = value;
				transition = isSelected_ ? Transition.None : Transition.ColorTint;
				UpdateColor();

				if( isSelected_ )
				{
					OwnerTabGroup.OnTabSelected(this);
					BindedNote.SetNoteViewParam(ViewParam);
					
					GameContext.Window.UpdateVerticalLayout();
					GameContext.TagList.OnTreePathChanged(BindedNote is TreeNote ? (BindedNote as TreeNote).Tree.TitleLine : null);
				}
				else
				{
					BindedNote.CacheNoteViewParam(ViewParam);
				}
			}
		}
	}
	bool isSelected_ = false;
	
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

	public Color Background { get { return background_.canvasRenderer.GetColor(); } set { background_.color = value; background_.CrossFadeColor(Color.white, 0.0f, true, true); } }
	Image background_;

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


	public void Bind(TreeNote note, TreePath path = null)
	{
		ViewParam = new NoteViewParam();
		BindedNote = note;
		ViewParam.Path = (path == null ? new TreePath() : path);
		ViewParam.TargetScrollValue = 1.0f;
		ViewParam.LogNoteTargetScrollValue = 1.0f;
		Line line = note.Tree.GetLineFromPath(ViewParam.Path);
		if( ViewParam.Path.Length == 0 )
		{
			Text = "Home";
		}
		else
		{
			Text = ViewParam.Path[ViewParam.Path.Length - 1];
		}
	}

	#region unity events

	protected override void Awake()
	{
		base.Awake();
		background_ = GetComponent<Image>();
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
		if( IsSelected == false )
		{
			if( CanSelect(showDialog: true) == false )
			{
				return;
			}

			IsSelected = true;
		}
	}

	public bool CanSelect(bool showDialog = false)
	{
		if( BindedNote is TreeNote )
		{
			Line line = (BindedNote as TreeNote).Tree.GetLineFromPath(ViewParam.Path);
			if( line == null )
			{
				// pathが見つからなければ表示できない
				if( showDialog )
				{
					GameContext.Window.ModalDialog.Show(String.Format("\"{0}\" は見つかりませんでした", ViewParam.Path.ToString()), ModalDialog.DialogType.OK, null);
				}
				return false;
			}
		}

		return true;
	}

	public void TryClose()
	{
		DoClose();
	}

	public void DoClose()
	{
		if( IsSelected )
			IsSelected = false;
		
		OwnerTabGroup.OnTabClosed(this);
		Destroy(this.gameObject);
	}

	public void UpdateColor()
	{
		if( background_ == null )
		{
			return;
		}

		if( BindedNote is TreeNote )
		{
			TreeNote treeNote = BindedNote as TreeNote;
			Background = isSelected_ ? GameContext.Config.ThemeColor : Color.white;
			if( isSelected_ )
			{
				OwnerTabGroup.SplitBar.SetColor(GameContext.Config.ThemeColor);
			}
		}
		if( textComponent_ != null && GameContext.Config != null )
		{
			textComponent_.color = isSelected_ ? Color.white : GameContext.Config.TextColor;
		}
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
