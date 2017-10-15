﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;
using UnityEngine.EventSystems;

public class LineField : CustomInputField, IColoredObject
{
	#region properties

	public Line BindedLine { get; set; }

	public bool IsSelected
	{
		get { return isSelected_; }
		set
		{
			if( isSelected_ != value )
			{
				isSelected_ = value;
				transition = isSelected_ ? Transition.None : Transition.ColorTint;
				Background = isSelected_ ? GameContext.Config.SelectionColor : colors.normalColor;
			}
		}
	}
	protected bool isSelected_;

	protected bool isPointerEntered_ = false;

	public Color Foreground { get { return textComponent.color; } set { textComponent.color = value; } }
	public Color Background { get { return targetGraphic.canvasRenderer.GetColor(); } set { targetGraphic.CrossFadeColor(value, 0.0f, true, true); } }
	public void SetColor(Color color) { Background = color; }
	public Color GetColor() { return Background;  }

	public Rect Rect { get { return new Rect((Vector2)targetGraphic.rectTransform.position + targetGraphic.rectTransform.rect.position, targetGraphic.rectTransform.rect.size); } }
	public float RectY { get { return targetGraphic.rectTransform.position.y; } }
	public float RectHeight {get{ return targetGraphic.rectTransform.sizeDelta.y; } set { targetGraphic.rectTransform.sizeDelta = new Vector2(targetGraphic.rectTransform.sizeDelta.x, value); } }

	protected UIGaugeRenderer strikeLine_;
	protected CheckMark checkMark_;
	protected UIMidairPrimitive listMark_;
	protected bool shouldUpdateTextLength_ = false;

	#endregion


	#region unity events

	// Use this for initialization
	protected override void Awake()
	{
		base.Awake();
		strikeLine_ = GetComponentInChildren<UIGaugeRenderer>(includeInactive: true);
		checkMark_ = textComponent.transform.Find("Check").GetComponent<CheckMark>();
		listMark_ = textComponent.transform.Find("Mark").GetComponent<UIMidairPrimitive>();
	}

	protected override void Start()
	{
		base.Start();
	}

	// Update is called once per frame
	void Update()
	{
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		if( shouldUpdateTextLength_ )
		{
			StartCoroutine(UpdateTextLengthCoroutine());
		}
	}

	#endregion


	#region public functions

	public void Paste(string pasteText)
	{
		DeleteSelection();
		BindedLine.Text = text.Insert(m_CaretPosition, pasteText);
		caretSelectPositionInternal = caretPositionInternal += pasteText.Length;
		BindedLine.CheckIsLink();
	}

	public void SetSelection(int start, int end)
	{
		CaretPosision = start;
		selectionFocusPosition = end;
	}

	public void SetTextDirectly(string text)
	{
		// onValueChangedを発生させないテキスト設定。
		// Fieldからのユーザーの入力のみをイベントとして取得したいので、
		// Lineクラスからシステム的に設定される場合（Undoや改行など）は、
		// この関数でイベント呼び出しを避ける。
		m_Text = text;
		//BindedLine.CheckIsComment();
		UpdateLabel();
		if( BindedLine.IsDone || BindedLine.IsOnList || BindedLine.IsLinkText ) OnTextLengthChanged();
	}

	public void SetIsDone(bool isDone, bool withAnim = true)
	{
		strikeLine_.gameObject.SetActive(isDone && BindedLine.IsClone == false);
		checkMark_.gameObject.SetActive(false);
		Foreground = GetDesiredTextColor();
		if( isDone )
		{
			listMark_.gameObject.SetActive(false);
			OnTextLengthChanged();

			if( withAnim )
			{
				if( BindedLine.IsClone == false )
				{
					strikeLine_.Rate = 0.0f;
					AnimManager.AddAnim(strikeLine_, 1.0f, ParamType.GaugeRate, AnimType.Time, 0.15f);
				}
				checkMark_.Check();
			}
		}
		else
		{
			listMark_.gameObject.SetActive(BindedLine.IsOnList);
		}
	}

	public void SetIsOnList(bool isOnList, bool withAnim = true)
	{
		Foreground = GetDesiredTextColor();
		if( isOnList )
		{
			if( BindedLine.IsDone )
			{
				return;
			}

			if( listMark_.gameObject.activeInHierarchy )
			{
				AnimManager.RemoveOtherAnim(listMark_.gameObject);
			}
			checkMark_.gameObject.SetActive(false);
			listMark_.gameObject.SetActive(true);
			listMark_.Width = 1;
			listMark_.ArcRate = 1.0f;
			OnTextLengthChanged();

			if( withAnim )
			{
				AnimManager.AddAnim(listMark_, 8.0f, ParamType.PrimitiveWidth, AnimType.Time, 0.05f);
				AnimManager.AddAnim(listMark_, 1.0f, ParamType.PrimitiveWidth, AnimType.Time, 0.2f, 0.05f);
			}
		}
		else
		{
			if( withAnim )
			{
				AnimManager.AddAnim(listMark_, 0.0f, ParamType.PrimitiveArc, AnimType.Time, 0.15f, endOption: AnimEndOption.Deactivate);
			}
			else
			{
				listMark_.gameObject.SetActive(false);
			}
		}
	}

