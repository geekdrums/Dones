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

	protected override void OnEnable()
	{
		base.OnEnable();
		if( shouldUpdateTextLength_ )
		{
			StartCoroutine(UpdateTextLengthCoroutine());
		}
	}

	protected bool shouldUpdateTextLength_ = false;

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

	protected IEnumerator UpdateTextLengthCoroutine()
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

		OnUpdatedTextRectLength();

		shouldUpdateTextLength_ = false;
	}

	public float GetTextRectLength(int index)
	{
		return CalcTextRectLength(m_TextComponent.cachedTextGenerator, index);
	}

	public float GetFullTextRectLength()
	{
		return GetTextRectLength(text.Length - 1);
	}

	public static float CalcTextRectLength(TextGenerator textGen, int index)
	{
		index = Math.Min(textGen.characters.Count - 1, Math.Max(0, index));
		return textGen.characters[index].cursorPos.x + textGen.characters[index].charWidth - textGen.characters[0].cursorPos.x;
	}

	protected virtual void OnUpdatedTextRectLength()
	{

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

	protected static string compositionString = "";
	public override void OnUpdateSelected(BaseEventData eventData)
	{
		base.OnUpdateSelected(eventData);
		// ひらがな入力で、変換の最後の1文字だけ、BackspaceのKeyDownが来ない問題
		bool compositionStringDeleted = (compositionString.Length > 0 && Input.compositionString.Length == 0);
		if( compositionStringDeleted )
			UpdateLabel();
	}

	#endregion
}
