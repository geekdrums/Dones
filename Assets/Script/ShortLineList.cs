using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ShortLineList : MonoBehaviour, IEnumerable<ShortLine>
{
	#region editor params

	public ShortLine ShortLinePrefab;
	public float LineHeight = 30;

	#endregion


	#region params

	List<ShortLine> lines_ = new List<ShortLine>();
	List<ShortLine> doneLines_ = new List<ShortLine>();
	ShortLine selectedLine_;
	int selectedIndex_ = -1;
	int dragStartIndex_ = -1;

	public ActionManager ActionManager { get { return actionManager_; } }
	ActionManager actionManager_ = new ActionManager();

	LayoutElement layout_;
	ContentSizeFitter contentSizeFitter_;

	#endregion


	#region unity functions

	// Use this for initialization
	void Awake ()
	{
		layout_ = GetComponentInParent<LayoutElement>();
		contentSizeFitter_ = GetComponentInParent<ContentSizeFitter>();
	}

	// Update is called once per frame
	void Update () {
		if( selectedLine_ == null ) return;

		bool ctrl = Input.GetKey(KeyCode.LeftControl);
		bool shift = Input.GetKey(KeyCode.LeftShift);
		bool alt = Input.GetKey(KeyCode.LeftAlt);
		bool ctrlOnly = ctrl && !alt && !shift;

		if( Input.GetKeyDown(KeyCode.UpArrow) )
		{
			if( selectedIndex_ > 0 )
			{
				--selectedIndex_;
				if( alt )
				{
					lines_.Remove(selectedLine_);
					lines_.Insert(selectedIndex_, selectedLine_);
					AnimManager.AddAnim(selectedLine_, GetTargetPosition(selectedLine_), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
					AnimManager.AddAnim(lines_[selectedIndex_+1], GetTargetPosition(lines_[selectedIndex_ + 1]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
				}
				else
				{
					lines_[selectedIndex_].Select();
				}
			}
		}
		else if( Input.GetKeyDown(KeyCode.DownArrow) )
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
		else if( Input.GetKeyDown(KeyCode.Space) && ctrlOnly )
		{
			selectedLine_.Done();
			UpdateSelection(selectedIndex_);
		}
		else if( Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace) )
		{
			ShortLine line = selectedLine_;
			int index = selectedIndex_;
			Line bindedLine = line.BindedLine;
			actionManager_.Execute(new Action(
				execute: () =>
				{
					if( bindedLine != null )
					{
						bindedLine.IsOnList = false;
					}
					RemoveShortLine(line);
					UpdateSelection(index);
				},
				undo: ()=>
				{
					if( bindedLine != null )
					{
						bindedLine.IsOnList = true;
					}
					InstantiateShortLine(bindedLine, index);
					UpdateSelection(index);
					line = selectedLine_;
				}
				));
		}
	}

	#endregion


	#region add / remove lines

	public ShortLine InstantiateShortLine(string text, int index = -1, bool withAnim = true)
	{
		ShortLine shortLine = Instantiate(ShortLinePrefab.gameObject, this.transform).GetComponent<ShortLine>();
		shortLine.Text = text;

		if( index < 0 )
		{
			index = lines_.Count;
			lines_.Add(shortLine);
		}
		else
		{
			lines_.Insert(index, shortLine);
		}

		if( withAnim )
		{
			AnimOnInstantiate(shortLine);
		}

		AnimLinesToTargetPosition(index, lines_.Count - 1);
		AnimDoneLinesToTargetPosition(0, doneLines_.Count - 1);

		UpdateLayoutElement();

		return shortLine;
	}

	public ShortLine InstantiateShortLine(Line line, int index = -1, bool withAnim = true)
	{
		ShortLine shortLine = InstantiateShortLine(line.Text, index, withAnim);
		shortLine.Bind(line);
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

	public void RemoveShortLine(ShortLine shortline)
	{
		float animTime = 0.2f;
		AnimManager.AddAnim(shortline, GameContext.Config.ShortLineBackColor, ParamType.Color, AnimType.Time, animTime, 0.0f, AnimEndOption.Destroy);
		AnimManager.AddAnim(shortline.GetComponentInChildren<Text>(), GameContext.Config.ShortLineBackColor, ParamType.TextColor, AnimType.Time, animTime);
		AnimManager.AddAnim(shortline.GetComponentInChildren<UIMidairPrimitive>(), 0.0f, ParamType.PrimitiveArc, AnimType.Time, animTime);
		int index = lines_.IndexOf(shortline);
		if( index >= 0 )
		{
			lines_.Remove(shortline);
			AnimLinesToTargetPosition(index, lines_.Count - 1);
		}
		index = doneLines_.IndexOf(shortline);
		if( index < 0 ) index = 0;
		AnimDoneLinesToTargetPosition(index, doneLines_.Count - 1);

		UpdateLayoutElement();
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
		if( lines_.Contains(line) )
		{
			selectedLine_ = line;
			selectedIndex_ = lines_.IndexOf(selectedLine_);
			GameContext.CurrentActionManager = actionManager_;
		}
	}

	public void OnDeselect(ShortLine line)
	{
		if( selectedLine_ == line )
		{
			selectedLine_ = null;
		}
	}

	public void OnDoneChanged(ShortLine line)
	{
		// move to target position
		if( line.IsDone )
		{
			if( lines_.Contains(line) )
			{
				int index = lines_.IndexOf(line);
				lines_.Remove(line);
				doneLines_.Insert(0, line);

				AnimLinesToTargetPosition(index, lines_.Count - 1);
				AnimManager.AddAnim(line, GetTargetPosition(line), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
				line.transform.SetAsLastSibling();
			}
		}
		else
		{
			if( doneLines_.Contains(line) )
			{
				int index = doneLines_.IndexOf(line);
				doneLines_.Remove(line);
				lines_.Add(line);

				AnimDoneLinesToTargetPosition(0, index - 1);
				AnimManager.AddAnim(line, GetTargetPosition(line), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
				line.transform.SetAsLastSibling();
			}
		}
	}

	public void OnBeginDrag(ShortLine line)
	{
		if( lines_.Contains(line) )
		{
			dragStartIndex_ = lines_.IndexOf(line);
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

			int newIndex = selectedIndex_;
			int oldIndex = dragStartIndex_;
			actionManager_.Execute(new Action(
				execute:()=>
				{
					AnimManager.AddAnim(line, GetTargetPosition(line), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
				},
				undo: () =>
				{
					lines_.Remove(line);
					lines_.Insert(oldIndex, line);
					AnimLinesToTargetPosition(newIndex, oldIndex);
				},
				redo: () => 
				{
					lines_.Remove(line);
					lines_.Insert(newIndex, line);
					AnimLinesToTargetPosition(newIndex, oldIndex);
				}
				));
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

	void UpdateSelection(int oldUpdateIndex)
	{
		if( oldUpdateIndex < lines_.Count )
		{
			lines_[oldUpdateIndex].Select();
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
		AnimManager.AddAnim(shortLine, GameContext.Config.ShortLineAccentColor, ParamType.Color, AnimType.Time, 0.15f);
		AnimManager.AddAnim(shortLine, GameContext.Config.ShortLineColor, ParamType.Color, AnimType.Time, 0.1f, 0.15f);
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
			AnimManager.AddAnim(doneLines_[i], GetTargetPosition(doneLines_[i]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
		}
	}

	#endregion
}
