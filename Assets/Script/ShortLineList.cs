using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UniRx;
using UniRx.Triggers;

public class ShortLineList : MonoBehaviour, IEnumerable<ShortLine>
{
	#region editor params

	public ShortLine ShortLinePrefab;
	public float LineWidth = 200;
	public float LineHeight = 30;
	public float ClosedLineWidth = 10;

	public Button OpenButton;
	public Button CloseButton;

	#endregion


	#region params

	public bool IsOpened { get { return isOpened_; } }
	public float Width { get { return isOpened_ ? LineWidth : ClosedLineWidth; } }

	List<ShortLine> lines_ = new List<ShortLine>();
	List<ShortLine> doneLines_ = new List<ShortLine>();
	ShortLine selectedLine_;
	int selectedIndex_ = -1;
	bool isOpened_ = true;

	LayoutElement layout_;
	ContentSizeFitter contentSizeFitter_;
	RectTransform scrollRect_;

	#endregion


	#region unity functions

	void Awake ()
	{
		layout_ = GetComponentInParent<LayoutElement>();
		contentSizeFitter_ = GetComponentInParent<ContentSizeFitter>();
		scrollRect_ = GetComponentInParent<ScrollRect>().GetComponent<RectTransform>();
	}

	// Use this for initialization
	void Start()
	{
		OpenButton.gameObject.SetActive(isOpened_ == false);
		SubscribeKeyInput();
	}

