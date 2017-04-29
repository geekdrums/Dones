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
	protected ReactiveProperty<string> textRx_ = new ReactiveProperty<string>();

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
					Field.StartCoroutine(ActivateCoroutine(GameContext.Config.AnimTime / 2));
				}
				AdjustLayoutRecursive();
			}
		}
	}
	protected bool isFolded_ = false;

	public bool EnableRecursiveLayout { get; set; }

	public GameObject Binding { get; protected set; }
	public TextField Field { get; protected set; }

	#endregion


	public Line(string text = "")
	{
		textRx_.Value = text;
		IsFolded = false;
		IsDone = false;
		EnableRecursiveLayout = true;

		children_.ObserveAdd().Subscribe(x =>
		{
			Line child = x.Value;
			Line oldParent = child.parent_;
			child.parent_ = this;
			if( oldParent != null )
			{
				oldParent.children_.Remove(child);
			}

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
				AdjustLayoutRecursive(x.Index);
			}
		});

		children_.ObserveRemove().Subscribe(x =>
		{
			Line child = x.Value;

			if( child.parent_ == this && child.Binding != null )
			{
				MonoBehaviour.Destroy(child.Binding);
				child.parent_ = null;
			}

			if( IsFolded == false )
			{
				AdjustLayoutRecursive(x.Index);
			}
		});
	}
	
	public void Bind(GameObject binding)
	{
		Binding = binding;
		Field = binding.GetComponent<TextField>();
		if( Field != null )
		{
			Field.BindedLine = this;
			Field.text = textRx_.Value;
			Field.onValueChanged.AsObservable().Subscribe(text => textRx_.Value = text).AddTo(Field);
			textRx_.Subscribe(text => Field.text = text).AddTo(Field);
			if( parent_ != null && parent_.Binding != null )
			{
				Binding.transform.parent = parent_.Binding.transform;
				Field.transform.localPosition = TargetPosition;
			}
			children_.ObserveCountChanged(true).Subscribe(x =>
			{
				TreeToggle toggle = Field.GetComponentInChildren<TreeToggle>();
				toggle.interactable = (x > 0);
				AnimManager.AddAnim(toggle.targetGraphic, toggle.interactable && toggle.isOn ? 0 : 90, ParamType.RotationZ, AnimType.Time, GameContext.Config.AnimTime);
			}).AddTo(Field);
		}
	}


	#region Tree params

	public Line Parent { get { return parent_; } private set { parent_ = value; } }
	protected Line parent_;
	protected ReactiveCollection<Line> children_ = new ReactiveCollection<Line>();

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
	public int Count { get { return children_.Count; } }
	public void Add(Line child) { children_.Add(child); }
	public void Insert(int index, Line child) { children_.Insert(index, child); }
	public void Remove(Line child) { children_.Remove(child); }

	#endregion


	#region Layout

	protected void AdjustLayout()
	{
		if( Binding != null )
		{
			AnimManager.AddAnim(Binding, TargetPosition, ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
		}
	}

	protected void AdjustLayoutRecursive(int startIndex = 0)
	{
		if( startIndex < Count )
		{
			Vector3 target = children_[startIndex].TargetPosition;
			if( EnableRecursiveLayout == false )
			{
				if( children_[startIndex].Binding != null )
				{
					AnimManager.AddAnim(children_[startIndex].Binding, target, ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
				}
				return;
			}

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
			parent_.AdjustLayoutRecursive(IndexInParent + 1);
		}
	}

	protected virtual Vector3 TargetPosition
	{
		get
		{
			return new Vector3(GameContext.Config.WidthPerLevel, -IndexInLocalTree * GameContext.Config.HeightPerLine);
		}
	}

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
