using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;
using UnityEngine.EventSystems;

public class DateTextBox : CustomInputField
{
	UIGaugeRenderer underLine_;
	DateUI dateUI_;

	protected bool isEditing_ = false;

	protected override void Awake()
	{
		onEndEdit.AddListener(delegate { OnTextEdited(); });
		//onValidateInput += delegate (string input, int charIndex, char addedChar) { return OnValidate(addedChar); };
		underLine_ = GetComponentInChildren<UIGaugeRenderer>(includeInactive: true);
		dateUI_ = GetComponentInParent<DateUI>();
	}

	protected override void OnFocused()
	{
		if( isEditing_ == false )
		{
			isEditing_ = true;
			underLine_.gameObject.SetActive(true);
			underLine_.SetColor(Foreground);
			underLine_.Rate = 0.0f;
			AnimManager.AddAnim(underLine_, 1.0f, ParamType.GaugeRate, AnimType.Time, 0.15f);
			AnimManager.AddAnim(this, 0.4f, ParamType.TextAlphaColor, AnimType.Time, 0.15f);
		}
	}

	private void OnTextEdited()
	{
		if( isEditing_ )
		{
			isEditing_ = false;
			AnimManager.AddAnim(underLine_, 0.0f, ParamType.GaugeRate, AnimType.BounceOut, 0.15f, endOption: AnimEndOption.Deactivate);

			int newDate = -1;

			if( int.TryParse(text, out newDate) )
			{
				if( 1 <= newDate && newDate <= 31 )
				{
					DateTime newDateTime = new DateTime(dateUI_.Date.Year, dateUI_.Date.Month, newDate, dateUI_.Date.Hour, dateUI_.Date.Minute, dateUI_.Date.Second);
					if( newDateTime < DateTime.Today )
					{
						dateUI_.OnDateChanged(newDateTime);
					}
					else
					{
						newDate = -1;
					}
				}
				else
				{
					newDate = -1;
				}
			}

			if( newDate < 0 )
			{
				text = dateUI_.Date.Day.ToString();
				AnimManager.AddAnim(this, 1.0f, ParamType.TextAlphaColor, AnimType.Time, 0.15f);
			}
		}
	}

	private char OnTextValidate(char charToValidate)
	{
		//Checks if a dollar sign is entered....
		//if( charToValidate == '$' )
		//{
		//	// ... if it is change it to an empty character.
		//	charToValidate = '\0';
		//}
		return charToValidate;
	}
}