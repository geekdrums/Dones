using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;
using UnityEngine.EventSystems;

public class CustomInputField : InputField, IColoredObject
{
	public bool IsFocused
	{
		get { return isFocused; }
		set
		{
			if( value != isFocused )
			{
				if( value )
				{
					ActivateInputField();
				}
				else
				{
					DeactivateInputField();
				}
			}
		}
	}

	// caret
	public int CaretPosision
	{
		get { return cachedCaretPos_; }
		set { selectionAnchorPosition = selectionFocusPosition = cachedCaretPos_ = desiredCaretPos_ = value; }
	}
	protected int cachedCaretPos_;
	protected static int desiredCaretPos_;
	protected static int desiredSelectionFocusPos_;
	public int ActualCaretPosition { get { return m_CaretSelectPosition; } set { m_CaretSelectPosition = m_CaretPosition = value; } }
	

	// color
	public Color Foreground { get { return textComponent.color; } set { textComponent.color = value; } }
	public Color Background { get { return targetGraphic.canvasRenderer.GetColor(); } set { targetGraphic.CrossFadeColor(value, 0.0f, true, true); } }
	public void SetColor(Color color) { Foreground = color; }
	public Color GetColor() { return Foreground; }


	// rect
	public Rect Rect { get { return new Rect((Vector2)targetGraphic.rectTransform.position + targetGraphic.rectTransform.rect.position, targetGraphic.rectTransform.rect.size); } }
	public float RectY { get { return targetGraphic.rectTransform.position.y; } }
	public float RectHeight { get { return targetGraphic.rectTransform.sizeDelta.y; } set { targetGraphic.rectTransform.sizeDelta = new Vector2(targetGraphic.rectTransform.sizeDelta.x, value); } }


	#region text length
	
	public void OnTextLengthChanged()
	{
		GameContext.TextLengthHelper.Request(m_TextComponent, OnUpdatedTextRectLength);
	}

	protected virtual void OnUpdatedTextRectLength()
	{

	}

	public float GetTextRectLength(int index)
	{
		return TextLengthHelper.GetTextRectLength(m_TextComponent.cachedTextGenerator, index);
	}

	public float GetFullTextRectLength()
	{
		return TextLengthHelper.GetFullTextRectLength(m_TextComponent.cachedTextGenerator);
	}

	#endregion


	#region override functions

	public override void OnPointerDown(PointerEventData eventData)
	{
		bool myDrag = IsActive() &&
			   IsInteractable() &&
			   (eventData.button == PointerEventData.InputButton.Left || eventData.button == PointerEventData.InputButton.Right) &&
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

		UpdateLabel();
		eventData.Use();
	}

	protected override void LateUpdate()
	{
		bool oldIsFocused = isFocused;

		base.LateUpdate();

		if( isFocused && !oldIsFocused )
		{
			cachedCaretPos_ = desiredCaretPos_;
			if( cachedCaretPos_ > text.Length )
			{
				cachedCaretPos_ = text.Length;
			}
			selectionAnchorPosition = selectionFocusPosition = cachedCaretPos_;
			if( desiredSelectionFocusPos_ >= 0 )
			{
				selectionFocusPosition = desiredSelectionFocusPos_;
				desiredSelectionFocusPos_ = -1;
			}
			OnFocused();
		}
	}

	protected virtual void OnFocused()
	{

	}

	protected virtual bool OnProcessKeyEvent(Event processingEvent, bool ctrl, bool shift, bool alt)
	{
		bool consumedEvent = false;
		return consumedEvent;
	}

	protected static string compositionString = "";
	protected Event outEvent = new Event();
	public override void OnUpdateSelected(BaseEventData eventData)
	{
		if( !isFocused )
			return;

		bool consumedEvent = false;

		// 全角変換中に確定させない状態でInputFieldからフォーカスを外すと変換中の文字が倍加するバグ
		// https://www.facebook.com/groups/unityuserj/permalink/1511318235594778/
		int compositionBugCount = -1;
		List<Event> currentEvents = new List<Event>();
		while( Event.PopEvent(outEvent) )
		{
			currentEvents.Add(new Event(outEvent));
		}
		if( currentEvents.Find((Event e) => e.rawType == EventType.MouseDown) != null )
		{
			compositionBugCount = 0;
			foreach( Event maybeDuplicatedEvent in currentEvents )
			{
				if( maybeDuplicatedEvent.rawType == EventType.KeyDown )
				{
					++compositionBugCount;
				}
			}
		}
		foreach( Event processingEvent in currentEvents )
		{
			if( processingEvent.rawType == EventType.KeyDown )
			{
				var currentEventModifiers = processingEvent.modifiers;
				bool ctrl = SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX ? (currentEventModifiers & EventModifiers.Command) != 0 : (currentEventModifiers & EventModifiers.Control) != 0;
				bool shift = (currentEventModifiers & EventModifiers.Shift) != 0;
				bool alt = (currentEventModifiers & EventModifiers.Alt) != 0;

				//print(string.Format("isKey:{0}, keyCode:{1}, character:{2}", processingEvent.isKey, processingEvent.keyCode, processingEvent.character));

				cachedCaretPos_ = m_CaretSelectPosition;

				// 派生先で自由にキー入力を処理。イベントが消化されたらその後は不要。
				consumedEvent = OnProcessKeyEvent(processingEvent, ctrl, shift, alt);

				if( consumedEvent == false )
				{
					switch( processingEvent.keyCode )
					{
						case KeyCode.None:
						{
							if( compositionBugCount >= 0 && compositionBugCount % 2 == 0 )
							{
								if( compositionBugCount == 0 ) continue;
								compositionBugCount -= 2;
							}
							KeyPressed(processingEvent);
						}
						break;
						default:
						{
							KeyPressed(processingEvent);
						}
						break;
					}
				}

				consumedEvent = true;
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
