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
	public class SearchResult
	{
		public Line Line;
		public int Index;
		public UIMidairRect Rect;
	}

	#region params

	IDisposable textSubscription_;
	bool textChanged_ = false;

	List<SearchResult> searchResults_ = new List<SearchResult>();
	int focusResultIndex_ = -1;

	HeapManager<UIMidairRect> rectHeap_ = new HeapManager<UIMidairRect>();
	UIMidairRect shade_;

	#endregion

	public void Initialize()
	{
		rectHeap_.Initialize(10, GetComponentsInChildren<UIMidairRect>()[0], this.transform);
		shade_ = GetComponentsInChildren<UIMidairRect>()[1];
		textSubscription_ = onValueChanged.AsObservable().Subscribe(text =>
		{
			OnTextChanged(text);
		});
	}


	#region unity functions

	void Update ()
	{
		if( isFocused )
		{
			if( textChanged_ )
			{
				Search();
				textChanged_ = false;
			}

			if( Input.GetKeyDown(KeyCode.KeypadEnter) )
			{

			}
		}
	}
	
	#endregion

	void ClearSearchResult()
	{
		foreach( SearchResult result in searchResults_ )
		{
			if( result.Rect != null )
			{
				rectHeap_.BackToHeap(result.Rect);
			}
		}
		searchResults_.Clear();
		focusResultIndex_ = -1;
	}

	public void Search()
	{
		ClearSearchResult();

		searchResults_.AddRange(GameContext.Window.Note.Tree.Search(text));

		Line lastFocusedLine = GameContext.Window.Note.Tree.LastFocusedLine;
		float lastTargetAbsolutePositionY = 0;
		if( lastFocusedLine != null && lastFocusedLine.Field != null )
		{
			lastTargetAbsolutePositionY = lastFocusedLine.TargetAbsolutePosition.y;
		}

		for( int i = 0; i < searchResults_.Count; ++i )
		{
			SearchResult result = searchResults_[i];
			if( result.Line.IsVisible && result.Line.Field != null )
			{
				InstantiateSearchRect(result);
			}

			if( focusResultIndex_ < 0 )
			{
				if( lastFocusedLine == null || lastFocusedLine.Field != null &&
					lastTargetAbsolutePositionY >= result.Line.TargetAbsolutePosition.y )
				{
					focusResultIndex_ = i;
				}
			}
		}

		if( focusResultIndex_ >= 0 )
		{
			if( searchResults_[focusResultIndex_].Rect != null )
			{
				searchResults_[focusResultIndex_].Rect.SetColor(GameContext.Config.SearchFocusColor);
			}
		}
	}

	void InstantiateSearchRect(SearchResult result)
	{
		result.Rect = rectHeap_.ReviveOrInstantiate(result.Line.Field.transform);
		result.Rect.SetColor(GameContext.Config.SearchUnfocusColor);
		
		GameContext.TextLengthHelper.Request(result.Line.Field.textComponent, () =>
		{
			float x = result.Index > 0 ? result.Line.Field.GetTextRectLength(result.Index - 1) : 0;
			float width = result.Line.Field.GetTextRectLength(result.Index + text.Length - 1) - x;

			result.Rect.rectTransform.anchoredPosition = new Vector2(x + 10, -4.5f);
			result.Rect.rectTransform.sizeDelta = new Vector2(width, -7);
			result.Rect.rectTransform.SetAsFirstSibling();
		});
	}
	
	public void FocusNext()
	{
		if( searchResults_.Count > 0 )
		{
			// フォーカス先のIndexを計算
			int oldFucusIndex = focusResultIndex_;
			bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
			if( shift )
			{
				--focusResultIndex_;
				if( focusResultIndex_ < 0 )
				{
					focusResultIndex_ = searchResults_.Count - 1;
				}
			}
			else
			{
				++focusResultIndex_;
			}
			if( focusResultIndex_ < 0 || searchResults_.Count <= focusResultIndex_ )
			{
				focusResultIndex_ = 0;
			}

			// 前にフォーカスしてたやつの色を戻す
			if( oldFucusIndex != focusResultIndex_ && 0 <= oldFucusIndex && oldFucusIndex < searchResults_.Count )
			{
				if( searchResults_[oldFucusIndex].Rect != null )
				{
					searchResults_[oldFucusIndex].Rect.SetColor(GameContext.Config.SearchUnfocusColor);
				}
			}

			// 新たにフォーカスするやつが折りたたまれていたら広げて表示する
			SearchResult focusResult = searchResults_[focusResultIndex_];
			if( focusResult.Rect == null )
			{
				focusResult.Line.IsVisible = true;
				foreach( SearchResult result in searchResults_ )
				{
					if( result.Rect == null && result.Line.IsVisible )
					{
						InstantiateSearchRect(result);
					}
				}
			}

			// 新たにフォーカスしたやつの色設定など
			focusResult.Rect.SetColor(GameContext.Config.SearchFocusColor);
			//AnimManager.AddAnim(focusResult.Rect.gameObject, focusResult.Rect.rectTransform.sizeDelta.x, ParamType.SizeDeltaX, AnimType.Time, 0.1f, initValue: 0.0f);
			GameContext.Window.Note.ScrollTo(focusResult.Line, immediate: true);
		}
	}


	#region events

	protected override void OnFocused()
	{
		base.OnFocused();

		placeholder.enabled = false;
		targetGraphic.enabled = false;
		
		AnimManager.AddAnim(shade_, 1.0f, ParamType.AlphaColor, AnimType.Linear, 0.2f);
	}

	public override void OnDeselect(BaseEventData eventData)
	{
		base.OnDeselect(eventData);

		targetGraphic.enabled = true;

		if( text == "" )
		{
			placeholder.enabled = true;
			AnimManager.AddAnim(shade_, 0.0f, ParamType.AlphaColor, AnimType.Time, 0.1f);

			ClearSearchResult();
		}
	}
	
	protected void OnTextChanged(string newText)
	{
		textChanged_ = true;
	}

	#endregion


	#region overrides
	
	protected override bool EnableSelectAll { get { return true; } }
	protected override bool OnProcessKeyEvent(Event processingEvent, bool ctrl, bool shift, bool alt)
	{
		bool consumedEvent = false;
		switch( processingEvent.keyCode )
		{
			case KeyCode.F3:
			case KeyCode.Return:
			case KeyCode.KeypadEnter:
			{
				FocusNext();
				consumedEvent = true;
			}
			break;
		}

		return consumedEvent;
	}

	#endregion

}
