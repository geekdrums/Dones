using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

// Window - Tree - [ Line ]
public class Line : IEnumerable<Line>
{
	#region params
	
	public string Text
	{
		get { return text_; }
		set
		{
			text_ = value;
			if( Field != null )
			{
				Field.SetTextDirectly(text_);
			}
		}
	}
	protected string text_;
	public int TextLength { get { return text_.Length; } }
	public override string ToString() { return Text; }

	public bool IsFolded
	{
		get { return isFolded_; }
		set
		{
			if( isFolded_ != value )
			{
				isFolded_ = value;

				foreach( Line line in children_ )
				{
					if( line.Binding != null )
					{
						line.Binding.SetActive(isFolded_ == false);
					}
				}
				if( Toggle != null )
				{
					Toggle.SetFold(IsFolded);
				}
			}
		}
	}
	protected bool isFolded_ = false;

	public bool IsDone
	{
		get { return isDone_; }
		set
		{
			if( isDone_ != value )
			{
				isDone_ = value;
				if( Field != null )
				{
					Field.SetDone(isDone_);
				}
			}
		}
	}
	protected bool isDone_ = false;


	public Vector3 TargetPosition { get; protected set; }

	public GameObject Binding { get; protected set; }
	public Tree Tree { get; protected set; }
	public TextField Field { get; protected set; }
	public TreeToggle Toggle { get; protected set; }
	
	protected IDisposable fieldSubscription_;
	protected IDisposable toggleSubscription_;
	
	#endregion


	public Line(string text = "", bool loadTag = false)
	{
		if( loadTag )
		{
			LoadTag(ref text);
		}
		text_ = text;
	}


	#region TextInputAction

	public abstract class TextAction : IAction
	{
		public StringBuilder Text = new StringBuilder();
		public int CaretPos;

		protected Line line_;

		public TextAction(Line line)
		{
			line_ = line;
			CaretPos = line.Field.CaretPosision;
		}

		public abstract void Execute();
		public abstract void Undo();
		public abstract void Redo();
	}

	public class TextInputAction : TextAction
	{
		public TextInputAction(Line line) : base(line)
		{
		}

		public override void Execute()
		{

		}

		public override void Undo()
		{
			line_.Field.IsFocused = true;
			line_.Field.CaretPosision = CaretPos;
			line_.Text = line_.text_.Remove(CaretPos, Text.Length);
		}

		public override void Redo()
		{
			line_.Field.IsFocused = true;
			line_.Text = line_.text_.Insert(CaretPos, Text.ToString());
			line_.Field.CaretPosision = CaretPos + Text.Length;
		}
	}

	public class TextDeleteAction : TextAction
	{
		public TextDeleteAction(Line line) : base(line)
		{
		}

		public override void Execute()
		{

		}

		public override void Undo()
		{
			line_.Field.IsFocused = true;
			line_.Text = line_.text_.Insert(CaretPos, Text.ToString());
			line_.Field.CaretPosision = CaretPos + Text.Length;
		}

		public override void Redo()
		{
			line_.Field.IsFocused = true;
			line_.Field.CaretPosision = CaretPos;
			line_.Text = line_.text_.Remove(CaretPos, Text.Length);
		}
	}

	protected TextAction textAction_ = null;
	protected float lastTextActionTime_ = 0;

