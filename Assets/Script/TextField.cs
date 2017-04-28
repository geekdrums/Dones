using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TextField : InputField
{
	public Line BindedLine { get; set; }
	
	public int CaretPosision { get { return caretPos_; } set { caretPos_ = value; } }
	protected int caretPos_;

	public bool IsFocused
	{
		get { return isFocused; }
		set
		{
			if( value )
			{
				ActivateInputField();
				isJustFocused_ = true;
			}
		}
	}
	protected bool isJustFocused_;

	public Color Foreground { get { return mesh_.color; } set { mesh_.color = value; } }
	protected TextMesh mesh_;

	// Use this for initialization
	protected override void Awake()
	{
		base.Awake();
		mesh_ = GetComponent<TextMesh>();
	}

	// Update is called once per frame
	void Update()
	{
		if( IsFocused )
		{
			if( Input.GetKeyDown(KeyCode.DownArrow) == false && Input.GetKeyDown(KeyCode.UpArrow) == false )
			{
				caretPos_ = caretPosition;
			}
			ForceLabelUpdate();
		}
	}


	public void OnValueChanged()
	{
		BindedLine.Text = text;
	}

	public void EndEdit()
	{
	}

	protected override void LateUpdate()
	{
		base.LateUpdate();
		
		if( isJustFocused_ )
		{
			selectionAnchorPosition = selectionFocusPosition = caretPos_;
			isJustFocused_ = false;
		}
	}
	
	//public override void OnUpdateSelected(BaseEventData eventData)
	//{
	//	if( Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow) )
	//	{
	//		int oldCaretPos = caretPos_;
	//		base.OnUpdateSelected(eventData);
	//		caretPos_ = oldCaretPos;
	//	}
	//	else
	//	{
	//		base.OnUpdateSelected(eventData);
	//	}
	//}
}
