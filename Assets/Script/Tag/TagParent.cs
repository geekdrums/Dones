﻿using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UniRx;
using UniRx.Triggers;

// Window > TagList > [ TagParent ] > TaggedLine
public class TagParent : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler, IEnumerable<TaggedLine>
{
	#region editor params

	public TaggedLine TaggedLinePrefab;
	public GameObject LineParent;
	public GameObject PinMark;
	public GameObject RepeatMark;

	#endregion


	#region params

	public float Height { get { return isFolded_ ? 30 : 30 + GameContext.Config.TagLineHeight * lines_.Count; } }

	public string Tag { get { return tag_; } }
	string tag_;

	public bool IsFolded
	{
		get
		{
			return isFolded_;
		}
		set
		{
			if( isFolded_ != value )
			{
				isFolded_ = value;

				if( isFolded_ )
				{
					LineParent.SetActive(false);
				}
				else
				{
					LineParent.SetActive(true);
				}
				if( tagToggle_ != null )
				{
					tagToggle_.SetFold(IsFolded);
				}
				tagList_.AnimParentsToTargetPosition();
			}
		}
	}
	bool isFolded_ = false;


	public bool IsPinned
	{
		get
		{
			return isPinned_;
		}
		set
		{
			isPinned_ = value;
			PinMark.SetActive(isPinned_);
		}
	}
	bool isPinned_ = false;


	public bool IsRepeat
	{
		get
		{
			return isRepeat_;
		}
		set
		{
			isRepeat_ = value;
			RepeatMark.SetActive(isRepeat_);
		}
	}
	bool isRepeat_ = false;


	public TagList OwnerList { get { return tagList_; } }
	TagList tagList_;

	List<TaggedLine> sourceLines_ = new List<TaggedLine>();
	List<TaggedLine> lines_ = new List<TaggedLine>();
	TaggedLine selectedLine_;
	TagToggle tagToggle_;
	int selectedIndex_ = -1;
	List<string> lineOrder_ = new List<string>();

	HeapManager<TaggedLine> heapManager_ = new HeapManager<TaggedLine>();

	#endregion


	public void Initialize(string tag, TagList owner)
	{
		tag_ = tag;
		tagList_ = owner;
		lines_.Clear();
		sourceLines_.Clear();
		selectedLine_ = null;
		selectedIndex_ = -1;
		GetComponentInChildren<Text>().text = "#" + tag_;

		isPinned_ = false;
		isRepeat_ = false;
		isFolded_ = false;

#if UNITY_EDITOR
		name = "#" + tag_;
#endif
	}


	#region unity events

	void Awake ()
	{
		heapManager_.Initialize(10, TaggedLinePrefab, LineParent.transform);
		tagToggle_ = GetComponentInChildren<TagToggle>();
	}
	
	void OnEnable()
	{
		SubscribeKeyInput();
	}

	void SubscribeKeyInput()
	{
		KeyCode[] throttleKeys = new KeyCode[]
		{
			//KeyCode.UpArrow,
			//KeyCode.DownArrow,
			KeyCode.Space,
			KeyCode.Backspace,
			KeyCode.Delete
		};

		var updateStream = this.UpdateAsObservable();

		foreach( KeyCode key in throttleKeys )
		{
			// 最初の入力
			updateStream.Where(x => Input.GetKeyDown(key))
				.Merge(
			// 押しっぱなしにした時の自動連打
			updateStream.Where(x => Input.GetKey(key))
				.Delay(TimeSpan.FromSeconds(GameContext.Config.ArrowStreamDelayTime))
				.ThrottleFirst(TimeSpan.FromSeconds(GameContext.Config.ArrowStreamIntervalTime))
				)
				.TakeUntil(this.UpdateAsObservable().Where(x => Input.GetKeyUp(key)))
				.RepeatUntilDisable(this)
				.Subscribe(_ => OnThrottleInput(key));
		}
	}

