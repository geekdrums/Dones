using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Line : IEnumerable<Line>
{
	#region params

	public string Text { get; set; }

	public bool IsDone { get { return isDone_; } set { isDone_ = value; } }
	protected bool isDone_ = false;

	public bool IsFolded
	{
		get { return isFolded_; }
		set
		{
			if( isFolded_  != value )
			{
				isFolded_ = value;
				foreach( Line line in children_ )
				{
					if( line.Field != null ) line.Field.enabled = (isFolded_ == false);
				}
				AdjustLayoutInChildren();
			}
		}
	}
	protected bool isFolded_ = false;

	public GameObject Binding { get; protected set; }
	public TextField Field { get; protected set; }

	#endregion


	public Line(string text = "")
	{
		Text = text;
		IsFolded = false;
		IsDone = false;
	}
	
	public void Bind(GameObject binding)
	{
		Binding = binding;
		Field = binding.GetComponent<TextField>();
		if( Field != null )
		{
			Field.BindedLine = this;
			Field.text = Text;
			if( parent_ != null && parent_.Binding != null )
			{
				Binding.transform.parent = parent_.Binding.transform;
				Field.transform.localPosition = TargetPosition;
			}
		}
	}


	#region Tree params

	public Line Parent { get { return parent_; } private set { parent_ = value; } }
	protected Line parent_;

	public Line this[int index]
	{
		get
		{
			return children_[index];
		}
		private set
		{
			children_[index] = value;
		}
	}
	protected List<Line> children_ = new List<Line>();

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
	public int Count { get { return children_.Count; } }
	
	public void Add(Line child)
	{
		if( child.parent_ != null )
		{
			child.parent_.children_.Remove(child);
		}
		child.parent_ = this;
		children_.Add(child);

		if( child.Binding != null && this.Binding != null )
		{
			child.Binding.transform.parent = this.Binding.transform;
		}

		if( IsFolded )
		{
			IsFolded = false;
		}
		else
		{
			child.AdjustLayout();
		}
	}
	public void Insert(int index, Line child)
	{
		if( child.parent_ != null )
		{
			int oldIndexinParent = child.IndexInParent;
			child.parent_.children_.Remove(child);
			if( child.Level > this.Level )
			{
				child.parent_.AdjustLayoutInChildren(oldIndexinParent);
			}
		}
		child.parent_ = this;
		children_.Insert(index, child);

		if( child.Binding != null && this.Binding != null )
		{
			child.Binding.transform.parent = this.Binding.transform;
		}

		if( IsFolded )
		{
			IsFolded = false;
		}
		else
		{
			AdjustLayoutInChildren(index);
		}
	}
	public void Remove(Line child)
	{
		if( children_.Contains(child) == false )
		{
			return;
		}

		int indexInParent = child.IndexInParent;
		children_.Remove(child);
		child.parent_ = null;
		
		if( IsFolded == false )
		{
			AdjustLayoutInChildren(indexInParent);
		}
	}

	#endregion


	#region Layout

	protected void AdjustLayout()
	{
		if( Binding != null )
		{
			AnimManager.AddAnim(Binding, TargetPosition, ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
		}
	}

	protected void AdjustLayoutInChildren(int startIndex = 0)
	{
		if( startIndex < Count )
		{
			Vector3 target = children_[startIndex].TargetPosition;
			for( int i = startIndex; i < Count; ++i )
			{
				if( children_[i].Binding != null )
				{
					AnimManager.AddAnim(children_[i].Binding, target, ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
				}
				target.y -= (1 + children_[i].VisibleChildCount) * GameContext.Config.HeightPerLine;
			}
		}

		if( parent_ != null )
		{
			parent_.AdjustLayoutInChildren(IndexInParent + 1);
		}
	}

	protected virtual Vector3 TargetPosition
	{
		get
		{
			return new Vector3(GameContext.Config.WidthPerLevel, -IndexInLocalTree * GameContext.Config.HeightPerLine);
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
