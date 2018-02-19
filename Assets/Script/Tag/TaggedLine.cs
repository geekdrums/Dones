using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;

// Window > TagList > TagParent > [ TaggedLine ]
public class TaggedLine : Selectable, IDragHandler, IBeginDragHandler, IEndDragHandler, IColoredObject
{	
	#region properties

	public Line BindedLine { get; private set; }

	public bool IsDone
	{
		get
		{
			if( BindedLine != null )
			{
				return BindedLine.IsDone;
			}
			else return isDone_;
		}
		set
		{
			isDone_ = value;
			if( BindedLine != null && BindedLine.IsDone != isDone_ )
			{
				BindedLine.IsDone = value;
			}
			OnDoneChanged();
		}
	}
	protected bool isDone_;

	public bool IsSelected { get { return isSelected_; } }
	protected bool isSelected_ = false;

	public string Text
	{
		get { return textComponent_.text; }
		set
		{
			textComponent_.text = value;
#if UNITY_EDITOR
			name = value;
#endif
			if( shouldUpdateTextLength_ == false )
			{
				shouldUpdateTextLength_ = true;
				if( this.gameObject.activeInHierarchy )
				{
					StartCoroutine(UpdateTextLengthCoroutine());
				}
			}
		}
	}
	Text textComponent_;

	public override string ToString()
	{
		return Text;
	}

	public Color Background
	{
		get { return image.color; }
		set
		{
			AnimManager.RemoveOtherAnim(gameObject, ParamType.Color);
			image.color = value;
		}
	}
	public Color GetColor() { return textComponent_.color; }
	public void SetColor(Color color) { textComponent_.color = color; }

	#endregion


	#region params

	CheckMark checkMark_;
	UIGaugeRenderer strikeLine_;
	UIMidairPrimitive listMark_;

	public TagParent Parent { get { return tagParent_; } }
	TagParent tagParent_;

	bool shouldUpdateTextLength_;

	#endregion


	#region unity events

	protected override void Awake()
	{
		base.Awake();
		textComponent_ = GetComponentInChildren<Text>();
		strikeLine_ = textComponent_.GetComponentInChildren<UIGaugeRenderer>(includeInactive: true);
		checkMark_ = GetComponentInChildren<CheckMark>(includeInactive: true);
		listMark_ = GetComponentInChildren<Button>().GetComponentInChildren<UIMidairPrimitive>();
		tagParent_ = GetComponentInParent<TagParent>();
		this.OnPointerDownAsObservable()
			.TimeInterval()
			.Select(t => t.Interval.TotalSeconds)
			.Buffer(2, 1)
			.Where(list => list[0] > GameContext.Config.DoubleClickInterval)
			.Where(list => list.Count > 1 ? list[1] <= GameContext.Config.DoubleClickInterval : false)
			.Subscribe(_ => ShowBindedLine()).AddTo(this);
		this.OnPointerDownAsObservable().Subscribe(_ => Select()).AddTo(this);
	}

	// Update is called once per frame
	void Update() {

	}


