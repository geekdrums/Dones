using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;

public class TreeToggle : Toggle {

	TextField textField_;

	// Use this for initialization
	protected override void Start () {
		textField_ = GetComponentInParent<TextField>();
		if( textField_ != null )
		{
			onValueChanged.AsObservable().Subscribe(x =>
			{
				if( textField_.BindedLine != null )
					textField_.BindedLine.IsFolded = !isOn;

				textField_.IsFocused = true;
				AnimManager.AddAnim(targetGraphic, interactable && isOn ? 0 : 90, ParamType.RotationZ, AnimType.Time, GameContext.Config.AnimTime);
			}).AddTo(this);
		}
	}

	
	// Update is called once per frame
	void Update () {
	}
}
