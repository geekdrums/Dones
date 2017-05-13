using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

public class Line : IEnumerable<Line>
{
	#region params

	public string Text { get { return textRx_.Value; } set { textRx_.Value = value; } }
	public int TextLength { get { return textRx_.Value.Length; } }
	public override string ToString() { return Text; }
	protected ReactiveProperty<string> textRx_ = new ReactiveProperty<string>();

	public bool IsFolded
	{
		get { return isFolded_; }
		set
		{
			if( isFolded_ != value )
			{
				isFolded_ = value;

				if( isFolded_ )
				{
					foreach( Line line in children_ )
					{
						if( line.Binding != null )
						{
							line.Binding.SetActive(false);
						}
					}
				}
				else
				{
					if( Field != null )
					{
						Field.StartCoroutine(ActivateCoroutine(GameContext.Config.AnimTime / 2));
					}
				}

				if( Toggle != null && Toggle.isOn != !IsFolded )
				{
					Toggle.isOn = !IsFolded;
					AnimManager.AddAnim(Toggle.targetGraphic, Toggle.interactable && Toggle.isOn ? 0 : 90, ParamType.RotationZ, AnimType.Time, GameContext.Config.AnimTime);
				}
			}
		}
	}
	protected bool isFolded_ = false;

	public bool IsDone { get { return isDone_; } set { isDone_ = value; } }
	protected bool isDone_ = false;

	public GameObject Binding { get; protected set; }
	public TextField Field { get; protected set; }
	public TreeToggle Toggle { get; protected set; }

	#endregion

	public Line(string text = "")
	{
		textRx_.Value = text;
		IsFolded = false;
		IsDone = false;
	}

	public void Bind(GameObject binding)
	{
		Binding = binding;
		Field = Binding.GetComponent<TextField>();
		if( Field != null )
		{
			if( parent_ != null && parent_.Binding != null )
			{
				Binding.transform.SetParent(parent_.Binding.transform);
				Field.transform.localPosition = CalcTargetPosition();
			}

			Field.BindedLine = this;
			Field.text = textRx_.Value;
			Field.onValueChanged.AsObservable().Subscribe(text => textRx_.Value = text).AddTo(Field);
			textRx_.Subscribe(text => Field.text = text).AddTo(Field);

			Toggle = Field.GetComponentInChildren<TreeToggle>();
			children_.ObserveCountChanged(true).Select(x => x > 0).DistinctUntilChanged().Subscribe(hasChild =>
			{
				Toggle.interactable = hasChild;
				AnimManager.AddAnim(Toggle.targetGraphic, Toggle.interactable && Toggle.isOn ? 0 : 90, ParamType.RotationZ, AnimType.Time, GameContext.Config.AnimTime);
			}).AddTo(Field);
		}
	}


	#region Tree params

	public Line Parent { get { return parent_; } private set { parent_ = value; } }
	protected Line parent_;
	protected ReactiveCollection<Line> children_ = new ReactiveCollection<Line>();

	public Line this[int index]
	{
		get
		{
			if( 0 <= index && index < children_.Count )	return children_[index];
			else return null;
		}
		private set
		{
			children_[index] = value;
		}
	}
	public int Count { get { return children_.Count; } }
	public void Add(Line child)
	{
		Insert(Count, child);
	}
	public void Insert(int index, Line child)
	{
		children_.Insert(index, child);

		Line oldParent = child.parent_;
		child.parent_ = this;
		if( oldParent != null && oldParent != this )
		{
			oldParent.children_.Remove(child);
		}

		if( child.Binding != null && this.Binding != null )
		{
			child.Binding.transform.SetParent(this.Binding.transform, worldPositionStays: true);
		}
	}
	public void Remove(Line child)
	{
		children_.Remove(child);
		if( child.parent_ == this && child.Binding != null )
		{
			MonoBehaviour.Destroy(child.Binding);
			child.parent_ = null;
		}
	}

	#endregion


	#region Enumerators