	protected override void OnEnable()
	{
		base.OnEnable();
		if( this.gameObject.activeInHierarchy && shouldUpdateTextLength_ )
		{
			StartCoroutine(UpdateTextLengthCoroutine());
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		
		if( BindedLine != null && tagParent_ != null && this.gameObject.activeSelf == false )
		{
			tagParent_.OnLineDisabled(this);
		}
		shouldUpdateTextLength_ = false;
	}

	#endregion


	#region binding

	public void Bind(Line line)
	{
		BindedLine = line;
		Text = line.TextWithoutHashTags;
		IsDone = line.IsDone;
	}

	public void ShowBindedLine()
	{
		if( BindedLine != null )
		{
			if( BindedLine.IsVisible == false )
			{
				BindedLine.IsVisible = true;
			}
			BindedLine.Tree.OwnerNote.ScrollTo(BindedLine);
			BindedLine.Field.Select();
			BindedLine.Field.IsFocused = true;
			BindedLine.Field.SetSelection(0, BindedLine.Text.Length);
		}
	}

	#endregion


	#region events

	public void OnActiveNoteChanged()
	{
		SetColor(GetTargetColor());
	}

	#endregion


	#region selection

	public override void OnSelect(BaseEventData eventData)
	{
		base.OnSelect(eventData);
		isSelected_ = true;
		image.color = GameContext.Config.TagSelectionColor;
		tagParent_.OnSelect(this);
	}

	public override void OnDeselect(BaseEventData eventData)
	{
		base.OnDeselect(eventData);
		isSelected_ = false;
		image.color = Color.white;
		tagParent_.OnDeselect(this);
	}

	#endregion


	#region done

	public void Done()
	{
		Line line = BindedLine;
		TreeNote treeNote = line.Tree.OwnerNote as TreeNote;
		line.Tree.ActionManager.Execute(new Action(
			execute: () =>
			{
				line.IsDone = !line.IsDone;
				if( treeNote != null )
				{
					treeNote.LogNote.OnDoneChanged(line);
				}
			}));
	}

	public void OnDoneChanged(bool withAnim = true)
	{
		if( IsDone )
		{
			strikeLine_.gameObject.SetActive(true);
			checkMark_.gameObject.SetActive(true);
			listMark_.SetColor(Color.clear);
			UpdateStrikeLine();

			if( withAnim )
			{
				strikeLine_.Rate = 0.0f;
				AnimManager.AddAnim(strikeLine_, 1.0f, ParamType.GaugeRate, AnimType.Time, 0.15f);
				checkMark_.Check();
			}
		}
		else
		{
			strikeLine_.gameObject.SetActive(false);
			checkMark_.gameObject.SetActive(false);
			listMark_.SetColor(Color.black);
		}
		SetColor(GetTargetColor());

		tagParent_.OnDoneChanged(this);
	}

	public void UpdateStrikeLine()
	{
		if( shouldUpdateTextLength_ == false )
		{
			shouldUpdateTextLength_ = true;
			StartCoroutine(UpdateTextLengthCoroutine());
		}
	}

	Color GetTargetColor()
	{
		if( IsDone )
		{
			return GameContext.Config.DoneTextColor;
		}
		else if( BindedLine.Tree.OwnerNote.IsActive || GameContext.Window.MainTabGroup.ActiveNote is TreeNote == false )
		{
			return GameContext.Config.TextColor;
		}
		else
		{
			return GameContext.Config.TagSubTextColor;
		}
	}

	IEnumerator UpdateTextLengthCoroutine()
	{
		yield return new WaitForEndOfFrame();

		TextGenerator gen = textComponent_.cachedTextGenerator;

		float charLength = gen.characters[gen.characters.Count - 1].cursorPos.x - gen.characters[0].cursorPos.x;
		float maxWidth = GameContext.Config.TagListWidth - GameContext.Config.TagCommaInterval;
		if( charLength > maxWidth )
		{
			int maxCharCount = 0;
			for( int i = 1; i < gen.characters.Count; ++i )
			{
				if( maxWidth < gen.characters[i].cursorPos.x - gen.characters[0].cursorPos.x )
				{
					maxCharCount = i - 1;
					charLength = gen.characters[i - 1].cursorPos.x - gen.characters[0].cursorPos.x;
					break;
				}
			}
			Text = Text.Substring(0, maxCharCount) + "...";
		}

		strikeLine_.SetLength(charLength);
		shouldUpdateTextLength_ = false;
	}

	public void Remove()
	{
		tagParent_.RemoveTaggedLine(this);
	}

	#endregion


	#region drag
	
	public void OnBeginDrag(PointerEventData eventData)
	{
		if( IsDone == false )
		{
			tagParent_.OnBeginDragLine(this);
		}
	}

	public void OnDrag(PointerEventData eventData)
	{
		if( IsDone == false )
		{
			tagParent_.OnDraggingLine(this, eventData);
		}
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if( IsDone == false )
		{
			tagParent_.OnEndDragLine(this);
		}
	}

	#endregion
}
