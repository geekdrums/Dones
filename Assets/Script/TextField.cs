using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;
using UnityEngine.EventSystems;

public class TextField : InputField
{
	public static string Clipboard
	{
		get
		{
			return GUIUtility.systemCopyBuffer;
		}
		set
		{
			GUIUtility.systemCopyBuffer = value;
		}
	}

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
			}
		}
	}

	public Color Background { get { return image_.color; } set { image_.color = value; } }
	public Color Foreground { get { return mesh_.color; } set { mesh_.color = value; } }

	protected Image image_;
	protected TextMesh mesh_;
	protected Tree ownerTree_;

	// Use this for initialization
	protected override void Start()
	{
		//base.Awake();
		mesh_ = GetComponent<TextMesh>();
		image_ = GetComponent<Image>();
		ownerTree_ = GetComponentInParent<Tree>();
	}

	// Update is called once per frame
	void Update()
	{
		if( isFocused )
		{
			if( Input.GetKeyDown(KeyCode.DownArrow) == false && Input.GetKeyDown(KeyCode.UpArrow) == false )
			{
				caretPos_ = caretPosition;
			}
			ForceLabelUpdate();
		}
	}

	protected override void LateUpdate()
	{
		bool oldIsFocused = isFocused;

		base.LateUpdate();

		if( oldIsFocused != isFocused )
		{
			selectionAnchorPosition = selectionFocusPosition = caretPos_;
			ownerTree_.OnFocused(BindedLine);
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
		
		Vector2 localMousePos;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(textComponent.rectTransform, eventData.position, eventData.pressEventCamera, out localMousePos);
		caretPos_ = caretSelectPositionInternal = caretPositionInternal = GetCharacterIndexFromPosition(localMousePos) + m_DrawStart;

		UpdateLabel();
		eventData.Use();
	}

	protected static string[] separator = new string[] { System.Environment.NewLine };
	protected static string[] tabstrings = new string[] { "	", "    " };
	protected void Paste()
	{
		string[] cilpboardLines = Clipboard.Split(separator, System.StringSplitOptions.None);
		Append(cilpboardLines[0]);

		int oldLevel = 0;
		int currentLevel = 0;
		Line parent = BindedLine.Parent;
		Line brother = BindedLine;
		for( int i = 1; i < cilpboardLines.Length; ++i )
		{
			string text = cilpboardLines[i];
			currentLevel = 0;
			while( text.StartsWith("	") )
			{
				++currentLevel;
				text = text.Remove(0, 1);
			}

			Line line = new Line(text);
			if( currentLevel > oldLevel )
			{
				brother.Add(line);
				parent = brother;
			}
			else if( currentLevel == oldLevel )
			{
				parent.Insert(brother.IndexInParent + 1, line);
			}
			else// currentLevel < oldLevel 
			{
				for( int level = oldLevel; level > currentLevel; --level )
				{
					if( parent.Parent == null ) break;
					
					brother = parent;
					parent = parent.Parent;
				}
				parent.Insert(brother.IndexInParent + 1, line);
			}
			ownerTree_.InstantiateLine(line);
			brother = line;
			oldLevel = currentLevel;
		}
	}


	protected Event processingEvent_ = new Event();
	public override void OnUpdateSelected(BaseEventData eventData)
	{
		if( !isFocused )
			return;

		bool consumedEvent = false;
		while( Event.PopEvent(processingEvent_) )
		{
			if( processingEvent_.rawType == EventType.KeyDown )
			{
				consumedEvent = true;

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
						Paste();
					}
					break;
				case KeyCode.M:
					if( ctrlOnly && BindedLine.Count > 0 )
					{
						BindedLine.IsFolded = !BindedLine.IsFolded;
					}
					break;
				default:
					KeyPressed(processingEvent_);
					break;
				}
			}
		}

		if( consumedEvent )
			UpdateLabel();

		eventData.Use();
	}



}
