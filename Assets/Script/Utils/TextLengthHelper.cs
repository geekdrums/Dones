using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TextLengthHelper : MonoBehaviour {

	class UpdateTextLengthRequest
	{
		public Text TextComponent;
		public event Action OnTextLengthCalculated;

		public void OnRequestFinished()
		{
			OnTextLengthCalculated();
		}
	}

	List<UpdateTextLengthRequest> requests_ = new List<UpdateTextLengthRequest>();

	// Use this for initialization
	void Awake () {
		GameContext.TextLengthHelper = this;
	}
	
	// Update is called once per frame
	void Update () {

	}

	public static float GetTextRectLength(TextGenerator textGen, int index)
	{
		if( textGen.characters.Count == 0 )
		{
			return 0;
		}

		index = Math.Min(textGen.characters.Count - 1, Math.Max(0, index));
		return textGen.characters[index].cursorPos.x + textGen.characters[index].charWidth - textGen.characters[0].cursorPos.x;
	}

	public static float GetFullTextRectLength(TextGenerator textGen)
	{
		return GetTextRectLength(textGen, textGen.characterCount - 1);
	}

	public void Request(Text textComponent, Action onTextLengthCalculated)
	{
		UpdateTextLengthRequest request = requests_.Find((UpdateTextLengthRequest existReq)=>existReq.TextComponent == textComponent);
		if( request == null )
		{
			request = new UpdateTextLengthRequest();
			request.TextComponent = textComponent;
			request.OnTextLengthCalculated += onTextLengthCalculated;
			requests_.Add(request);
			StartCoroutine(UpdateTextLengthCoroutine(request));
		}
		else
		{
			request.OnTextLengthCalculated += onTextLengthCalculated;
		}
	}

	public void CancelRequest(Text textComponent)
	{
		UpdateTextLengthRequest request = requests_.Find((UpdateTextLengthRequest existReq) => existReq.TextComponent == textComponent);
		if( request != null )
		{
			requests_.Remove(request);
		}
	}

	IEnumerator UpdateTextLengthCoroutine(UpdateTextLengthRequest request)
	{
		yield return new WaitWhile(() => request.TextComponent.gameObject.activeInHierarchy == false
										|| request.TextComponent.cachedTextGenerator.characterCount != request.TextComponent.text.Length + 1);
		
		request.OnRequestFinished();
		requests_.Remove(request);
	}

	public float AbbreviateText(Text textComponent, float maxWidth, string substituteText = "...")
	{
		TextGenerator gen = textComponent.cachedTextGenerator;
		float charLength = GetFullTextRectLength(gen);
		if( charLength > maxWidth )
		{
			int maxCharCount = 0;
			for( int i = 1; i < gen.characters.Count; ++i )
			{
				if( maxWidth < GetTextRectLength(gen, i) )
				{
					maxCharCount = i - 1;
					charLength = GetTextRectLength(gen, i - 1);
					break;
				}
			}
			textComponent.text = textComponent.text.Substring(0, maxCharCount) + substituteText;
		}
		return charLength;
	}
}
