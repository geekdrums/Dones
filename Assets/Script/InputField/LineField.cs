using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;
using UnityEngine.EventSystems;

public class LineField : CustomInputField
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
				foreach( TagText tagText in tagTexts_ )
				{
					tagText.gameObject.SetActive(isSelected_ == false && isFocused == false);
				}
			}
		}
	}
	protected bool isSelected_;

	public bool HasSelection { get { return caretPositionInternal != caretSelectPositionInternal; } }

	public string SelectionString { get { return text.Substring(SelectionStartIndex, SelectionLength); } }
	public int SelectionStartIndex { get { return Math.Min(caretPositionInternal, caretSelectPositionInternal); } }
	public int SelectionEndIndex { get { return Math.Max(caretPositionInternal, caretSelectPositionInternal); } }
	public int SelectionLength { get { return Math.Abs(caretPositionInternal - caretSelectPositionInternal); } }

	protected UIGaugeRenderer strikeLine_;
	protected CheckMark checkMark_;

	protected List<TagText> tagTexts_ = new List<TagText>();

	#endregion


	#region public functions

	public void Initialize()
	{
		strikeLine_ = GetComponentInChildren<UIGaugeRenderer>(includeInactive: true);
		checkMark_ = textComponent.transform.Find("Check").GetComponent<CheckMark>();
	}

	public void DeleteSelection()
	{
		if( caretPositionInternal == caretSelectPositionInternal )
			return;

		if( caretPositionInternal < caretSelectPositionInternal )
		{
			m_Text = text.Substring(0, caretPositionInternal) + text.Substring(caretSelectPositionInternal, text.Length - caretSelectPositionInternal);
			caretSelectPositionInternal = caretPositionInternal;
		}
		else
		{
			m_Text = text.Substring(0, caretSelectPositionInternal) + text.Substring(caretPositionInternal, text.Length - caretPositionInternal);
			caretPositionInternal = caretSelectPositionInternal;
		}
	}

	public void Paste(string pasteText)
	{
		DeleteSelection();
		BindedLine.Text = text.Insert(m_CaretPosition, pasteText);
		caretSelectPositionInternal = caretPositionInternal += pasteText.Length;
	}

	public void SetSelection(int start, int length)
	{
		CaretPosision = start;
		selectionFocusPosition = start + length;
		desiredSelectionFocusPos_ = selectionFocusPosition;
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
#if UNITY_EDITOR
		gameObject.name = text;
#endif
		if( BindedLine.IsDone || BindedLine.HasAnyTags || BindedLine.IsLinkText ) OnTextLengthChanged();
	}

	#endregion


	#region set state

	public void SetIsDone(bool isDone, bool withAnim = true)
	{
		checkMark_.gameObject.SetActive(isDone);
		SetDesiredStrikeLineState();
		Foreground = GetDesiredTextColor();
		if( isDone )
		{
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

			foreach( TagText tagText in tagTexts_ )
			{
				BindedLine.Tree.TagHeapManager.BackToHeap(tagText);
			}
			tagTexts_.Clear();
		}
	}

	public void OnRepeatDone()
	{
		OnTextLengthChanged();
		SetDesiredStrikeLineState();
		strikeLine_.gameObject.SetActive(true);
		checkMark_.gameObject.SetActive(true);

		float backToUsualTime = 0.7f;

		strikeLine_.Rate = 0.0f;
		AnimManager.AddAnim(strikeLine_, 1.0f, ParamType.GaugeRate, AnimType.Time, 0.15f);
		AnimManager.AddAnim(strikeLine_, 0.0f, ParamType.GaugeRate, AnimType.Time, 0.1f, backToUsualTime, endOption: AnimEndOption.Deactivate);

		checkMark_.CheckAndUncheck(backToUsualTime);

		Foreground = GameContext.Config.DoneTextColor;
		AnimManager.AddAnim(this.gameObject, GameContext.Config.TextColor, ParamType.Color, AnimType.Time, 0.1f, backToUsualTime);
	}

	public void SetHashTags(List<string> tags)
	{
		if( BindedLine.IsDone || BindedLine.Tree == null || BindedLine.Tree.TagHeapManager == null ) return;

		List<TagText> removeList = new List<TagText>();
		foreach( TagText tagText in tagTexts_ )
		{
			if( tags.Find((string t) => "#" + t == tagText.Text) == null )
			{
				removeList.Add(tagText);
			}
		}
		foreach( TagText removeText in removeList )
		{
			tagTexts_.Remove(removeText);
			BindedLine.Tree.TagHeapManager.BackToHeap(removeText);
		}
		foreach( string tag in tags )
		{
			if( tagTexts_.Find((TagText t) => t.Text == "#" + tag) == null )
			{
				TagText tagText = BindedLine.Tree.TagHeapManager.Instantiate(this.transform);
				tagText.Text = "#" + tag;
				tagText.gameObject.SetActive(isFocused == false);
				tagText.TextComponent.fontStyle = BindedLine.IsBold ? FontStyle.Bold : FontStyle.Normal;
				tagText.UpdateTextRect(this);
				tagTexts_.Add(tagText);
			}
		}
	}

	public void SetIsLinkText(bool isLink)
	{
		SetDesiredStrikeLineState();
		Foreground = GetDesiredTextColor();
		if( isLink )
		{
			OnTextLengthChanged();
		}
	}

	public void SetIsClone(bool isClone)
	{
		Foreground = GetDesiredTextColor();
	}

	public void SetIsBold(bool isBold)
	{
		if( (textComponent.fontStyle == FontStyle.Bold) != isBold )
		{
			textComponent.fontStyle = isBold ? FontStyle.Bold : FontStyle.Normal;
			foreach( TagText tagText in tagTexts_ )
			{
				tagText.TextComponent.fontStyle = isBold ? FontStyle.Bold : FontStyle.Normal;
			}
			OnTextLengthChanged();
		}
	}

	protected void SetDesiredStrikeLineState()
	{
		float charLength = GetFullTextRectLength();
		if( BindedLine.IsLinkText )
		{
			strikeLine_.gameObject.SetActive(true);
			strikeLine_.rectTransform.anchoredPosition = new Vector2(0, -7);
			strikeLine_.SetLength(charLength);
			strikeLine_.SetColor(GameContext.Config.ThemeColor);
		}
		else
		{
			if( BindedLine.IsDone && BindedLine.IsClone == false )
			{
				strikeLine_.gameObject.SetActive(true);
			}
			else
			{
				strikeLine_.gameObject.SetActive(false);
			}
			strikeLine_.rectTransform.anchoredPosition = new Vector2(-3, 0);
			strikeLine_.SetLength(charLength + 6);
			strikeLine_.SetColor(GameContext.Config.StrikeColor);
		}
	}

	protected Color GetDesiredTextColor()
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
			else if( BindedLine.IsLinkText )
			{
				return GameContext.Config.ThemeColor;
			}
			else
			{
				return GameContext.Config.TextColor;
			}
		}
		return GameContext.Config.TextColor;
	}

	public override void OnTextLengthChanged()
	{
		float charLength = GetFullTextRectLength();

		if( BindedLine.IsLinkText )
		{
			strikeLine_.SetLength(charLength);
		}
		else
		{
			strikeLine_.SetLength(charLength + 6);
		}

		checkMark_.SetPositionX(charLength);

		float rectHeight = RectHeight;
		foreach( TagText tagText in tagTexts_ )
		{
			tagText.UpdateTextRect(this);
		}
	}

	public void OnFontSizeChanged(int fontSize, float heightPerLine)
	{
		textComponent.fontSize = fontSize;
		RectHeight = heightPerLine;
		OnTextLengthChanged();
		foreach( TagText tagText in tagTexts_ )
		{
			tagText.TextComponent.fontSize = fontSize;
		}
	}

	#endregion


	#region override functions

	protected override void OnDestroy()
	{
		if( BindedLine != null && BindedLine.Tree != null )
		{
			BindedLine.Tree.OnTextFieldDestroy(this);
		}
	}


	protected override void OnFocused()
	{
		base.OnFocused();
		if( BindedLine != null && BindedLine.Tree != null )
		{
			BindedLine.Tree.OnFocused(BindedLine);
			foreach( TagText tagText in tagTexts_ )
			{
				tagText.gameObject.SetActive(false);
			}
		}
	}

	protected override void LateUpdate()
	{
		base.LateUpdate();

		if( isFocused && BindedLine != null && BindedLine.NeedFixInput() )
		{
			BindedLine.FixTextInputAction();
		}
	}

	public void OnDoubleClick()
	{
		caretSelectPositionInternal = 0;
		caretPositionInternal = text.Length;
	}

	public override void OnDeselect(BaseEventData eventData)
	{
		base.OnDeselect(eventData);

		if( BindedLine != null && BindedLine.Tree != null )
		{
			BindedLine.Tree.OnFocusEnded(BindedLine);
			foreach( TagText tagText in tagTexts_ )
			{
				tagText.gameObject.SetActive(true);
			}
		}
	}

	public override void OnPointerDown(PointerEventData eventData)
	{
		base.OnPointerDown(eventData);
		BindedLine.FixTextInputAction();
	}

	public override void OnPointerUp(PointerEventData eventData)
	{
		base.OnPointerUp(eventData);

		if( BindedLine.IsLinkText && isFocused )
		{
			Vector2 localMousePos;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(textComponent.rectTransform, eventData.position, eventData.pressEventCamera, out localMousePos);
			if( GetCharacterIndexFromPosition(localMousePos) < text.Length )
			{
				Application.OpenURL(text);
			}
		}
	}

	protected override bool OnProcessKeyEvent(Event processingEvent, bool ctrl, bool shift, bool alt)
	{
		bool consumedEvent = false;

		if( BindedLine == null || BindedLine.Tree == null )
			return consumedEvent;


		bool ctrlOnly = ctrl && !alt && !shift;
		bool ctrlOrCtrlShift = ctrlOnly || (ctrl && shift);
		switch( processingEvent.keyCode )
		{
			case KeyCode.V:
			if( ctrlOnly )
			{
				// process in ownerTree
				consumedEvent = true;
			}
			break;
			case KeyCode.C:
			case KeyCode.X:
			if( ctrlOrCtrlShift && (isSelected_ || selectionAnchorPosition == selectionFocusPosition) )
			{
				// process in ownerTree
				consumedEvent = true;
			}
			break;
			case KeyCode.Semicolon:
			if( ctrlOnly && BindedLine.Tree.HasSelection == false )
			{
				DateTime now = DateTime.Now;
				string oldText = text;
				BindedLine.Tree.ActionManager.Execute(new LineAction(
					targetLines: BindedLine,
					execute: () =>
					{
						Paste(now.ToString(GameContext.Config.TimeFormat));
					},
					undo: () =>
					{
						text = oldText;
					}
					));

				consumedEvent = true;
			}
			break;
			case KeyCode.Colon:
			case KeyCode.Equals://日本語キーボードだとこっちになってるらしい。どうしたものか。Configにするか。
			if( ctrlOnly && BindedLine.Tree.HasSelection == false )
			{
				DateTime date = DateTime.Now;
				string oldText = text;
				BindedLine.Tree.ActionManager.Execute(new LineAction(
					targetLines: BindedLine,
					execute: () =>
					{
						Paste(date.ToString(GameContext.Config.DateFormat));
					},
					undo: () =>
					{
						text = oldText;
					}
					));

				consumedEvent = true;
			}
			break;
			case KeyCode.Delete:
			{
				if( BindedLine.Tree.HasSelection )
				{
					// process in ownerTree
					consumedEvent = true;
				}
				else
				{
					bool use = cachedCaretPos_ < text.Length;
					if( use ) BindedLine.Tree.OnDeleteKeyConsumed();
				}
			}
			break;
			case KeyCode.Backspace:
			{
				if( BindedLine.Tree.HasSelection )
				{
					// process in ownerTree
					consumedEvent = true;
				}
			}
			break;
			case KeyCode.DownArrow:
			{
				if( BindedLine.NextVisibleLine != null )
				{
					// process in ownerTree
					consumedEvent = true;
				}
				else
				{
					BindedLine.FixTextInputAction();
				}
			}
			break;
			case KeyCode.UpArrow:
			{
				if( BindedLine.PrevVisibleLine != null )
				{
					// process in ownerTree
					consumedEvent = true;
				}
				else
				{
					BindedLine.FixTextInputAction();
				}
			}
			break;
			case KeyCode.RightArrow:
			case KeyCode.LeftArrow:
			{
				KeyPressed(processingEvent);
				consumedEvent = true;
				desiredCaretPos_ = m_CaretSelectPosition;
				BindedLine.FixTextInputAction();
				if( GameContext.Window.TagIncrementalDialog.IsActive )
				{
					if( Line.GetTagInCaretPosition(BindedLine.Text, desiredCaretPos_) == null )
					{
						GameContext.Window.TagIncrementalDialog.Close();
					}
				}
			}
			break;
			case KeyCode.Home:
			case KeyCode.End:
			{
				KeyPressed(processingEvent);
				consumedEvent = true;
				desiredCaretPos_ = m_CaretSelectPosition;
				BindedLine.FixTextInputAction();
			}
			break;
			case KeyCode.None:
			{
				if( ctrlOrCtrlShift && (processingEvent.character == ' ' || processingEvent.character == '　') )
				{
					// process in ownerTree
					if( ctrl && shift && processingEvent.character == '　' )
					{
						// shift かつ 全角でSpace入力した時はKeyCodeのSpaceがイベントとして来ないのでこっちで対応
						BindedLine.Tree.OnCtrlShiftSpaceInput();
					}
					consumedEvent = true;
				}
				else if( (ctrl || alt == false) && BindedLine.Tree.HasSelection && processingEvent.character.ToString() != Line.TabString )
				{
					// 複数Line選択してキー入力したら、Line内部の文字列削除じゃなくてTreeからLine削除を呼ぶ
					LineField newField = BindedLine.Tree.DeleteSelection().Field;
					newField.KeyPressed(processingEvent);
					newField.CaretPosision = newField.text.Length;
					consumedEvent = true;
				}
			}
			break;
		}

		return consumedEvent;
	}

	#endregion
}