	void OnThrottleInput(KeyCode key)
	{
		if( selectedLine_ == null ) return;

		bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
		bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
		bool ctrlOnly = ctrl && !alt && !shift;
		
		switch( key )
		{
		case KeyCode.Space:
			selectedLine_.Done();
			UpdateSelection();
			break;
		case KeyCode.Backspace:
		case KeyCode.Delete:
			Line line = selectedLine_.BindedLine;
			selectedLine_.BindedLine.Tree.ActionManager.Execute(new LineAction(
				targetLines: line,
				execute: () =>
				{
					line.RemoveTag(Tag);
				},
				undo: () =>
				{
					line.AddTag(Tag);
				}));
			break;
		case KeyCode.DownArrow:
			{
				if( selectedIndex_ < lines_.Count - 1 )
				{
					++selectedIndex_;
					if( alt )
					{
						lines_.Remove(selectedLine_);
						lines_.Insert(selectedIndex_, selectedLine_);
						AnimManager.AddAnim(selectedLine_, GetTargetPosition(selectedLine_), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
						AnimManager.AddAnim(lines_[selectedIndex_ - 1], GetTargetPosition(lines_[selectedIndex_ - 1]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
					}
					else
					{
						lines_[selectedIndex_].Select();
					}
				}
			}
			break;
		case KeyCode.UpArrow:
			{
				if( selectedIndex_ > 0 )
				{
					--selectedIndex_;
					if( alt )
					{
						lines_.Remove(selectedLine_);
						lines_.Insert(selectedIndex_, selectedLine_);
						AnimManager.AddAnim(selectedLine_, GetTargetPosition(selectedLine_), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
						AnimManager.AddAnim(lines_[selectedIndex_ + 1], GetTargetPosition(lines_[selectedIndex_ + 1]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
					}
					else
					{
						lines_[selectedIndex_].Select();
					}
				}
			}
			break;
		}
	}

	// Update is called once per frame
	void Update () {

	}

	#endregion


	#region add / remove lines

	public TaggedLine InstantiateTaggedLine(Line line, int index = -1, bool withAnim = true)
	{
		TaggedLine taggedLine = heapManager_.Instantiate(LineParent.transform);

		AnimManager.RemoveOtherAnim(taggedLine);
		
		if( index < 0 )
		{
			index = lines_.Count;
			lines_.Add(taggedLine);
			sourceLines_.Add(taggedLine);
		}
		else
		{
			lines_.Insert(index, taggedLine);
		}
		AnimLinesToTargetPosition(index, lines_.Count - 1);

		taggedLine.Bind(line);

		if( withAnim && IsFolded == false )
		{
			AnimOnInstantiate(taggedLine);
		}
		
		tagList_.AnimParentsToTargetPosition();

		return taggedLine;
	}

	public TaggedLine FindBindedLine(Line line)
	{
		foreach( TaggedLine taggedLine in sourceLines_ )
		{
			if( taggedLine.BindedLine == line )
			{
				return taggedLine;
			}
		}
		return null;
	}

	public void RemoveTaggedLine(TaggedLine taggedLine)
	{
		bool needSelectionUpdate = false;
		if( taggedLine == selectedLine_ )
		{
			needSelectionUpdate = true;
		}

		sourceLines_.Remove(taggedLine);

		int index = lines_.IndexOf(taggedLine);
		if( index >= 0 )
		{
			lines_.Remove(taggedLine);
		}
		else
		{
			index = 0;
		}
		float delayTime = 0.0f;
		if( taggedLine.IsDoneAnimating )
		{
			delayTime = 0.3f;
		}
		AnimLinesToTargetPosition(index, lines_.Count - 1, delayTime);

		if( isPinned_ || sourceLines_.Count > 0 )
		{
			float animTime = 0.2f;
			AnimManager.AddAnim(taggedLine, 0.0f, ParamType.AlphaColor, AnimType.Time, animTime, delay: delayTime, endOption: AnimEndOption.Deactivate);
			
			if( needSelectionUpdate )
			{
				UpdateSelection();
			}
		}
		else
		{
			OnLineDisabled(taggedLine);
			tagList_.OnTagEmpty(this);
		}
		tagList_.AnimParentsToTargetPosition(delayTime);
	}

	public void RemoveLine(Line line)
	{
		TaggedLine taggedLine = FindBindedLine(line);
		if( taggedLine != null )
		{
			RemoveTaggedLine(taggedLine);
		}
	}

	public void ClearLines()
	{
		foreach( TaggedLine line in sourceLines_ )
		{
			heapManager_.BackToHeap(line);
		}
		sourceLines_.Clear();
		lines_.Clear();
		selectedLine_ = null;
		selectedIndex_ = -1;
	}

	public int IndexOf(TaggedLine taggedLine)
	{
		if( lines_.Contains(taggedLine) )
		{
			return lines_.IndexOf(taggedLine);
		}
		else return -1;
	}

	public int Count { get { return lines_.Count; } }

	#endregion


	#region events

	public void OnSelect(TaggedLine taggedLine)
	{
		selectedLine_ = taggedLine;
		foreach( TaggedLine line in this )
		{
			if( line != selectedLine_ )
			{
				line.OnDeselect(null);
			}
		}
		if( lines_.Contains(taggedLine) )
		{
			selectedIndex_ = lines_.IndexOf(selectedLine_);
		}
		else
		{
			selectedIndex_ = -1;
			Debug.LogError("couldn't find " + taggedLine.ToString());
		}
	}

	public void OnDeselect(TaggedLine taggedLine)
	{
		if( selectedLine_ == taggedLine )
		{
			selectedLine_ = null;
			selectedIndex_ = -1;
		}
	}

	public void OnDoneChanged(TaggedLine taggedLine)
	{
		/*
		// move to target position
		if( taggedLine.IsDone )
		{
			if( lines_.Contains(taggedLine) )
			{
				int index = lines_.IndexOf(taggedLine);
				lines_.Remove(taggedLine);
				doneLines_.Insert(0, taggedLine);

				AnimLinesToTargetPosition(index, lines_.Count - 1);
				AnimManager.AddAnim(taggedLine, GetTargetPosition(taggedLine), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
				taggedLine.transform.SetAsLastSibling();
			}
		}
		else
		{
			if( doneLines_.Contains(taggedLine) )
			{
				int index = doneLines_.IndexOf(taggedLine);
				doneLines_.Remove(taggedLine);
				lines_.Add(taggedLine);

				AnimDoneLinesToTargetPosition(0, index - 1);
				AnimManager.AddAnim(taggedLine, GetTargetPosition(taggedLine), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
				taggedLine.transform.SetAsLastSibling();
			}
		}
		*/
	}

	public void OnBeginDragLine(TaggedLine taggedLine)
	{
		if( lines_.Contains(taggedLine) )
		{
			taggedLine.transform.parent = tagList_.transform;
			taggedLine.transform.SetAsLastSibling();
		}
	}

	public void OnDraggingLine(TaggedLine taggedLine, PointerEventData eventData)
	{
		if( lines_.Contains(taggedLine) )
		{
			int index = lines_.IndexOf(taggedLine);
			taggedLine.transform.localPosition += new Vector3(0, eventData.delta.y, 0);

			bool overed = tagList_.IsOutsideOf(this, taggedLine.transform.localPosition.y);
			tagList_.OnOverDraggingLine(taggedLine, eventData, overed);
			if( overed == false )
			{
				float currentY = -(taggedLine.transform.localPosition.y - this.transform.localPosition.y);
				int desiredIndex = Mathf.Clamp((int)(currentY / GameContext.Config.TagLineHeight), 0, lines_.Count - 1);
				if( index != desiredIndex )
				{
					TaggedLine oldDesiredIndexLine = lines_[desiredIndex];
					sourceLines_.Remove(taggedLine);
					sourceLines_.Insert(sourceLines_.IndexOf(oldDesiredIndexLine), taggedLine);
					lines_.Remove(taggedLine);
					lines_.Insert(desiredIndex, taggedLine);
					int sign = (int)Mathf.Sign(desiredIndex - index);
					for( int i = index; i != desiredIndex; i += sign )
					{
						AnimManager.AddAnim(lines_[i], GetTargetPosition(lines_[i]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
					}
				}
			}
		}
	}

	public void OnEndDragLine(TaggedLine taggedLine)
	{
		if( lines_.Contains(taggedLine) )
		{
			bool overed = tagList_.IsOutsideOf(this, taggedLine.transform.localPosition.y);
			if( overed )
			{
				selectedLine_ = null;
				selectedIndex_ = -1;
			}
			else
			{
				taggedLine.transform.parent = LineParent.transform;
				selectedIndex_ = lines_.IndexOf(taggedLine);
				AnimManager.AddAnim(taggedLine, GetTargetPosition(taggedLine), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
			}
			tagList_.OnEndOverDragLine(taggedLine, overed);
		}
	}

	public void OnLineDisabled(TaggedLine line)
	{
		if( sourceLines_.Contains(line) == false )
		{
			heapManager_.BackToHeap(line);
		}
	}

	public void OnMenuButtonDown()
	{
		GameContext.Window.TagMenu.Show(this);
	}

	public void OnTreePathChanged(Line titleLine)
	{
		lines_.Clear();
		foreach( TaggedLine taggedLine in sourceLines_ )
		{
			bool isActive = titleLine == null || taggedLine.BindedLine.IsChildOf(titleLine);
			taggedLine.gameObject.SetActive(isActive);
			if( isActive )
			{
				lines_.Add(taggedLine);
			}
		}
		AnimLinesToTargetPosition(0, lines_.Count - 1);
	}

	#endregion


	#region drag

	public void OnBeginDrag(PointerEventData eventData)
	{
		tagList_.OnBeginDragTag(this);
	}

	public void OnDrag(PointerEventData eventData)
	{
		tagList_.OnDraggingTag(this, eventData);
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		tagList_.OnEndDragTag(this);
	}

	#endregion


	#region utils

	// IEnumerable<TaggedLine>
	public IEnumerator<TaggedLine> GetEnumerator()
	{
		foreach( TaggedLine line in lines_ )
		{
			yield return line;
		}
	}
	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.GetEnumerator();
	}

	Vector3 GetTargetPosition(TaggedLine taggedLine)
	{
		return Vector3.down * GameContext.Config.TagLineHeight * lines_.IndexOf(taggedLine);
	}

	void UpdateSelection()
	{
		if( selectedIndex_ < lines_.Count )
		{
			lines_[selectedIndex_].Select();
		}
		else if( lines_.Count > 0 )
		{
			lines_[lines_.Count - 1].Select();
			selectedIndex_ = lines_.Count - 1;
		}
		else
		{
			selectedLine_ = null;
			selectedIndex_ = -1;
		}
	}

	void AnimOnInstantiate(TaggedLine taggedLine)
	{
		taggedLine.transform.localPosition = Vector3.zero;
		AnimManager.AddAnim(taggedLine, GetTargetPosition(taggedLine), ParamType.Position, AnimType.BounceIn, 0.25f);
	}

	void AnimLinesToTargetPosition(int startIndex, int endIndex, float delayTime = 0.0f)
	{
		if( startIndex > endIndex )
		{
			int start = endIndex;
			endIndex = startIndex;
			startIndex = start;
		}
		if( startIndex < 0 || lines_.Count <= endIndex )
		{
			return;
		}
		for( int i = startIndex; i <= endIndex; ++i )
		{
			AnimManager.RemoveOtherAnim(lines_[i], ParamType.Position);
			AnimManager.AddAnim(lines_[i], GetTargetPosition(lines_[i]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime, delayTime);
		}
	}

	public void AddLineOrder(string line)
	{
		lineOrder_.Add(line);
	}

	public void ApplyLineOrder()
	{
		List<TaggedLine> sortedlines = new List<TaggedLine>();
		foreach( string text in lineOrder_ )
		{
			TaggedLine line = lines_.Find((l) => l.Text == text);
			if( line != null )
			{
				sortedlines.Add(line);
			}
		}
		foreach( TaggedLine taggedLine in lines_ )
		{
			if( sortedlines.Contains(taggedLine) == false )
				sortedlines.Add(taggedLine);
		}

		lines_ = sortedlines;
		AnimLinesToTargetPosition(0, lines_.Count - 1);
	}

	#endregion
}
