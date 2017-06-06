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

	
	// Update is called once per frame
	void Update () {
	}

	public void SetFold(bool isFolded, bool withAnim = true)
	{
		isOn = !isFolded;
		if( withAnim )
		{
			AnimManager.AddAnim(targetGraphic, interactable && isOn ? 0 : 90, ParamType.RotationZ, AnimType.Time, GameContext.Config.AnimTime);
		}
		else
		{
			transform.localRotation = Quaternion.AngleAxis(isOn ? 0 : 90, Vector3.forward);
		}
	}
}
