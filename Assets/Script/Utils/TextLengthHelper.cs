using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TextLengthHelper : MonoBehaviour {
	
	// Use this for initialization
	void Awake () {
		GameContext.TextLengthHelper = this;
	}
	
	// Update is called once per frame
	void Update () {

	}

	public static float GetTextRectLength(Text text, int index)
	{
		if( index < 0 )
		{
			return 0;
		}
		else if( index >= text.text.Length )
		{
			return text.preferredWidth;
		}

		int sum = 0;
		for( int i = 0; i <= index; ++i )
		{
			if( text.text.Length <= i )
			{
				break;
			}

			CharacterInfo chInfo;
			text.font.GetCharacterInfo(text.text[i], out chInfo);
			sum += chInfo.advance;
		}

		return sum;
	}

	public static float GetFullTextRectLength(Text text)
	{
		return text.preferredWidth;
	}

	public float AbbreviateText(Text text, float maxWidth, string substituteText = "...")
	{
		float charLength = GetFullTextRectLength(text);
		if( charLength > maxWidth )
		{
			int maxCharCount = 0;
			for( int i = 1; i < text.text.Length; ++i )
			{
				if( maxWidth < GetTextRectLength(text, i) )
				{
					maxCharCount = i - 1;
					charLength = GetTextRectLength(text, i - 1);
					break;
				}
			}
			text.text = text.text.Substring(0, maxCharCount) + substituteText;
		}
		return charLength;
	}
}
