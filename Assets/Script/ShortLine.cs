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
			if( BindedLine != null )
			{
				BindedLine.IsDone = value; ;
			}
			else
			{
				OnDoneChanged();
			}
			interactable = (isDone_ == false);
		}
	}
	protected bool isDone_;

	public bool IsSelected { get { return isSelected_; } }
	protected bool isSelected_ = false;

	public string Text { get { return textComponent_.text; } set { textComponent_.text = value; } }
	Text textComponent_;

	public Color Background { get { return image.color; } set { image.color = value; } }
	public Color GetColor() { return Background; }
	public void SetColor(Color color) { Background = color; }
	
	#endregion


	#region params

	CheckMark checkMark_;
	UIGaugeRenderer strikeLine_;
	Button doneButton_;

	ShortLineList ownerList_;

	bool shouldUpdateStrikeLine_;

	#endregion


	#region unity functions

	protected override void Awake()
	{
		base.Awake();
		textComponent_ = GetComponentInChildren<Text>();
		strikeLine_ = textComponent_.GetComponentInChildren<UIGaugeRenderer>(includeInactive: true);
		checkMark_ = GetComponentInChildren<CheckMark>(includeInactive: true);
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
		isDone_ = line.IsDone;
	}
	
	public void ShowBindedLine()
	{
		if( BindedLine != null )
		{
			if( BindedLine.Tree.IsActive == false )
			{
				BindedLine.Tree.IsActive = true;
			}
			BindedLine.Tree.UpdateScrollTo(BindedLine);
			BindedLine.Field.Select();
			BindedLine.Field.IsFocused = true;
		}
	}

	#endregion


	#region selection

	public override void OnSelect(BaseEventData eventData)
	{
		base.OnSelect(eventData);
		if( IsDone == false )
		{
			isSelected_ = true;
			transition = Transition.None;
			Background = GameContext.Config.ShortLineSelectionColor;
			ownerList_.OnSelect(this);
		}
	}

	public override void OnDeselect(BaseEventData eventData)
	{
		base.OnDeselect(eventData);
		if( IsDone == false )
		{
			isSelected_ = false;
			transition = Transition.ColorTint;
			Background = GameContext.Config.ShortLineColor;
			ownerList_.OnDeselect(this);
		}
	}

	#endregion


	#region done

	public void Done()
	{
		GameContext.CurrentActionManager = ownerList_.ActionManager;
		ownerList_.ActionManager.Execute(new Action(
			execute: () =>
			{
				IsDone = !IsDone;
			}));
	}

	public void OnDoneChanged(bool withAnim = true)
	{
		if( IsDone )
		{
			strikeLine_.gameObject.SetActive(true);
			checkMark_.gameObject.SetActive(true);
			Background = GameContext.Config.ShortLineBackColor;
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
			Background = GameContext.Config.ShortLineColor;
			textComponent_.color = Color.white;
		}

		ownerList_.OnDoneChanged(this);
	}

	public void UpdateStrikeLine()
	{
		if( shouldUpdateStrikeLine_ == false )
		{
			shouldUpdateStrikeLine_ = true;
			if( this.gameObject.activeInHierarchy )
			{
				StartCoroutine(UpdateStrikeLineCoroutine());
			}
		}
	}

	IEnumerator UpdateStrikeLineCoroutine()
	{
		yield return new WaitForEndOfFrame();

		TextGenerator gen = textComponent_.cachedTextGenerator;

		float charLength = gen.characters[gen.characters.Count - 1].cursorPos.x - gen.characters[0].cursorPos.x;
		charLength /= textComponent_.pixelsPerUnit;

		strikeLine_.SetLength(charLength);
		shouldUpdateStrikeLine_ = false;
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
