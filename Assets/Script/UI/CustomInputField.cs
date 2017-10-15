using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;
using UnityEngine.EventSystems;

public class CustomInputField : InputField
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

	public int CaretPosision
	{
		get { return cachedCaretPos_; }
		set { selectionAnchorPosition = selectionFocusPosition = cachedCaretPos_ = desiredCaretPos_ = value; }
	}
	protected int cachedCaretPos_;
	protected static int desiredCaretPos_;
	public int ActualCaretPosition { get { return m_CaretSelectPosition; } set { m_CaretSelectPosition = m_CaretPosition = value; } }


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

	protected static string compositionString = "";
	public override void OnUpdateSelected(BaseEventData eventData)
	{
		base.OnUpdateSelected(eventData);
		// ひらがな入力で、変換の最後の1文字だけ、BackspaceのKeyDownが来ない問題
		bool compositionStringDeleted = (compositionString.Length > 0 && Input.compositionString.Length == 0);
		if( compositionStringDeleted )
			UpdateLabel();
	}
}
