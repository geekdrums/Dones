using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;

public class TagToggle : Toggle {

	TagParent tagParent_;

	public Color TargetColor { get { return interactable == false ? Color.clear : (tagParent_ != null && tagParent_.IsFolded ? GameContext.Config.ToggleColor : GameContext.Config.ToggleOpenedColor); } }

	// Use this for initialization
	protected override void Start () {
		tagParent_ = GetComponentInParent<TagParent>();
		onValueChanged.AsObservable().Subscribe(x =>
		{
			bool isFolded = !isOn;
			if( tagParent_ != null && tagParent_.IsFolded != isFolded )
			{
				tagParent_.IsFolded = isFolded;
			}
		}).AddTo(this);
		if( GameContext.Config != null )
		{
			AnimToTargetVisual();
		}
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
		(targetGraphic as Image).color = TargetColor;
		targetGraphic.CrossFadeColor(Color.white, 0, true, true);
		AnimManager.AddAnim(targetGraphic, interactable && isOn ? 0 : 90, ParamType.RotationZ, AnimType.Time, GameContext.Config.AnimTime);
	}
}