	public void SetIsLinkText(bool isLink)
	{
		if( isLink )
		{
			strikeLine_.gameObject.SetActive(isLink);
			Foreground = GetDesiredTextColor();
			strikeLine_.transform.localPosition = new Vector3(strikeLine_.transform.localPosition.x, -5, strikeLine_.transform.localPosition.z);
			strikeLine_.SetColor(GameContext.Config.ThemeColor);
			OnTextLengthChanged();
		}
		else if( BindedLine.IsDone == false && BindedLine.IsComment == false )
		{
			strikeLine_.gameObject.SetActive(isLink);
			Foreground = GetDesiredTextColor();
			strikeLine_.transform.localPosition = new Vector3(strikeLine_.transform.localPosition.x, 0, strikeLine_.transform.localPosition.z);
			strikeLine_.SetColor(GameContext.Config.StrikeColor);
		}
	}

	public void SetIsClone(bool isClone)
	{
		Foreground = GetDesiredTextColor();
	}

	public void SetIsComment(bool isComment)
	{
		if( isComment )
		{
			strikeLine_.gameObject.SetActive(isComment);
			Foreground = GetDesiredTextColor();
			Background = Color.white;
			strikeLine_.Direction = Vector3.up;
			strikeLine_.Length = 28;
			strikeLine_.Width = 6;
			strikeLine_.Rate = 1.0f;
			strikeLine_.rectTransform.anchoredPosition = new Vector2(-10, -13);
			strikeLine_.SetColor(GameContext.Config.CommentLineColor);
			transition = Transition.None;
		}
		else
		{
			strikeLine_.gameObject.SetActive(false);
			Foreground = GetDesiredTextColor();
			strikeLine_.Direction = Vector3.right;
			strikeLine_.Length = 0;
			strikeLine_.Width = 1;
			strikeLine_.rectTransform.anchoredPosition = new Vector2(-5, 0);
			strikeLine_.SetColor(GameContext.Config.StrikeColor);
			transition = Transition.ColorTint;
			if( BindedLine.IsDone )
			{
				SetIsDone(BindedLine.IsDone);
			}
		}
	}

	Color GetDesiredTextColor()
	{
		if( BindedLine != null )
		{
			if( BindedLine.IsClone )
			{
				if( BindedLine.IsDone )
				{
					return GameContext.Config.TextColor;
				}
				else if( BindedLine.Parent.IsDone )
				{
					return GameContext.Config.CommentTextColor;
				}
				else
				{
					return GameContext.Config.CloneTextColor;
				}
			}
			else if( BindedLine.IsDone )
			{
				return GameContext.Config.DoneTextColor;
			}
			else if( BindedLine.IsOnList )
			{
				return GameContext.Config.ShortLineColor;
			}
			else if( BindedLine.IsLinkText )
			{
				return GameContext.Config.ThemeColor;
			}
			else if( BindedLine.IsComment )
			{
				return GameContext.Config.CommentTextColor;
			}
			else
			{
				return GameContext.Config.TextColor;
			}
		}
		return GameContext.Config.TextColor;
	}

	public void OnTextLengthChanged()
	{
		if( shouldUpdateTextLength_ == false )
		{
			shouldUpdateTextLength_ = true;
			if( this.gameObject.activeInHierarchy )
			{
				StartCoroutine(UpdateTextLengthCoroutine());
			}
		}
	}

	IEnumerator UpdateTextLengthCoroutine()
	{
		yield return new WaitWhile(() => m_TextComponent.cachedTextGenerator.characterCount == 0);

		bool isRendered = true;
		ScrollRect scrollRect = GetComponentInParent<ScrollRect>();
		yield return new WaitWhile(() =>
		{
			float scrollHeight = scrollRect.GetComponent<RectTransform>().rect.height;
			float heightPerLine = GameContext.Config.HeightPerLine;

			// Lineが下側に出て見えなくなった場合
			float targetUnderHeight = -(transform.position.y - scrollRect.transform.position.y) + heightPerLine / 2 - scrollHeight;
			if( targetUnderHeight > 0 )
			{
				isRendered = false;
				return true;
			}

			// Lineが上側に出て見えなくなった場合
			float targetOverHeight = (transform.position.y - scrollRect.transform.position.y);
			if( targetOverHeight > 0 )
			{
				isRendered = false;
				return true;
			}

			return false;
		});

		if( isRendered == false )
		{
			yield return new WaitForEndOfFrame();
		}

		float charLength = GetTextRectLength();

		if( BindedLine.IsComment == false )
			strikeLine_.SetLength(charLength + 10);

		checkMark_.gameObject.SetActive(BindedLine.IsDone);
		checkMark_.SetPositionX(charLength + 5);
		listMark_.GetComponent<RectTransform>().anchoredPosition = new Vector2(charLength + 15, listMark_.transform.localPosition.y);

		shouldUpdateTextLength_ = false;
	}