	protected void OnTextChanged(string newText)
	{
		int oldCaretPos = Field.CaretPosision;
		int currentCaretPos = Field.ActualCaretPosition;

		if( Input.compositionString.Length > 0 )
		{
			// compositionStringがある状態で日本語入力を確定させると挿入位置がズレるバグへの対処
			currentCaretPos -= Input.compositionString.Length;
			Field.ActualCaretPosition = currentCaretPos;
		}

		if( oldCaretPos < currentCaretPos )
		{
			if( textAction_ == null || textAction_ is TextInputAction == false || Time.time - lastTextActionTime_ > GameContext.Config.TextInputFixIntervalTime )
			{
				textAction_ = new TextInputAction(this);
				Tree.ActionManager.Execute(textAction_);
			}
			string appendText = newText.Substring(oldCaretPos, currentCaretPos - oldCaretPos);
			textAction_.Text.Append(appendText);
			if( appendText == " " )
			{
				FixTextInputAction();
			}
		}
		else
		{
			if( textAction_ == null || textAction_ is TextDeleteAction == false || Time.time - lastTextActionTime_ > GameContext.Config.TextInputFixIntervalTime )
			{
				textAction_ = new TextDeleteAction(this);
				Tree.ActionManager.Execute(textAction_);
			}

			int deletedCount = text_.Length - newText.Length;
			if( oldCaretPos > currentCaretPos )	// backspace
			{
				string deletedText = text_.Substring(currentCaretPos, deletedCount);
				textAction_.Text.Insert(0, deletedText);
			}
			else // if( oldCaretPos == currentCaretPos ) delete
			{
				string deletedText = text_.Substring(oldCaretPos, deletedCount);
				textAction_.Text.Append(deletedText);
			}
			textAction_.CaretPos = currentCaretPos;
		}

		lastTextActionTime_ = Time.time;
		text_ = newText;

		if( IsDone ) Field.UpdateDone();
	}

	public void FixTextInputAction()
	{
		textAction_ = null;
	}

	#endregion


	#region Binding

	public void Bind(GameObject binding)
	{
		Binding = binding;
		Field = Binding.GetComponent<TextField>();
		if( Field != null )
		{
			if( parent_ != null && parent_.Binding != null )
			{
				Field.transform.SetParent(parent_.Binding.transform);
				Field.transform.localPosition = CalcTargetPosition();
			}

			Field.BindedLine = this;
			Field.text = text_;
			fieldSubscription_ = Field.onValueChanged.AsObservable().Subscribe(text =>
			{
				OnTextChanged(text);
			});
			Field.SetDone(isDone_, withAnim: false);

			Toggle = Field.GetComponentInChildren<TreeToggle>();
			toggleSubscription_ = children_.ObserveCountChanged(true).Select(x => x > 0).DistinctUntilChanged().Subscribe(hasChild =>
			{
				Toggle.interactable = hasChild;
				AnimManager.AddAnim(Toggle.targetGraphic, Toggle.interactable && Toggle.isOn ? 0 : 90, ParamType.RotationZ, AnimType.Time, GameContext.Config.AnimTime);
			}).AddTo(Field);
			Toggle.SetFold(IsFolded);

			if( parent_ != null && parent_.IsFolded )
			{
				Binding.SetActive(false);
			}
		}
		else
		{
			Tree = Binding.GetComponent<Tree>();
		}
	}

	public void ReBind()
	{
		Binding.SetActive(true);
		Bind(Binding);
		Tree.OnReBind(this);
	}

	public void UnBind()
	{
		Field.BindedLine = null;
		Binding = null;
		Field = null;
	}

	#endregion


