using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;

public class ShortLine : Selectable, IDragHandler, IBeginDragHandler, IEndDragHandler, IColoredObject
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
			if( shouldUpdateTextLength_ == false )
			{
				shouldUpdateTextLength_ = true;
				StartCoroutine(UpdateTextLengthCoroutine());
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
	public Color GetColor() { return image.color; }
	public void SetColor(Color color) { image.color = color; }
	
	public Color TargetColor
	{
		get
		{
			if( isSelected_ )
			{
				return IsDone ? GameContext.Config.ShortLineBackSelectionColor : GameContext.Config.ShortLineSelectionColor;
			}
			else if( IsDone )
			{
				return GameContext.Config.ShortLineBackColor;
			}
			else
			{
				return Color.Lerp(GameContext.Config.ShortLineColor, GameContext.Config.ShortLineBackColor, ownerList_.IndexOf(this) * ownerList_.Gradation);
			}
		}
	}

	#endregion


	#region params

	CheckMark checkMark_;
	UIGaugeRenderer strikeLine_;
	Button doneButton_;
	UIMidairPrimitive listMark_;

	ShortLineList ownerList_;

	bool shouldUpdateTextLength_;

	#endregion


	#region unity functions

	protected override void Awake()
	{
		base.Awake();
		textComponent_ = GetComponentInChildren<Text>();
		strikeLine_ = textComponent_.GetComponentInChildren<UIGaugeRenderer>(includeInactive: true);
		checkMark_ = GetComponentInChildren<CheckMark>(includeInactive: true);
		listMark_ = GetComponentInChildren<UIMidairPrimitive>(includeInactive: true);
		ownerList_ = GetComponentInParent<ShortLineList>();
		doneButton_ = GetComponentInChildren<Button>();
		this.OnPointerDownAsObservable()
			.TimeInterval()
			.Select(t => t.Interval.TotalSeconds)
			.Buffer(2, 1)
			.Where(list => list[0] > GameContext.Config.DoubleClickInterval)
			.Where(list => list.Count > 1 ? list[1] <= GameContext.Config.DoubleClickInterval : false)
			.Subscribe(_ => ShowBindedLine()).AddTo(this);
	}

	// Update is called once per frame
	void Update () {

	}

	#endregion


	#region binding

	public void Bind(Line line)
	{
		BindedLine = line;
		Text = line.Text;
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
			BindedLine.Tree.ScrollTo(BindedLine);
			BindedLine.Field.Select();
			BindedLine.Field.IsFocused = true;
		}
	}

	#endregion


	#region selection

	public override void OnSelect(BaseEventData eventData)
	{
		base.OnSelect(eventData);
		isSelected_ = true;
		Background = TargetColor;
		ownerList_.OnSelect(this);
	}

	public override void OnDeselect(BaseEventData eventData)
	{
		base.OnDeselect(eventData);
		isSelected_ = false;
		Background = TargetColor;
		ownerList_.OnDeselect(this);
	}

	#endregion


	#region done

	public void Done()
	{
		Line line = BindedLine;
		TreeNote treeNote = line.Tree as TreeNote;
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
			listMark_.gameObject.SetActive(false);
			textComponent_.color = GameContext.Config.DoneTextColor;
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
			listMark_.gameObject.SetActive(true);
			textComponent_.color = Color.white;
		}

		ownerList_.OnDoneChanged(this);
	}

	public void UpdateStrikeLine()
	{
		if( shouldUpdateTextLength_ == false )
		{
			shouldUpdateTextLength_ = true;
			StartCoroutine(UpdateTextLengthCoroutine());
		}
	}

	IEnumerator UpdateTextLengthCoroutine()
	{
		yield return new WaitForEndOfFrame();

		TextGenerator gen = textComponent_.cachedTextGenerator;

		float charLength = gen.characters[gen.characters.Count - 1].cursorPos.x - gen.characters[0].cursorPos.x;
		float maxWidth = ownerList_.LineWidth - 58;
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
		Line line = BindedLine;
		BindedLine.Tree.ActionManager.Execute(new Action(
			execute: () =>
			{
				line.IsOnList = false;
				ShortLine shortline = ownerList_.FindBindedLine(line);
				// 最初はthisだけどRedoしたら違うオブジェクトになっているため
				if( shortline != null )
				{
					ownerList_.RemoveShortLine(shortline);
				}
			},
			undo: () =>
			{
				line.IsOnList = true;
				ownerList_.InstantiateShortLine(line);
			}));
	}

	#endregion


	#region drag
	
	public void OnBeginDrag(PointerEventData eventData)
	{
		if( IsDone == false )
		{
			ownerList_.OnBeginDrag(this);
		}
	}

	public void OnDrag(PointerEventData eventData)
	{
		if( IsDone == false )
		{
			ownerList_.OnDragging(this, eventData);
		}
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if( IsDone == false )
		{
			ownerList_.OnEndDrag(this);
		}
	}

	#endregion
}