	public float GetTextRectLength()
	{
		TextGenerator gen = m_TextComponent.cachedTextGenerator;
		return gen.characters[gen.characters.Count - 1].cursorPos.x - gen.characters[0].cursorPos.x;
	}

	#endregion


	#region override functions

	protected override void OnDestroy()
	{
		if( BindedLine != null && BindedLine.Tree != null )
			BindedLine.Tree.OnTextFieldDestroy(this);
	}

	protected override void LateUpdate()
	{
		bool oldIsFocused = isFocused;

		base.LateUpdate();

		if( oldIsFocused != isFocused && BindedLine != null && BindedLine.Tree != null )
		{
			cachedCaretPos_ = desiredCaretPos_;
			if( cachedCaretPos_ > text.Length )
			{
				cachedCaretPos_ = text.Length;
			}
			selectionAnchorPosition = selectionFocusPosition = cachedCaretPos_;
			BindedLine.Tree.OnFocused(BindedLine);
		}

		if( isFocused && BindedLine != null && BindedLine.NeedFixInput() )
		{
			BindedLine.FixTextInputAction();
		} 
	}

	public override void OnDeselect(BaseEventData eventData)
	{
		base.OnDeselect(eventData);

		if( BindedLine != null && BindedLine.Tree != null )
		{
			BindedLine.Tree.OnFocusEnded(BindedLine);
		}
	}

	public override void OnPointerDown(PointerEventData eventData)
	{
		bool myDrag = IsActive() &&
			   IsInteractable() &&
			   eventData.button == PointerEventData.InputButton.Left &&
			   m_TextComponent != null &&
			   m_Keyboard == null;

		if( myDrag == false )
			return;

		EventSystem.current.SetSelectedGameObject(gameObject, eventData);
		
		base.OnPointerDown(eventData);

		// override this feature
		//if( hadFocusBefore )
		//{
		Vector2 localMousePos;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(textComponent.rectTransform, eventData.position, eventData.pressEventCamera, out localMousePos);
		desiredCaretPos_ = cachedCaretPos_ = caretSelectPositionInternal = caretPositionInternal = GetCharacterIndexFromPosition(localMousePos) + m_DrawStart;
		//}

		BindedLine.FixTextInputAction();
		UpdateLabel();
		eventData.Use();
	}

	public override void OnPointerEnter(PointerEventData eventData)
	{
		base.OnPointerEnter(eventData);
		if( Input.GetMouseButton(0) == false )
		{
			isPointerEntered_ = true;
		}
	}

	public override void OnPointerExit(PointerEventData eventData)
	{
		base.OnPointerExit(eventData);
		isPointerEntered_ = false;
	}

	public override void OnPointerUp(PointerEventData eventData)
	{
		base.OnPointerUp(eventData);

		if( BindedLine.IsLinkText && isPointerEntered_ )
		{
			Vector2 localMousePos;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(textComponent.rectTransform, eventData.position, eventData.pressEventCamera, out localMousePos);
			if( GetCharacterIndexFromPosition(localMousePos) < text.Length )
			{
				Application.OpenURL(text);
			}
		}
	}
	
