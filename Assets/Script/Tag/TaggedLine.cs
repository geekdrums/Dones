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
			UpdateStrikeLineLength();
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

	public bool IsDoneAnimating { get { return isDone_ && AnimManager.IsAnimating(strikeLine_); } }

	#endregion


	#region params

	CheckMark checkMark_;
	UIGaugeRenderer strikeLine_;
	UIMidairPrimitive listMark_;

	public TagParent Parent { get { return tagParent_; } }
	TagParent tagParent_;

	#endregion


	#region unity events

	protected override void Awake()
	{
		base.Awake();
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

	protected override void OnDisable()
	{
		base.OnDisable();
		
		if( BindedLine != null && tagParent_ != null && this.gameObject.activeSelf == false )
		{
			tagParent_.OnLineDisabled(this);
		}
	}

	#endregion


	#region binding

	public void Bind(Line line)
	{
		if( textComponent_ == null )
		{
			textComponent_ = GetComponentInChildren<Text>();
			strikeLine_ = textComponent_.GetComponentInChildren<UIGaugeRenderer>(includeInactive: true);
			checkMark_ = GetComponentInChildren<CheckMark>(includeInactive: true);
			listMark_ = GetComponentInChildren<Button>().GetComponentInChildren<UIMidairPrimitive>();
			tagParent_ = GetComponentInParent<TagParent>();
		}

		isSelected_ = false;
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
	

	#region selection

	public override void OnSelect(BaseEventData eventData)
	{
		base.OnSelect(eventData);
		isSelected_ = true;
		tagParent_.OnSelect(this);
	}

	public override void OnDeselect(BaseEventData eventData)
	{
		base.OnDeselect(eventData);
		isSelected_ = false;
		tagParent_.OnDeselect(this);
	}

	#endregion


	#region done

	public void Done()
	{
		if( tagParent_.IsRepeat )
		{
			BindedLine.Tree.RepeatDone(BindedLine);
			OnRepeatDone();
		}
		else
		{
			BindedLine.Tree.Done(BindedLine);
		}
		ShowBindedLine();
	}

	public void OnRepeatDone()
	{
		strikeLine_.gameObject.SetActive(true);
		checkMark_.gameObject.SetActive(true);

		float backToUsualTime = 0.7f;

		// strike anim
		strikeLine_.Rate = 0.0f;
		AnimManager.AddAnim(strikeLine_, 1.0f, ParamType.GaugeRate, AnimType.Time, 0.15f);
		AnimManager.AddAnim(strikeLine_, 0.0f, ParamType.GaugeRate, AnimType.Time, 0.1f, backToUsualTime, endOption: AnimEndOption.Deactivate);

		// check anim
		checkMark_.CheckAndUncheck(backToUsualTime);

		// list anim
		listMark_.SetColor(Color.clear);
		AnimManager.AddAnim(listMark_, Color.black, ParamType.Color, AnimType.Time, 0.15f, backToUsualTime);

		// color anim
		SetColor(GameContext.Config.DoneTextColor);
		AnimManager.AddAnim(this.gameObject, GameContext.Config.TextColor, ParamType.Color, AnimType.Time, 0.1f, backToUsualTime);
	}

	public void OnDoneChanged(bool withAnim = true)
	{
		if( IsDone )
		{
			strikeLine_.gameObject.SetActive(true);
			checkMark_.gameObject.SetActive(true);
			listMark_.SetColor(Color.clear);
			UpdateStrikeLineLength();

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

	Color GetTargetColor()
	{
		if( IsDone )
		{
			return GameContext.Config.DoneTextColor;
		}
		else
		{
			return GameContext.Config.TextColor;
		}
	}

	void UpdateStrikeLineLength()
	{
		float charLength = GameContext.TextLengthHelper.AbbreviateText(textComponent_, GameContext.Config.TagListTextMaxWidth, "...");
		strikeLine_.SetLength(charLength);
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
			OnDeselect(null);
		}
	}

	#endregion
}