	void SubscribeKeyInput()
	{
		KeyCode[] throttleKeys = new KeyCode[]
		{
			KeyCode.UpArrow,
			KeyCode.DownArrow,
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

		bool ctrl = Input.GetKey(KeyCode.LeftControl);
		bool shift = Input.GetKey(KeyCode.LeftShift);
		bool alt = Input.GetKey(KeyCode.LeftAlt);
		bool ctrlOnly = ctrl && !alt && !shift;
		
		List<ShortLine> list = selectedLine_.IsDone ? doneLines_ : lines_;

		switch( key )
		{
		case KeyCode.Space:
			selectedLine_.Done();
			UpdateSelection(list, selectedIndex_);
			break;
		case KeyCode.Backspace:
		case KeyCode.Delete:
			selectedLine_.Remove();
			break;
		case KeyCode.DownArrow:
			{
				if( selectedIndex_ < list.Count - 1 )
				{
					++selectedIndex_;
					if( alt )
					{
						list.Remove(selectedLine_);
						list.Insert(selectedIndex_, selectedLine_);
						AnimManager.AddAnim(selectedLine_, GetTargetPosition(selectedLine_), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
						AnimManager.AddAnim(list[selectedIndex_ - 1], GetTargetPosition(list[selectedIndex_ - 1]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
					}
					else
					{
						list[selectedIndex_].Select();
					}
				}
				else if( selectedLine_.IsDone == false && doneLines_.Count > 0 && alt == false )
				{
					selectedIndex_ = 0;
					doneLines_[0].Select();
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
						list.Remove(selectedLine_);
						list.Insert(selectedIndex_, selectedLine_);
						AnimManager.AddAnim(selectedLine_, GetTargetPosition(selectedLine_), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
						AnimManager.AddAnim(list[selectedIndex_ + 1], GetTargetPosition(list[selectedIndex_ + 1]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
					}
					else
					{
						list[selectedIndex_].Select();
					}
				}
				else if( selectedLine_.IsDone && lines_.Count > 0 && alt == false )
				{
					selectedIndex_ = lines_.Count - 1;
					lines_[lines_.Count - 1].Select();
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

	public ShortLine InstantiateShortLine(Line line, int index = -1, bool withAnim = true)
	{
		ShortLine shortLine = Instantiate(ShortLinePrefab.gameObject, this.transform).GetComponent<ShortLine>();

		if( line.IsDone == false )
		{
			if( index < 0 )
			{
				index = lines_.Count;
				lines_.Add(shortLine);
			}
			else
			{
				lines_.Insert(index, shortLine);
			}
			AnimLinesToTargetPosition(index, lines_.Count - 1);
			AnimDoneLinesToTargetPosition(0, doneLines_.Count - 1);
		}
		else
		{
			if( index < 0 )
			{
				index = doneLines_.Count;
				doneLines_.Add(shortLine);
			}
			else
			{
				doneLines_.Insert(index, shortLine);
			}
			AnimDoneLinesToTargetPosition(index, doneLines_.Count - 1);
		}

		shortLine.Bind(line);

		if( withAnim )
		{
			AnimOnInstantiate(shortLine);
		}
		
		UpdateLayoutElement();

		return shortLine;
	}

	public ShortLine FindBindedLine(Line line)
	{
		foreach( ShortLine shortLine in lines_ )
		{
			if( shortLine.BindedLine == line )
			{
				return shortLine;
			}
		}
		foreach( ShortLine shortLine in doneLines_ )
		{
			if( shortLine.BindedLine == line )
			{
				return shortLine;
			}
		}
		return null;
	}

	public void RemoveShortLine(ShortLine shortLine)
	{
		bool needSelectionUpdate = false;
		if( shortLine == selectedLine_ )
		{
			needSelectionUpdate = true;
		}

		float animTime = 0.2f;
		AnimManager.AddAnim(shortLine, GameContext.Config.ShortLineAccentColor, ParamType.Color, AnimType.Time, 0.15f);
		AnimManager.AddAnim(shortLine, GameContext.Config.ShortLineBackColor, ParamType.Color, AnimType.Time, 0.1f, 0.15f, AnimEndOption.Destroy);
		AnimManager.AddAnim(shortLine.GetComponentInChildren<Text>(), GameContext.Config.ShortLineBackColor, ParamType.TextColor, AnimType.Time, animTime);
		AnimManager.AddAnim(shortLine.GetComponentInChildren<UIMidairPrimitive>(), 0.0f, ParamType.PrimitiveArc, AnimType.Time, animTime);

		int index = lines_.IndexOf(shortLine);
		if( index >= 0 )
		{
			lines_.Remove(shortLine);
		}
		else
		{
			index = 0;
		}
		AnimLinesToTargetPosition(index, lines_.Count - 1);

		index = doneLines_.IndexOf(shortLine);
		if( index >= 0 )
		{
			doneLines_.Remove(shortLine);
		}
		else
		{
			index = 0;
		}
		AnimDoneLinesToTargetPosition(index, doneLines_.Count - 1);

		UpdateLayoutElement();

		if( needSelectionUpdate )
		{
			UpdateSelection(shortLine.BindedLine.IsDone ? doneLines_ : lines_, selectedIndex_);
		}
	}

	public void SetLineIndex(ShortLine shortLine, int index)
	{
		if( lines_.Contains(shortLine) && index < lines_.Count )
		{
			int oldIndex = lines_.IndexOf(shortLine);
			if( oldIndex != index )
			{
				lines_.Remove(shortLine);
				lines_.Insert(index, shortLine);
				AnimLinesToTargetPosition(index, oldIndex);
			}
		}
	}

	void UpdateLayoutElement()
	{
		layout_.preferredHeight = LineHeight * (lines_.Count + doneLines_.Count);
		contentSizeFitter_.SetLayoutVertical();
	}

	#endregion


	#region events

	public void OnSelect(ShortLine line)
	{
		selectedLine_ = line;
		if( lines_.Contains(line) )
		{
			selectedIndex_ = lines_.IndexOf(selectedLine_);
		}
		else if( doneLines_.Contains(line) )
		{
			selectedIndex_ = doneLines_.IndexOf(selectedLine_);
		}
		else
		{
			selectedIndex_ = -1;
			Debug.LogError("couldn't find " + line.ToString() + " in any list.");
		}
	}

	public void OnDeselect(ShortLine line)
	{
		if( selectedLine_ == line )
		{
			selectedLine_ = null;
			selectedIndex_ = -1;
		}
	}

	public void OnDoneChanged(ShortLine shortLine)
	{
		// move to target position
		if( shortLine.IsDone )
		{
			if( lines_.Contains(shortLine) )
			{
				int index = lines_.IndexOf(shortLine);
				lines_.Remove(shortLine);
				doneLines_.Insert(0, shortLine);

				AnimLinesToTargetPosition(index, lines_.Count - 1);
				AnimManager.AddAnim(shortLine, GetTargetPosition(shortLine), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
				AnimManager.AddAnim(shortLine, GameContext.Config.ShortLineAccentColor, ParamType.Color, AnimType.Time, 0.15f);
				AnimManager.AddAnim(shortLine, GameContext.Config.ShortLineBackColor, ParamType.Color, AnimType.Time, 0.1f, 0.15f);
				shortLine.transform.SetAsLastSibling();
			}
		}
		else
		{
			if( doneLines_.Contains(shortLine) )
			{
				int index = doneLines_.IndexOf(shortLine);
				doneLines_.Remove(shortLine);
				lines_.Add(shortLine);

				AnimDoneLinesToTargetPosition(0, index - 1);
				AnimManager.AddAnim(shortLine, GetTargetPosition(shortLine), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
				AnimManager.AddAnim(shortLine, GameContext.Config.ShortLineColor, ParamType.Color, AnimType.Time, 0.1f);
				shortLine.transform.SetAsLastSibling();
			}
		}
	}

	public void OnBeginDrag(ShortLine line)
	{
		if( lines_.Contains(line) )
		{
			AnimManager.AddAnim(line, 5.0f, ParamType.PositionX, AnimType.Time, GameContext.Config.AnimTime);
			line.transform.SetAsLastSibling();
		}
	}

	public void OnDragging(ShortLine line, PointerEventData eventData)
	{
		if( lines_.Contains(line) )
		{
			int index = lines_.IndexOf(line);
			line.transform.localPosition += new Vector3(0, eventData.delta.y, 0);

			int desiredIndex = Mathf.Clamp((int)(-line.transform.localPosition.y / LineHeight), 0, lines_.Count - 1);
			if( index != desiredIndex )
			{
				lines_.Remove(line);
				lines_.Insert(desiredIndex, line);
				int sign = (int)Mathf.Sign(desiredIndex - index);
				for( int i = index; i != desiredIndex; i += sign )
				{
					AnimManager.AddAnim(lines_[i], GetTargetPosition(lines_[i]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
				}
			}
		}
	}

	public void OnEndDrag(ShortLine line)
	{
		if( lines_.Contains(line) )
		{
			selectedIndex_ = lines_.IndexOf(line);
			AnimManager.AddAnim(line, GetTargetPosition(line), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
		}
	}

	#endregion


	#region button behaviour

	public void Open()
	{
		isOpened_ = true;
		OpenButton.gameObject.SetActive(false);
		scrollRect_.GetComponent<RectTransform>().anchoredPosition = new Vector3(0, scrollRect_.GetComponent<RectTransform>().anchoredPosition.y);
		GameContext.Window.OnHeaderWidthChanged();
	}

	public void Close()
	{
		isOpened_ = false;
		OpenButton.gameObject.SetActive(true);
		scrollRect_.GetComponent<RectTransform>().anchoredPosition = new Vector3(-LineWidth + ClosedLineWidth, scrollRect_.GetComponent<RectTransform>().anchoredPosition.y);
		GameContext.Window.OnHeaderWidthChanged();
	}

	public void DoneAll()
	{
		List<Line> doneLines = new List<Line>(from shortline in doneLines_ select shortline.BindedLine);

		AnimManager.AddShakeAnim(scrollRect_, 15.0f, 0.15f, 0.025f, ParamType.PositionY);

		foreach(Tree tree in GameContext.Window)
		{
			tree.ActionManager.StartChain();
		}
		foreach( Line line in doneLines )
		{
			line.Tree.ActionManager.Execute(new Action(
				execute: () =>
				{
					line.IsOnList = false;
					ShortLine shortline = FindBindedLine(line);
					if( shortline != null )
					{
						RemoveShortLine(shortline);
					}
				},
				undo: () =>
				{
					line.IsOnList = true;
					InstantiateShortLine(line);
				}));
		}
		foreach( Tree tree in GameContext.Window )
		{
			tree.ActionManager.EndChain();
		}
	}
	
	#endregion


	#region utils

	// IEnumerable<ShortLine>
	public IEnumerator<ShortLine> GetEnumerator()
	{
		foreach( ShortLine line in lines_ )
		{
			yield return line;
		}
		foreach( ShortLine line in doneLines_ )
		{
			yield return line;
		}
	}
	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.GetEnumerator();
	}

	Vector3 GetTargetPosition(ShortLine line)
	{
		if( line.IsDone )
		{
			return Vector3.down * LineHeight * (lines_.Count + doneLines_.IndexOf(line));
		}
		else
		{
			return Vector3.down * LineHeight * lines_.IndexOf(line);
		}
	}

	void UpdateSelection(List<ShortLine> list, int oldSelectIndex)
	{
		if( oldSelectIndex < list.Count )
		{
			list[oldSelectIndex].Select();
		}
		else
		{
			selectedLine_ = null;
			selectedIndex_ = -1;
		}
	}

	void AnimOnInstantiate(ShortLine shortLine)
	{
		shortLine.transform.localPosition = Vector3.zero;
		AnimManager.AddAnim(shortLine, GetTargetPosition(shortLine), ParamType.Position, AnimType.BounceIn, 0.25f);
		shortLine.Background = GameContext.Config.ShortLineBackColor;
		if( shortLine.IsDone == false )
		{
			AnimManager.AddAnim(shortLine, GameContext.Config.ShortLineAccentColor, ParamType.Color, AnimType.Time, 0.15f);
			AnimManager.AddAnim(shortLine, GameContext.Config.ShortLineColor, ParamType.Color, AnimType.Time, 0.1f, 0.15f);
		}
		UIMidairPrimitive primitive = shortLine.GetComponentInChildren<UIMidairPrimitive>();
		AnimManager.AddAnim(primitive, 8.0f, ParamType.PrimitiveWidth, AnimType.Time, 0.05f, 0.25f);
		AnimManager.AddAnim(primitive, 1.0f, ParamType.PrimitiveWidth, AnimType.Time, 0.2f, 0.3f);
	}

	void AnimLinesToTargetPosition(int startIndex, int endIndex)
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
			AnimManager.AddAnim(lines_[i], GetTargetPosition(lines_[i]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
		}
	}

	void AnimDoneLinesToTargetPosition(int startIndex, int endIndex)
	{
		if( startIndex > endIndex )
		{
			int start = endIndex;
			endIndex = startIndex;
			startIndex = start;
		}
		if( startIndex < 0 || doneLines_.Count <= endIndex )
		{
			return;
		}
		for( int i = startIndex; i <= endIndex; ++i )
		{
			AnimManager.RemoveOtherAnim(doneLines_[i], ParamType.Position);
			AnimManager.AddAnim(doneLines_[i], GetTargetPosition(doneLines_[i]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
		}
	}

	#endregion
}
