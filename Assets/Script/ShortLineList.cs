using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ShortLineList : MonoBehaviour {

	public ShortLine ShortLinePrefab;
	public float LineHeight = 30;

	List<ShortLine> lines_ = new List<ShortLine>();
	List<ShortLine> doneLines_ = new List<ShortLine>();
	ShortLine selectedLine_;
	int selectedIndex_ = -1;

	// Use this for initialization
	void Start () {
		GameContext.LineList = this;
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
			if( selectedIndex_ < lines_.Count )
			{
				lines_[selectedIndex_].Select();
			}
			else
			{
				selectedLine_ = null;
				selectedIndex_ = -1;
			}
		}
	}

	public void InstantiateShortLine(Line line = null, bool withAnim = true)
	{
		ShortLine shortLine = Instantiate(ShortLinePrefab.gameObject, this.transform).GetComponent<ShortLine>();
		lines_.Add(shortLine);
		if( line != null )
		{
			shortLine.Bind(line);
		}
		if( withAnim )
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

		for( int i = 0; i < doneLines_.Count; ++i )
		{
			AnimManager.AddAnim(doneLines_[i], GetTargetPosition(doneLines_[i]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
		}
	}

	public void RemoveShortLine(Line line)
	{
		ShortLine removeLine = null;
		foreach(ShortLine shortLine in lines_)
		{
			if( shortLine.BindedLine == line )
			{
				removeLine = shortLine;
				break;
			}
		}
		if( removeLine != null )
		{
			float animTime = 0.2f;
			AnimManager.AddAnim(removeLine, GameContext.Config.ShortLineBackColor, ParamType.Color, AnimType.Time, animTime, 0.0f, AnimEndOption.Destroy);
			AnimManager.AddAnim(removeLine.GetComponentInChildren<Text>(), GameContext.Config.ShortLineBackColor, ParamType.TextColor, AnimType.Time, animTime);
			AnimManager.AddAnim(removeLine.GetComponentInChildren<UIMidairPrimitive>(), 0.0f, ParamType.PrimitiveArc, AnimType.Time, animTime);
			int index = lines_.IndexOf(removeLine);
			lines_.Remove(removeLine);
			for( int i = index; i < lines_.Count; ++i )
			{
				AnimManager.AddAnim(lines_[i], GetTargetPosition(lines_[i]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime, animTime);
			}
			for( int i = 0; i < doneLines_.Count; ++i )
			{
				AnimManager.AddAnim(doneLines_[i], GetTargetPosition(doneLines_[i]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime, animTime);
			}
		}
	}

	public void OnSelect(ShortLine line)
	{
		if( lines_.Contains(line) )
		{
			selectedLine_ = line;
			selectedIndex_ = lines_.IndexOf(selectedLine_);
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
			int index = lines_.IndexOf(line);
			lines_.Remove(line);
			doneLines_.Insert(0, line);

			for( int i = index; i < lines_.Count; ++i )
			{
				AnimManager.AddAnim(lines_[i], GetTargetPosition(lines_[i]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
			}
			AnimManager.AddAnim(line, GetTargetPosition(line), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
		}
		else
		{
			int index = doneLines_.IndexOf(line);
			doneLines_.Remove(line);
			lines_.Add(line);

			for( int i = 0; i < index; ++i )
			{
				AnimManager.AddAnim(doneLines_[i], GetTargetPosition(doneLines_[i]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
			}
			AnimManager.AddAnim(line, GetTargetPosition(line), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
		}
		line.transform.SetAsLastSibling();
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

	public Vector3 GetTargetPosition(ShortLine line)
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
}
