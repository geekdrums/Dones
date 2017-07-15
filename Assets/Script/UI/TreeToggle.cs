using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;

public class TreeToggle : Toggle {

	TextField textField_;
	UIGaugeRenderer verticalLine_;
	bool wasAnimRequested_ = false;

	public Color TargetColor { get { return interactable == false ? Color.clear : (textField_ != null && textField_.BindedLine.IsFolded ? GameContext.Config.ToggleColor : GameContext.Config.ToggleOpenedColor); } }

	// Use this for initialization
	protected override void Start () {
		textField_ = GetComponentInParent<TextField>();
		verticalLine_ = GetComponentInChildren<UIGaugeRenderer>(includeInactive: true);
		onValueChanged.AsObservable().Subscribe(x =>
		{
			Line line = textField_.BindedLine;
			bool isFolded = !isOn;
			if( line != null && line.IsFolded != isFolded )
			{
				line.Tree.OnFoldUpdated(line, isFolded);
			}
		}).AddTo(this);
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		AnimToTargetVisual();
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		wasAnimRequested_ = false;
	}

	// Update is called once per frame
	void Update () {

	}

	public void SetFold(bool isFolded)
	{
		isOn = !isFolded;
		AnimToTargetVisual();
	}

	public void AnimToTargetVisual()
	{
		if( wasAnimRequested_ == false && gameObject.activeInHierarchy )
		{
			wasAnimRequested_ = true;
			StartCoroutine(AnimToTargetVisualCoroutine());
		}
	}
	IEnumerator AnimToTargetVisualCoroutine()
	{
		yield return new WaitForEndOfFrame();

		if( textField_ != null && textField_.BindedLine != null )
		{
			this.interactable = textField_ != null && textField_.BindedLine.Count > 0;
			(targetGraphic as UIMidairPrimitive).SetColor(TargetColor);
			targetGraphic.CrossFadeColor(Color.white, 0, true, true);
			AnimManager.AddAnim(targetGraphic, interactable && isOn ? 0 : 90, ParamType.RotationZ, AnimType.Time, GameContext.Config.AnimTime);
			if( verticalLine_ != null )
			{
				float lineHeight = 0;
				if( textField_ != null )
				{
					lineHeight = textField_.BindedLine.VisibleChildCount * GameContext.Config.HeightPerLine;
				}
				AnimManager.AddAnim(verticalLine_, lineHeight, ParamType.GaugeLength, AnimType.Time, GameContext.Config.AnimTime);
				verticalLine_.rectTransform.sizeDelta = new Vector2(1, lineHeight);
			}
		}
		wasAnimRequested_ = false;
	}
}
