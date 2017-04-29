using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;
using UnityEngine.EventSystems;

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
			}
		}
	}

	public Color Background { get { return image_.color; } set { image_.color = value; } }
	public Color Foreground { get { return mesh_.color; } set { mesh_.color = value; } }

	protected Image image_;
	protected TextMesh mesh_;

	// Use this for initialization
	protected override void Awake()
	{
		base.Awake();
		mesh_ = GetComponent<TextMesh>();
		image_ = GetComponent<Image>();

		this.ObserveEveryValueChanged(x => x.isFocused)
			.Where(f => f)
			.Subscribe(x =>
			{
				GetComponentInParent<Tree>().OnFocused(BindedLine);
			}).AddTo(this);
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
}
