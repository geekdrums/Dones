using System.Collections;
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

	public int CaretPosision
	{
		get { return cachedCaretPos_; }
		set { selectionAnchorPosition = selectionFocusPosition = cachedCaretPos_ = desiredCaretPos_ = value; }
	}
	protected int cachedCaretPos_;
	protected static int desiredCaretPos_;
	public int ActualCaretPosition { get { return m_CaretSelectPosition; } set { m_CaretSelectPosition = m_CaretPosition = value; } }

	public Color Foreground { get { return textComponent.color; } set { textComponent.color = value; } }
	public Color Background { get { return targetGraphic.canvasRenderer.GetColor(); } set { targetGraphic.CrossFadeColor(value, 0.0f, true, true); } }
	public void SetColor(Color color) { Background = color; }
	public Color GetColor() { return Background;  }

	public Rect Rect { get { return new Rect(targetGraphic.rectTransform.position, targetGraphic.rectTransform.sizeDelta); } }
	public float RectY { get { return targetGraphic.rectTransform.position.y; } }

	protected UIGaugeRenderer strikeLine_;
	protected CheckMark checkMark_;
	protected UIMidairPrimitive listMark_;
	protected bool shouldUpdateTextLength_ = false;

	#endregion


	#region unity functions

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

	public void SetTextDirectly(string text)
	{
		// onValueChangedを発生させないテキスト設定。
		// Fieldからのユーザーの入力のみをイベントとして取得したいので、
		// Lineクラスからシステム的に設定される場合（Undoや改行など）は、
		// この関数でイベント呼び出しを避ける。
		m_Text = text;
		UpdateLabel();
		if( BindedLine.IsDone || BindedLine.IsOnList ) OnTextLengthChanged();
	}

	public void SetDone(bool isDone, bool withAnim = true)
	{
		strikeLine_.gameObject.SetActive(isDone);
		checkMark_.gameObject.SetActive(isDone);
		Foreground = GetDesiredTextColor();
		if( isDone )
		{
			listMark_.gameObject.SetActive(false);
			OnTextLengthChanged();

			if( withAnim )
			{
				strikeLine_.Rate = 0.0f;
				AnimManager.AddAnim(strikeLine_, 1.0f, ParamType.GaugeRate, AnimType.Time, 0.15f);
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
		if( isOnList && BindedLine.IsDone == false )
		{
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

	Color GetDesiredTextColor()
	{
		if( BindedLine != null )
		{
			return (BindedLine.IsDone ? GameContext.Config.DoneTextColor : (BindedLine.IsOnList ? GameContext.Config.ShortLineColor : GameContext.Config.TextColor));
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
		yield return new WaitForEndOfFrame();

		TextGenerator gen = m_TextComponent.cachedTextGenerator;

		float charLength = gen.characters[gen.characters.Count - 1].cursorPos.x - gen.characters[0].cursorPos.x;
		charLength /= m_TextComponent.pixelsPerUnit;

		strikeLine_.SetLength(charLength + 10);

		checkMark_.SetPositionX(charLength + 5);
		listMark_.GetComponent<RectTransform>().anchoredPosition = new Vector2(charLength + 15, listMark_.transform.localPosition.y);

		shouldUpdateTextLength_ = false;
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

		UpdateLabel();
		eventData.Use();
	}
	
	protected Event processingEvent_ = new Event();
	public override void OnUpdateSelected(BaseEventData eventData)
	{
		if( !isFocused || BindedLine == null || BindedLine.Tree == null )
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

				cachedCaretPos_ = m_CaretSelectPosition;
				switch( processingEvent_.keyCode )
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
						KeyPressed(processingEvent_);
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
				case KeyCode.Delete:
					{
						if( BindedLine.Tree.HasSelection )
						{
							// process in ownerTree
						}
						else
						{
							bool use = cachedCaretPos_ < text.Length;
							KeyPressed(processingEvent_);
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
							KeyPressed(processingEvent_);
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
							KeyPressed(processingEvent_);
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
							KeyPressed(processingEvent_);
							BindedLine.FixTextInputAction();
						}
					}
					break;
				case KeyCode.RightArrow:
				case KeyCode.LeftArrow:
					{
						KeyPressed(processingEvent_);
						desiredCaretPos_ = m_CaretSelectPosition;
						BindedLine.FixTextInputAction();
					}
					break;
				default:
					if( ctrlOnly && processingEvent_.keyCode == KeyCode.None && processingEvent_.character.ToString() == " " )
					{
						// process in ownerTree
					}
					else if( processingEvent_.keyCode == KeyCode.None && BindedLine.Tree.HasSelection && processingEvent_.character.ToString() != Line.TabString )
					{
						TextField newField = BindedLine.Tree.DeleteSelection().Field;
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
