﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;
using UnityEngine.EventSystems;

public class TextField : InputField, IColoredObject
{
	#region properties

	public Line BindedLine { get; set; }
	
	public int CaretPosision
	{
		get { return caretPos_; }
		set { caretPos_ = desiredCaretPos_ = value; }
	}
	protected int caretPos_;
	protected static int desiredCaretPos_;

	public bool IsFocused
	{
		get { return isFocused; }
		set
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

	public bool IsSelected
	{
		get { return isSelected_; }
		set
		{
			if( isSelected_ != value )
			{
				isSelected_ = value;
				Background = isSelected_ ? selectionColor : colors.normalColor;
			}
		}
	}
	protected bool isSelected_;

	public Color Foreground { get { return textComponent.color; } set { textComponent.color = value; } }
	public Color Background { get { return image_.color; } set { image_.color = value; } }
	public void SetColor(Color color) { Background = color; }
	public Color GetColor() { return Background;  }

	public Rect Rect { get { return new Rect(image_.rectTransform.position, image_.rectTransform.sizeDelta); } }
	public float RectY { get { return image_.rectTransform.position.y; } }
	protected Image image_;

	public Tree OwnerTree { get { return ownerTree_; } }
	protected Tree ownerTree_;

	#endregion


	#region unity functions

	// Use this for initialization
	protected override void Awake()
	{
		base.Awake();
		image_ = GetComponent<Image>();
	}

	protected override void Start()
	{
		base.Start();
		ownerTree_ = GetComponentInParent<Tree>();
	}

	// Update is called once per frame
	void Update()
	{
	}

	#endregion


	#region public functions

	public void Paste(string text)
	{
		Append(text);
		UpdateLabel();
	}

	#endregion


	#region override functions

	protected override void OnDestroy()
	{
		ownerTree_.OnTextFieldDestroy(this);
	}

	protected override void LateUpdate()
	{
		bool oldIsFocused = isFocused;

		base.LateUpdate();

		if( oldIsFocused != isFocused )
		{
			caretPos_ = desiredCaretPos_;
			if( caretPos_ > text.Length )
			{
				caretPos_ = text.Length;
			}
			selectionAnchorPosition = selectionFocusPosition = caretPos_;
			ownerTree_.OnFocused(BindedLine);
		}
		if( isFocused )
		{
			if( Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) )
			{
				desiredCaretPos_ = caretPos_ = caretPosition;
			}
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
		desiredCaretPos_ = caretPos_ = caretSelectPositionInternal = caretPositionInternal = GetCharacterIndexFromPosition(localMousePos) + m_DrawStart;
		//}

		UpdateLabel();
		eventData.Use();
	}
	
	protected Event processingEvent_ = new Event();
	public override void OnUpdateSelected(BaseEventData eventData)
	{
		if( !isFocused )
			return;

		//bool consumedEvent = false;
		while( Event.PopEvent(processingEvent_) )
		{
			if( processingEvent_.rawType == EventType.KeyDown )
			{
				//consumedEvent = true;

				var currentEventModifiers = processingEvent_.modifiers;
				bool ctrl = SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX ? (currentEventModifiers & EventModifiers.Command) != 0 : (currentEventModifiers & EventModifiers.Control) != 0;
				bool shift = (currentEventModifiers & EventModifiers.Shift) != 0;
				bool alt = (currentEventModifiers & EventModifiers.Alt) != 0;
				bool ctrlOnly = ctrl && !alt && !shift;

				switch( processingEvent_.keyCode )
				{
				case KeyCode.V:
					if( ctrlOnly )
					{
						// process in ownerTree
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
						KeyPressed(processingEvent_);
					}
					break;
				case KeyCode.M:
					if( ctrlOnly && BindedLine.Count > 0 )
					{
						BindedLine.IsFolded = !BindedLine.IsFolded;
						BindedLine.AdjustLayoutRecursive();
					}
					break;
				case KeyCode.Delete:
					{
						bool use = caretPos_ < text.Length;
						KeyPressed(processingEvent_);
						if( use ) ownerTree_.OnDeleteKeyConsumed();
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
							KeyPressed(processingEvent_);
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
							KeyPressed(processingEvent_);
						}
					}
					break;
				default:
					if( processingEvent_.keyCode == KeyCode.None && ownerTree_.HasSelection )
					{
						TextField newField = ownerTree_.DeleteSelection(true).Field;
						newField.KeyPressed(processingEvent_);
						newField.CaretPosision = newField.text.Length;
					}
					else
					{
						KeyPressed(processingEvent_);
					}
					break;
				}
			}
		}

		// ひらがな入力で、変換の最後の1文字だけ、BackspaceのKeyDownが来ない問題で、仕方なく毎回Update
		//if( consumedEvent )
		UpdateLabel();

		eventData.Use();
	}

	#endregion
}