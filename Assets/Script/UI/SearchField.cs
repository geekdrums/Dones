using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;
using UnityEngine.EventSystems;

public class SearchField : CustomInputField
{
	GameObject Icon;
	
	IDisposable textSubscription_;

	Color initialBackColor_;

	bool textChanged_ = false;


	public void Initialize()
	{
		textSubscription_ = onValueChanged.AsObservable().Subscribe(text =>
		{
			OnTextChanged(text);
		});
		Icon = transform.Find("icon").gameObject;
		initialBackColor_ = Background;
	}

	// Update is called once per frame
	void Update ()
	{
		if( isFocused && textChanged_ )
		{
			textChanged_ = false;
		}
	}


	protected override void OnFocused()
	{
		placeholder.enabled = false;
		targetGraphic.enabled = false;
	}

	public override void OnDeselect(BaseEventData eventData)
	{
		base.OnDeselect(eventData);

		targetGraphic.enabled = true;

		if( text == "" )
		{
			placeholder.enabled = true;
		}
	}

	//float lastTextInputTime_ = 0;

	protected void OnTextChanged(string newText)
	{
		textChanged_ = true;
		/*
		if( Time.time - lastTextInputTime_ > GameContext.Config.SearchInputIntervalTime )
		{

		}

		lastTextInputTime_ = Time.time;
		*/
	}
}