	#region Tree params

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
	public Line Parent { get { return parent_; } private set { parent_ = value; } }
	public int Count { get { return children_.Count; } }
	public void Add(Line child)
	{
		Insert(Count, child);
	}
	public void Insert(int index, Line child)
	{
		children_.Insert(index, child);
		child.Tree = Tree;

		Line oldParent = child.parent_;
		child.parent_ = this;
		if( oldParent != null && oldParent != this )
		{
			oldParent.children_.Remove(child);
		}

		if( child.Field == null || child.Field.BindedLine != child )
		{
			Tree.Bind(child);
		}
		else if( child.Field.BindedLine == child && child.Field.gameObject.activeSelf == false )
		{
			child.ReBind();
		}
		else
		{
			child.Binding.transform.SetParent(this.Binding.transform, worldPositionStays: true);
			child.AdjustLayout();
		}
	}
	public void Remove(Line child)
	{
		children_.Remove(child);
		if( child.parent_ == this && child.Binding != null )
		{
			child.fieldSubscription_.Dispose();
			child.toggleSubscription_.Dispose();
			child.parent_ = null;
			Tree.OnRemove(child);
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
	
	public IEnumerable<Line> GetVisibleChildren()
	{
		if( this.IsFolded == false )
		{
			foreach( Line child in children_ )
			{
				yield return child;
				foreach( Line visibleLine in child.GetVisibleChildren() )
				{
					yield return visibleLine;
				}
			}
		}
	}

	public IEnumerable<Line> GetAllChildren()
	{
		foreach( Line child in children_ )
		{
			yield return child;
			foreach( Line grandchild in child.GetAllChildren() )
			{
				yield return grandchild;
			}
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

	public void AdjustLayoutRecursive(int startIndex = 0, Predicate<Line> predToBreak = null)
	{
		if( startIndex < Count )
		{
			Vector3 target = children_[startIndex].CalcTargetPosition();
			for( int i = startIndex; i < Count; ++i )
			{
				if( predToBreak != null && predToBreak(children_[i]) )
				{
					return;
				}
				if( children_[i].Binding != null )
				{
					if( target == children_[i].Binding.transform.localPosition && AnimManager.IsAnimating(children_[i].Binding) == false )
					{
						// これ以下は高さが変わっていないので、再計算は不要
						return;
					}

					children_[i].TargetPosition = target;
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
			parent_.AdjustLayoutRecursive(Index + 1, predToBreak);
		}
	}

	public void AdjustLayoutInChildren()
	{
		if( Count > 0 )
		{
			Vector3 target = children_[0].CalcTargetPosition();
			for( int i = 0; i < Count; ++i )
			{
				if( children_[i].Binding != null )
				{
					children_[i].TargetPosition = target;
					AnimManager.AddAnim(children_[i].Binding, target, ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
				}
				target.y -= (1 + children_[i].VisibleChildCount) * GameContext.Config.HeightPerLine;
			}
		}
	}

	protected Vector3 CalcTargetPosition(Direction dir = Direction.XY)
	{
		switch(dir)
		{
		case Direction.XY:
			TargetPosition = new Vector3(GameContext.Config.WidthPerLevel, -IndexInLocalTree * GameContext.Config.HeightPerLine);
			break;
		case Direction.X:
			TargetPosition = new Vector3(GameContext.Config.WidthPerLevel, Binding.transform.localPosition.y);
			break;
		case Direction.Y:
			TargetPosition = new Vector3(Binding.transform.localPosition.x, -IndexInLocalTree * GameContext.Config.HeightPerLine);
			break;
		}
		return TargetPosition;
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

	public int Index { get { return parent_ != null ? parent_.children_.IndexOf(this) : -1; } }

	public int IndexInLocalTree
	{
		get
		{
			int index = 0;
			if( parent_ != null )
			{
				if( parent_.IsFolded == false )
				{
					int indexInParent = Index;
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
					if( parent.Count > child.Index + 1 )
					{
						return parent[child.Index + 1];
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
					return parent_[Index - 1].LastVisibleLine;
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
	
	public Vector3 TargetAbsolutePosition
	{
		get
		{
			if( parent_.parent_ != null ) return TargetPosition + parent_.TargetAbsolutePosition;
			else if( Field != null ) return TargetPosition + Field.transform.parent.position;
			else return TargetPosition;
		}
	}

	#endregion

	#region save & load

	public static string TabString = "	";
	public static string FoldTag = "<f>";
	public static string DoneTag = "<d>";
	public void AppendStringTo(StringBuilder builder, bool appendTag = false)
	{
		int level = Level - 1;
		for( int i = 0; i < level; ++i )
		{
			builder.Append(TabString);
		}
		builder.Append(Text);
		if( appendTag )
		{
			if( IsFolded )
			{
				builder.Append(FoldTag);
			}
			if( IsDone )
			{
				builder.Append(DoneTag);
			}
		}
		builder.AppendLine();
	}

	public void LoadTag(ref string text)
	{
		if( text.EndsWith(DoneTag) )
		{
			text = text.Remove(text.Length - DoneTag.Length);
			IsDone = true;
		}
		if( text.EndsWith(FoldTag) )
		{
			text = text.Remove(text.Length - FoldTag.Length);
			IsFolded = true;
		}
	}

	#endregion
}
