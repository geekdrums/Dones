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
		public SearchField OwnerField;

		public void OnTextChanged(string text)
		{
			if( text.Length <= Index || text.IndexOf(OwnerField.text, Index) != Index )
			{
				OwnerField.RemoveSearchResult(this);
			}
		}

		public void OnBindStateChanged(Line.EBindState bindState)
		{
			switch( bindState )
			{
				case Line.EBindState.Bind:
				{
					if( Rect == null )
					{
						OwnerField.InstantiateSearchRect(this);
					}
				}
				break;

				case Line.EBindState.WeakBind:
				{
					OwnerField.RemoveSearchResult(this);
				}
				break;

				case Line.EBindState.Unbind:
				{
					OwnerField.RemoveSearchResult(this);
				}
				break;
			}
		}
	}

	#region params

	IDisposable textSubscription_;
	bool textChanged_ = false;

	List<SearchResult> searchResults_ = new List<SearchResult>();
	int focusResultIndex_ = -1;

	HeapManager<UIMidairRect> rectHeap_ = new HeapManager<UIMidairRect>();
	UIMidairRect shade_;
	Text countText_;

	#endregion

	protected override void Awake()
	{
		base.Awake();

		rectHeap_.Initialize(10, GetComponentsInChildren<UIMidairRect>()[0], this.transform);
		shade_ = GetComponentsInChildren<UIMidairRect>()[1];
		countText_ = GetComponentsInChildren<Text>(includeInactive: true)[2];
		textSubscription_ = onValueChanged.AsObservable().Subscribe(text =>
		{
			OnSearchTextChanged(text);
		});

		GameContext.Window.Note.ActionManager.ChainStarted += this.actionManager__ChainStarted;
		GameContext.Window.Note.ActionManager.ChainEnded += this.actionManager__ChainEnded;
		GameContext.Window.Note.ActionManager.Executed += this.actionManager__Executed;
	}


	#region unity functions

	void Update ()
	{
		if( isFocused )
		{
			if( textChanged_ )
			{
				Search(GameContext.Window.Note.Tree);
				textChanged_ = false;
			}
		}
	}

	#endregion


	#region Search

	bool CanSearch()
	{
		return text != null && text != "" && text != " " && text != "	";
	}

	void OnSearchTextChanged(string newText)
	{
		textChanged_ = true;
	}

	void Search(Tree tree)
	{
		ClearSearchResult();

		if( CanSearch() == false )
		{
			UpdateSearchCountText();
			return;
		}

		searchResults_.AddRange(tree.Search(text));

		Line lastFocusedLine = tree.LastFocusedLine;
		for( int i = 0; i < searchResults_.Count; ++i )
		{
			SearchResult result = searchResults_[i];
			OnAddedSearchResult(result);

			if( focusResultIndex_ < 0 )
			{
				if( lastFocusedLine == null || lastFocusedLine.CompareTo(result.Line) >= 0 )
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

		if( searchResults_.Count > 0 )
		{
			countText_.gameObject.SetActive(true);
		}
		else
		{
			countText_.gameObject.SetActive(false);
		}
		UpdateSearchCountText();
	}

	bool Search(Line line)
	{
		bool find = false;
		foreach( SearchField.SearchResult result in line.Search(text) )
		{
			if( searchResults_.Find((SearchResult r) => r.Line == line && r.Index == result.Index) == null )
			{
				searchResults_.Add(result);
				OnAddedSearchResult(result);
				UpdateSearchCountText();
				find = true;
			}
		}
		return find;
	}

	void OnAddedSearchResult(SearchResult result)
	{
		result.OwnerField = this;
		if( result.Line.Field != null )
		{
			InstantiateSearchRect(result);
		}
		result.Line.OnBindStateChanged += result.OnBindStateChanged;
		result.Line.OnTextChanged += result.OnTextChanged;
	}

	void Sort()
	{
		SearchResult prevFocusedResult = null;
		if( focusResultIndex_ >= 0 )
		{
			prevFocusedResult = searchResults_[focusResultIndex_];
		}

		searchResults_.Sort((SearchResult res, SearchResult other) => res.Line.CompareTo(other.Line));

		if( prevFocusedResult != null )
		{
			focusResultIndex_ = searchResults_.IndexOf(prevFocusedResult);
			UpdateSearchCountText();
		}
	}

	#endregion


	#region Focus

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

			UpdateSearchCountText();

			// 前にフォーカスしてたやつの色を戻す
			if( oldFucusIndex != focusResultIndex_ && 0 <= oldFucusIndex && oldFucusIndex < searchResults_.Count )
			{
				if( searchResults_[oldFucusIndex].Rect != null )
				{
					searchResults_[oldFucusIndex].Rect.SetColor(GameContext.Config.SearchUnfocusColor);
					searchResults_[oldFucusIndex].Rect.rectTransform.SetAsFirstSibling();
				}
			}

			// 新たにフォーカスするやつが折りたたまれていたら広げて表示する
			SearchResult focusResult = searchResults_[focusResultIndex_];
			if( focusResult.Line.IsVisible == false )
			{
				focusResult.Line.IsVisible = true;
			}

			// 新たにフォーカスしたやつの色設定など
			focusResult.Rect.SetColor(GameContext.Config.SearchFocusColor);
			//AnimManager.AddAnim(focusResult.Rect.gameObject, focusResult.Rect.rectTransform.sizeDelta.x, ParamType.SizeDeltaX, AnimType.Time, 0.1f, initValue: 0.0f);
			GameContext.Window.Note.ScrollTo(focusResult.Line, immediate: true);
		}
	}

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

		if( CanSearch() == false )
		{
			text = "";
			placeholder.enabled = true;
			AnimManager.AddAnim(shade_, 0.0f, ParamType.AlphaColor, AnimType.Time, 0.1f);
			countText_.gameObject.SetActive(false);

			ClearSearchResult();
		}
	}

	#endregion


	#region edit events

	void actionManager__ChainStarted(object sender, ChainActionEventArgs e)
	{
	}

	void actionManager__ChainEnded(object sender, ChainActionEventArgs e)
	{
		OnEdited(e.Action);
	}

	void actionManager__Executed(object sender, ActionEventArgs e)
	{
		if( (sender as ActionManager).IsChaining == false )
		{
			OnEdited(e.Action);
		}
	}

	void OnEdited(ActionBase action)
	{
		if( CanSearch() == false )
		{
			return;
		}

		if( action is Line.TextAction )
		{
			(action as Line.TextAction).OnFixEvent += OnTextEditLineFixed;
		}

		bool find = false;
		foreach( Line line in action.GetTargetLines() )
		{
			if( line.IsAlive() )
			{
				find |= Search(line);
			}
		}
		if( find )
		{
			Sort();
		}
	}

	void OnTextEditLineFixed(Line.TextAction action, Line line)
	{
		if( CanSearch() == false )
		{
			return;
		}

		if( Search(line) )
		{
			Sort();
		}
		(action as Line.TextAction).OnFixEvent -= OnTextEditLineFixed;
	}

	public void OnTreePathChanged(Tree tree)
	{
		Search(tree);
	}

	#endregion


	#region utils

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

	void ClearSearchResult()
	{
		foreach( SearchResult result in searchResults_ )
		{
			if( result.Rect != null )
			{
				rectHeap_.BackToHeap(result.Rect);
			}
			result.Line.OnBindStateChanged -= result.OnBindStateChanged;
			result.Line.OnTextChanged -= result.OnTextChanged;
		}
		searchResults_.Clear();
		focusResultIndex_ = -1;
	}

	void RemoveSearchResult(SearchResult result)
	{
		if( result.Rect != null )
		{
			rectHeap_.BackToHeap(result.Rect);
		}
		result.Line.OnBindStateChanged -= result.OnBindStateChanged;
		result.Line.OnTextChanged -= result.OnTextChanged;

		int index = searchResults_.IndexOf(result);
		if( index < focusResultIndex_ )
		{
			--focusResultIndex_;
		}
		else if( index == focusResultIndex_ )
		{
			--focusResultIndex_;
			if( 0 <= focusResultIndex_ && focusResultIndex_ < searchResults_.Count && searchResults_[focusResultIndex_].Rect != null )
			{
				searchResults_[focusResultIndex_].Rect.SetColor(GameContext.Config.SearchFocusColor);
			}
		}
		searchResults_.Remove(result);
		UpdateSearchCountText();
	}

	void UpdateSearchCountText()
	{
		if( searchResults_.Count > 0 )
		{
			countText_.text = String.Format("{0}/{1}", focusResultIndex_ + 1, searchResults_.Count);
		}
		else
		{
			countText_.text = "";
		}
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
