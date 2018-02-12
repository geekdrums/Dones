using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TagIncrementalDialog : MonoBehaviour {

	public GameObject LayoutParent;
	public TagText TagTextPrefab;
	public float TagTextHeight = 27;
	public float BaseXOffset;
	public Image SelectionImage;

	public bool IsActive { get { return gameObject.activeSelf; } }

	HeapManager<TagText> tagTextHeapManager_;
	RectTransform rect_;

	List<TagText> searchResults_ = new List<TagText>();

	public int SelectedIndex { get { return selectedIndex_; } }
	int selectedIndex_ = 0;

	
	void Initialize () {
		tagTextHeapManager_ = new HeapManager<TagText>();
		tagTextHeapManager_.Initialize(3, TagTextPrefab);
		rect_ = GetComponent<RectTransform>();
	}
	
	// Update is called once per frame
	void Update () {
		if( Input.GetMouseButtonDown(0) )
		{
			Close();
		}
	}

	public void Show(Vector2 position, string text = null)
	{
		if( rect_ == null )
		{
			Initialize();
		}
		rect_.anchoredPosition = position;
		this.gameObject.SetActive(true);
		selectedIndex_ = 0;
		UpdateLayout();

		searchResults_.Clear();
		foreach( TagParent tagParent in GameContext.TagList )
		{
			if( string.IsNullOrEmpty(text) || (tagParent.Tag.StartsWith(text) && tagParent.Tag != text) )
			{
				TagText tagText = tagTextHeapManager_.Instantiate(LayoutParent.transform);
				tagText.Text = "#" + tagParent.Tag;
				searchResults_.Add(tagText);
			}
		}
		SortSeachResult();

		if( searchResults_.Count > 0 )
		{
			OnTextLengthChanged();
		}
		else
		{
			Close();
		}
	}

	public void Close()
	{
		this.gameObject.SetActive(false);
		foreach( TagText tagText in searchResults_ )
		{
			tagTextHeapManager_.BackToHeap(tagText);
		}
		searchResults_.Clear();
		selectedIndex_ = 0;
	}

	bool shouldUpdateTextLength_ = false;

	void OnTextLengthChanged()
	{
		if( shouldUpdateTextLength_ == false )
		{
			shouldUpdateTextLength_ = true;
			if( this.gameObject.activeInHierarchy )
			{
				StartCoroutine(UpdateTextLengthCoroutine());
			}
		}
	}

	IEnumerator UpdateTextLengthCoroutine()
	{
		yield return new WaitWhile(() => searchResults_.Find((TagText tagText) => tagText.TextComponent.cachedTextGenerator.characterCount == 0) != null);

		OnUpdatedTextRectLength();

		shouldUpdateTextLength_ = false;
	}

	void OnUpdatedTextRectLength()
	{
		float maxWidth = 0;
		float height = searchResults_.Count * TagTextHeight;
		foreach( TagText tagText in searchResults_ )
		{
			float width = CustomInputField.CalcTextRectLength(tagText.TextComponent.cachedTextGenerator, tagText.Text.Length - 1);
			if( maxWidth < width )
			{
				maxWidth = width;
			}
		}

		foreach( UIMidairRect rect in GetComponentsInChildren<UIMidairRect>() )
		{
			rect.Width = maxWidth + 20;
			rect.Height = height + 2;
			rect.RecalculatePolygon();
		}

		SelectionImage.rectTransform.sizeDelta = new Vector2(maxWidth + 20, TagTextHeight);

		foreach( TagText tagText in searchResults_ )
		{
			tagText.BG.rectTransform.sizeDelta = new Vector2(maxWidth, TagTextHeight);
		}
	}

	public void IncrementalSearch(string text)
	{
		foreach( TagText tagText in searchResults_ )
		{
			if( tagText.Text.StartsWith("#" + text) == false || tagText.Text == "#" + text )
				tagTextHeapManager_.BackToHeap(tagText);
		}
		foreach( TagParent tagParent in GameContext.TagList )
		{
			if( string.IsNullOrEmpty(text) || (tagParent.Tag.StartsWith(text) && tagParent.Tag != text) )
			{
				if( searchResults_.Find((TagText tagText) => tagText.Text == "#" + tagParent.Tag) == null )
				{
					TagText tagText = tagTextHeapManager_.Instantiate(LayoutParent.transform);
					tagText.Text = "#" + tagParent.Tag;
					searchResults_.Add(tagText);
				}
			}
		}

		searchResults_.RemoveAll((TagText tagText) => tagText.gameObject.activeSelf == false);
		SortSeachResult();

		if( searchResults_.Count == 0 )
		{
			Close();
		}
		else
		{
			selectedIndex_ = 0;
			UpdateLayout();
			OnTextLengthChanged();
		}
	}

	public void OnArrowInput(KeyCode key)
	{
		if( key == KeyCode.UpArrow )
		{
			selectedIndex_ -= 1;
		}
		else if( key == KeyCode.DownArrow )
		{
			selectedIndex_ += 1;
		}
		selectedIndex_ = Math.Min(searchResults_.Count - 1, Math.Max(0, selectedIndex_));
		UpdateLayout();
	}

	public string GetSelectedTag()
	{
		if( selectedIndex_ >= 0 )
		{
			return searchResults_[selectedIndex_].Text.TrimStart('#');
		}
		else return null;
	}

	void UpdateLayout()
	{
		SelectionImage.gameObject.SetActive(selectedIndex_ >= 0);
		SelectionImage.rectTransform.anchoredPosition = new Vector2(BaseXOffset, -selectedIndex_ * TagTextHeight);
		//LayoutParent.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(BaseXOffset, (selectedIndex_ + 1) * TagTextHeight);
	}

	void SortSeachResult()
	{
		int index = 0;
		foreach( TagParent tagParent in GameContext.TagList )
		{
			TagText tagText = searchResults_.Find((TagText tt) => tt.Text == "#" + tagParent.Tag);
			if( tagText != null )
			{
				tagText.transform.SetSiblingIndex(index);
				++index;
			}
		}
	}
}
