using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShortLine : Selectable, IDragHandler, IBeginDragHandler, IEndDragHandler, IColoredObject {

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
			if( BindedLine != null )
			{
				BindedLine.IsDone = value; ;
			}
			isDone_ = value;
			interactable = (isDone_ == false);
			OnDoneChanged();
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

	CheckMark checkMark_;
	UIGaugeRenderer strikeLine_;
	Button doneButton_;

	ShortLineList ownerList_;

	bool shouldUpdateStrikeLine_;

	protected override void Awake()
	{
		base.Awake();
		textComponent_ = GetComponentInChildren<Text>();
		strikeLine_ = textComponent_.GetComponentInChildren<UIGaugeRenderer>(includeInactive: true);
		checkMark_ = GetComponentInChildren<CheckMark>(includeInactive: true);
		ownerList_ = GetComponentInParent<ShortLineList>();
		doneButton_ = GetComponentInChildren<Button>();
	}

	// Update is called once per frame
	void Update () {
	}

	public void Bind(Line line)
	{
		BindedLine = line;
		Text = line.Text;
		isDone_ = line.IsDone;
	}


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
		IsDone = !IsDone;
	}

	public void OnDoneChanged(bool withAnim = true)
	{
		if( IsDone )
		{
			strikeLine_.gameObject.SetActive(true);
			checkMark_.gameObject.SetActive(true);
			Background = GameContext.Config.ShortLineBackColor;
			textComponent_.color = GameContext.Config.DoneTextColor;
			doneButton_.transition = Transition.None;
			doneButton_.targetGraphic.color = Color.clear;
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
			doneButton_.targetGraphic.color = GameContext.Config.ShortLineColor;
			doneButton_.transition = Transition.ColorTint;
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