	// IEnumerable<Line>
	public IEnumerator<Line> GetEnumerator()
	{
		foreach( Line line in children_ )
		{
			yield return line;
		}
	}
	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.GetEnumerator();
	}

	public IEnumerable<Line> GetBetween(Line other)
	{
		if( other == null )
		{
			yield return this;
		}
		else
		{
			int sign = Field.Rect.y < other.Field.Rect.y ? 1 : -1;
			for( Line line = this; line != other && line != null; line = (sign > 0 ? line.PrevVisibleLine : line.NextVisibleLine) )
			{
				yield return line;
			}
			yield return other;
		}
	}

	public IEnumerable<Line> GetUntil(float y)
	{
		Line line = null;
		if( Field.RectY > y )
		{
			for( line = this; line != null && line.Field.Rect.yMin > y; line = line.NextVisibleLine )
			{
				yield return line;
			}
			if( line != null ) yield return line;
		}
		else
		{
			for( line = this; line != null && line.Field.Rect.yMax < y; line = line.PrevVisibleLine )
			{
				yield return line;
			}
			if( line != null ) yield return line;
		}
	}

	#endregion


	#region Layout

	[Flags]
	public enum Direction
	{
		X = 0x01,
		Y = 0x10,
		XY = X | Y,
	}

	public void AdjustLayout(Direction dir = Direction.XY)
	{
		if( Binding != null )
		{
			AnimManager.AddAnim(Binding, CalcTargetPosition(dir), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
		}
	}

	public void AdjustLayoutRecursive(int startIndex = 0)
	{
		if( startIndex < Count )
		{
			Vector3 target = children_[startIndex].CalcTargetPosition();
			for( int i = startIndex; i < Count; ++i )
			{
				if( children_[i].Binding != null )
				{
					if( target == children_[i].Binding.transform.localPosition )
					{
						// これ以下は高さが変わっていないので、再計算は不要
						return;
					}

					AnimManager.AddAnim(children_[i].Binding, target, ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
					//debug
					//children_[i].Field.Foreground = ColorManager.MakeAlpha(children_[i].Field.Foreground, 0);
					//AnimManager.AddAnim(children_[i].Field.textComponent, 1, ParamType.TextAlphaColor, AnimType.Time, 0.5f);
				}
				target.y -= (1 + children_[i].VisibleChildCount) * GameContext.Config.HeightPerLine;
			}
		}

		if( parent_ != null )
		{
			parent_.AdjustLayoutRecursive(IndexInParent + 1);
		}
	}

	protected Vector3 CalcTargetPosition(Direction dir = Direction.XY)
	{
		switch(dir)
		{
		case Direction.XY:
			return new Vector3(GameContext.Config.WidthPerLevel, -IndexInLocalTree * GameContext.Config.HeightPerLine);
		case Direction.X:
			return new Vector3(GameContext.Config.WidthPerLevel, Binding.transform.localPosition.y);
		case Direction.Y:
			return new Vector3(Binding.transform.localPosition.x, -IndexInLocalTree * GameContext.Config.HeightPerLine);
		}
		return Vector3.zero;
	}

	#endregion


	#region coroutine

	protected IEnumerator ActivateCoroutine(float delay)
	{
		yield return new WaitForSeconds(delay);
		foreach( Line line in children_ )
		{
			if( line.Binding != null )
			{
				line.Binding.SetActive(true);
			}
		}
	}

	#endregion


	#region Properties
	
	public int Level
	{
		get
		{
			int level = 0;
			if( parent_ != null )
			{
				level = parent_.Level + 1;
			}
			return level;
		}
	}

	public bool HasVisibleChild { get { return IsFolded == false && children_.Count > 0; } }

	public int VisibleChildCount
	{
		get
		{
			int count = 0;
			if( this.IsFolded == false )
			{
				count += children_.Count;
				foreach( Line child in children_ )
				{
					count += child.VisibleChildCount;
				}
			}
			return count;
		}
	}

	public int IndexInParent { get { return parent_ != null ? parent_.children_.IndexOf(this) : -1; } }

	public int IndexInLocalTree
	{
		get
		{
			int index = 0;
			if( parent_ != null )
			{
				if( parent_.IsFolded == false )
				{
					int indexInParent = IndexInParent;
					index += indexInParent + 1;
					for( int i = 0; i < indexInParent; ++i )
					{
						index += parent_[i].VisibleChildCount;
					}
				}
			}
			return index;
		}
	}

	public int IndexInTree
	{
		get
		{
			int index = 0;
			if( parent_ != null )
			{
				index = parent_.IndexInTree + IndexInLocalTree;
			}
			return index;
		}
	}

	public IEnumerable<Line> VisibleTree
	{
		get
		{
			if( parent_ != null )
			{
				yield return this;
			}
			if( this.IsFolded == false )
			{
				foreach( Line child in children_ )
				{
					foreach( Line visibleLine in child.VisibleTree )
					{
						yield return visibleLine;
					}
				}
			}
		}
	}

	public Line NextVisibleLine
	{
		get
		{
			if( HasVisibleChild )
			{
				return children_[0];
			}
			else
			{
				Line parent = parent_;
				Line child = this;
				while( parent != null )
				{
					if( parent.Count > child.IndexInParent + 1 )
					{
						return parent[child.IndexInParent + 1];
					}
					child = parent;
					parent = parent.parent_;
				}
				return null;
			}
		}
	}

	public Line PrevVisibleLine
	{
		get
		{
			if( parent_ != null )
			{
				if( parent_.children_[0] != this )
				{
					return parent_[IndexInParent - 1].LastVisibleLine;
				}
				else if( parent_.parent_ != null )
				{
					return parent_;
				}
			}
			return null;
			
		}
	}

	public Line LastVisibleLine
	{
		get
		{
			if( HasVisibleChild )
			{
				return children_[children_.Count - 1].LastVisibleLine;
			}
			else return this;
		}
	}
	
	#endregion

}