	public override void OnUpdateSelected(BaseEventData eventData)
	{
		if( !isFocused || BindedLine == null || BindedLine.Tree == null )
			return;

		bool consumedEvent = false;

		int compositionBugCount = -1;
		Event popEvent = new Event();
		List<Event> currentEvents = new List<Event>();
		while( Event.PopEvent(popEvent) )
		{
			currentEvents.Add(new Event(popEvent));
		}
		if( currentEvents.Find((Event e) => e.rawType == EventType.MouseDown) != null )
		{
			compositionBugCount = 0;
			foreach( Event maybeDuplicatedEvent in currentEvents )
			{
				if( maybeDuplicatedEvent.rawType == EventType.keyDown )
				{
					++compositionBugCount;
				}
			}
		}
		foreach( Event processingEvent in currentEvents )
		{
			if( processingEvent.rawType == EventType.KeyDown )
			{
				consumedEvent = true;

				var currentEventModifiers = processingEvent.modifiers;
				bool ctrl = SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX ? (currentEventModifiers & EventModifiers.Command) != 0 : (currentEventModifiers & EventModifiers.Control) != 0;
				bool shift = (currentEventModifiers & EventModifiers.Shift) != 0;
				bool alt = (currentEventModifiers & EventModifiers.Alt) != 0;
				bool ctrlOnly = ctrl && !alt && !shift;

				cachedCaretPos_ = m_CaretSelectPosition;
				switch( processingEvent.keyCode )
				{
				case KeyCode.V:
					if( ctrlOnly )
					{
						// process in ownerTree
					}
					break;
				case KeyCode.Space:
					if( ctrlOnly )
					{
						// process in ownerTree
					}
					else
					{
						KeyPressed(processingEvent);
					}
					break;
				case KeyCode.C:
				case KeyCode.X:
					if( ctrlOnly && isSelected_ )
					{
						// process in ownerTree
					}
					else
					{
						KeyPressed(processingEvent);
					}
					break;
				case KeyCode.M:
					if( ctrlOnly )
					{
						if( BindedLine.Count > 0 )
						{
							BindedLine.Tree.OnFoldUpdated(BindedLine, !BindedLine.IsFolded);
						}
						else if( BindedLine.Parent.Parent != null )
						{
							BindedLine.Tree.OnFoldUpdated(BindedLine.Parent, true);
						}
					}
					break;
				case KeyCode.T:
					if( ctrlOnly && BindedLine.Tree.HasSelection == false )
					{
						DateTime now = DateTime.Now;
						string oldText = text;
						BindedLine.Tree.ActionManager.Execute(new Action(
							execute: () =>
							{
								Paste(now.ToString("HH:mm"));
							},
							undo: () =>
							{
								text = oldText;
							}
							));
					}
					break;
				case KeyCode.H:
					if( ctrlOnly && BindedLine.Tree.HasSelection == false )
					{
						DateTime date = DateTime.Now;
						string oldText = text;
						BindedLine.Tree.ActionManager.Execute(new Action(
							execute: () =>
							{
								Paste(date.ToString("yyyy/M/d (ddd)"));
							},
							undo: () =>
							{
								text = oldText;
							}
							));
					}
					break;
				case KeyCode.Delete:
					{
						if( BindedLine.Tree.HasSelection )
						{
							// process in ownerTree
						}
						else
						{
							bool use = cachedCaretPos_ < text.Length;
							KeyPressed(processingEvent);
							if( use ) BindedLine.Tree.OnDeleteKeyConsumed();
						}
					}
					break;
				case KeyCode.Backspace:
					{
						if( BindedLine.Tree.HasSelection )
						{
							// process in ownerTree
						}
						else
						{
							KeyPressed(processingEvent);
						}
					}
					break;
				case KeyCode.DownArrow:
					{
						if( BindedLine.NextVisibleLine != null )
						{
							// process in ownerTree
						}
						else
						{
							KeyPressed(processingEvent);
							BindedLine.FixTextInputAction();
						}
					}
					break;
				case KeyCode.UpArrow:
					{
						if( BindedLine.PrevVisibleLine != null )
						{
							// process in ownerTree
						}
						else
						{
							KeyPressed(processingEvent);
							BindedLine.FixTextInputAction();
						}
					}
					break;
				case KeyCode.RightArrow:
				case KeyCode.LeftArrow:
					{
						KeyPressed(processingEvent);
						desiredCaretPos_ = m_CaretSelectPosition;
						BindedLine.FixTextInputAction();
					}
					break;
				case KeyCode.Home:
				case KeyCode.End:
					{
						KeyPressed(processingEvent);
						desiredCaretPos_ = m_CaretSelectPosition;
						BindedLine.FixTextInputAction();
					}
					break;
				default:
					if( ctrlOnly && processingEvent.keyCode == KeyCode.None && processingEvent.character.ToString() == " " )
					{
						// process in ownerTree
					}
					else if( processingEvent.keyCode == KeyCode.None && BindedLine.Tree.HasSelection && processingEvent.character.ToString() != Line.TabString )
					{
						LineField newField = BindedLine.Tree.DeleteSelection().Field;
						newField.KeyPressed(processingEvent);
						newField.CaretPosision = newField.text.Length;
					}
					else
					{
						if( compositionBugCount >= 0 && compositionBugCount % 2 == 0 )
						{
							if( compositionBugCount == 0 ) continue;
							compositionBugCount -= 2;
						}
						KeyPressed(processingEvent);
					}
					break;
				}
			}
		}

		// ひらがな入力で、変換の最後の1文字だけ、BackspaceのKeyDownが来ない問題
		bool compositionStringDeleted = (compositionString.Length > 0 && Input.compositionString.Length == 0);
		if( consumedEvent || compositionStringDeleted )
			UpdateLabel();

		compositionString = Input.compositionString;

		eventData.Use();
	}

	#endregion
}