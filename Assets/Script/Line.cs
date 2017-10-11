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
			if( IsOnList )
			{
				ShortLine shortline = GameContext.Window.LineList.FindBindedLine(this);
				if( shortline != null )
				{
					shortline.Text = text_;
				}
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
					Line parent = Parent;
					while( parent != null && parent.Toggle != null )
					{
						parent.Toggle.AnimToTargetVisual();
						parent = parent.Parent;
					}
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
			if( isLinkText_ || isComment_ ) return;

			if( isDone_ != value )
			{
				isDone_ = value;
				if( Field != null )
				{
					Field.SetIsDone(isDone_);
					Field.SetIsOnList(isOnList_);
				}
				if( IsOnList )
				{
					ShortLine bindedLine = GameContext.Window.LineList.FindBindedLine(this);
					if( bindedLine != null )
					{
						bindedLine.IsDone = isDone_;
					}
				}
			}
		}
	}
	protected bool isDone_ = false;

	public bool IsOnList
	{
		get { return isOnList_; }
		set
		{
			if( isLinkText_ || isComment_ ) return;

			if( isOnList_ != value )
			{
				isOnList_ = value;
				if( Field != null )
				{
					Field.SetIsOnList(isOnList_);
				}
			}
		}
	}
	protected bool isOnList_ = false;

	public bool IsLinkText { get { return isLinkText_; } }
	protected bool isLinkText_ = false;

	public bool IsClone
	{
		get { return isClone_; }
		set
		{
			if( isClone_ != value )
			{
				isClone_ = value;
				if( Field != null )
				{
					Field.SetIsClone(isClone_);
				}
			}
		}
	}
	protected bool isClone_ = false;

	public bool IsBold
	{
		get { return isBold_; }
		set
		{
			if( isBold_ != value )
			{
				isBold_ = value;
				if( Field != null )
				{
					Field.textComponent.fontStyle = isBold_ ? FontStyle.Bold : FontStyle.Normal;
				}
			}
		}
	}
	protected bool isBold_ = false;

	public bool IsComment
	{
		get { return isComment_; }
		set
		{
			if( isComment_ != value )
			{
				isComment_ = value;
				if( Field != null )
					Field.SetIsComment(isComment_);
			}
		}
	}
	protected bool isComment_ = false;

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
		if( IsOnList )
		{
			GameContext.Window.LineList.InstantiateShortLine(this);
		}
		CheckIsLink();
		CheckIsComment();
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

		if( oldCaretPos < text_.Length && Input.compositionString.Length > 0 )
		{
			// compositionStringがある状態で日本語入力を確定させると挿入位置がズレるバグへの対処
			//Debug.Log(string.Format("old={0}, caret={2} | current={1}, caret={3}, | compositionString={4}", text_, newText, oldCaretPos, currentCaretPos, Input.compositionString.Length));
			currentCaretPos -= Input.compositionString.Length;
			Field.ActualCaretPosition = currentCaretPos;
		}

		if( oldCaretPos < currentCaretPos )
		{
			if( textAction_ == null || textAction_ is TextInputAction == false || Time.time - lastTextActionTime_ > GameContext.Config.TextInputFixIntervalTime )
			{
				FixTextInputAction();
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
				FixTextInputAction();
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

		if( IsDone || IsOnList || IsLinkText )
		{
			Field.OnTextLengthChanged();
		}
		if( IsOnList )
		{
			ShortLine shortline = GameContext.Window.LineList.FindBindedLine(this);
			if( shortline != null )
			{
				shortline.Text = text_;
			}
		}
		CheckIsComment();
	}

	public void FixTextInputAction()
	{
		if( GameContext.Config.IsAutoSave && textAction_ != null )
		{
			Tree.Save();
		}

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
			Field.SetTextDirectly(text_);
			fieldSubscription_ = Field.onValueChanged.AsObservable().Subscribe(text =>
			{
				OnTextChanged(text);
			});

			UpdateBindingField();

			Toggle = Field.GetComponentInChildren<TreeToggle>();
			toggleSubscription_ = children_.ObserveCountChanged(true).DistinctUntilChanged().Subscribe(x =>
			{
				Toggle.AnimToTargetVisual();
				Line parent = Parent;
				while( parent != null && parent.Toggle != null)
				{
					parent.Toggle.AnimToTargetVisual();
					parent = parent.Parent;
				}
			});
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
		if( Field != null )
		{
			Field.BindedLine = null;
		}
		Binding = null;
		Field = null;
	}

	public void UpdateBindingField()
	{
		if( Field != null )
		{
			Field.SetIsClone(isClone_);
			Field.SetIsDone(isDone_, withAnim: false);
			Field.SetIsOnList(isOnList_, withAnim: false);
			Field.textComponent.fontStyle = isBold_ ? FontStyle.Bold : FontStyle.Normal;
			if( isLinkText_ )
				Field.SetIsLinkText(isLinkText_);
			if( isComment_ )
				Field.SetIsComment(isComment_);
		}
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
		Line oldParent = child.parent_;

		if( oldParent == this )
		{
			children_.Move(child.Index, index);
		}
		else
		{
			children_.Insert(index, child);
			child.Tree = Tree;
			child.parent_ = this;
			if( oldParent != null )
			{
				oldParent.children_.Remove(child);
			}
		}

		child.OnFoundParent();

		if( child.IsOnList )
		{
			ShortLine shortline = GameContext.Window.LineList.FindBindedLine(child);
			if( shortline == null )
			{
				GameContext.Window.LineList.InstantiateShortLine(child);
			}
		}
	}
	public void Remove(Line child)
	{
		children_.Remove(child);
		if( child.parent_ == this && child.Binding != null )
		{
			child.parent_ = null;
			child.OnLostParent();
		}
	}

	protected void OnFoundParent()
	{
		if( Field == null || Field.BindedLine != this )
		{
			// Fieldがまだ無い、またはヒープに返して他のLineに使われた
			Tree.Bind(this);
			foreach( Line child in this.GetAllChildren() )
			{
				child.OnFoundParent();
			}
		}
		else if( Field.BindedLine == this && Field.gameObject.activeSelf == false )
		{
			// ヒープに返したが、他のものには使われていなかった
			ReBind();
			foreach( Line child in this.GetAllChildren() )
			{
				child.OnFoundParent();
			}
		}
		else // Field != null && Field.BindedLine == this && && Field.gameObject.activeSelf
		{
			// 適切なFieldをもう持っている
			if( Binding.transform.parent != Parent.Binding.transform )
			{
				Binding.transform.SetParent(Parent.Binding.transform, worldPositionStays: true);
			}
			AdjustLayout();
		}
	}

	protected void OnLostParent()
	{
		fieldSubscription_.Dispose();
		toggleSubscription_.Dispose();
		Tree.OnLostParent(this);
		if( IsOnList )
		{
			ShortLine shortline = GameContext.Window.LineList.FindBindedLine(this);
			if( shortline != null )
			{
				GameContext.Window.LineList.RemoveShortLine(shortline);
			}
		}
		foreach(Line child in this.GetAllChildren())
		{
			child.OnLostParent();
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
			int sign = Field.RectY < other.Field.RectY ? 1 : -1;
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
			float heightPerLine = GameContext.Config.HeightPerLine;
			for( int i = startIndex; i < Count; ++i )
			{
				if( predToBreak != null && predToBreak(children_[i]) )
				{
					return;
				}
				if( children_[i].Field != null )
				{
					if( target == children_[i].Field.transform.localPosition && AnimManager.IsAnimating(children_[i].Binding) == false )
					{
						// これ以下は高さが変わっていないので、再計算は不要
						return;
					}

					children_[i].TargetPosition = target;
					AnimManager.AddAnim(children_[i].Binding, target, ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
				}
				target.y -= (1 + children_[i].VisibleChildCount) * heightPerLine;
			}
		}

		if( parent_ != null )
		{
			parent_.AdjustLayoutRecursive(Index + 1, predToBreak);
		}
	}

	public void AdjustFontSizeRecursive(int fontSize, float heightPerLine)
	{
		AdjustLayoutInChildren();
		for( int i = 0; i < Count; ++i )
		{
			if( children_[i].Field != null )
			{
				children_[i].Field.textComponent.fontSize = fontSize;
				children_[i].Field.RectHeight = heightPerLine;
				children_[i].Field.OnTextLengthChanged();
				children_[i].Toggle.AnimToTargetVisual();
				children_[i].AdjustFontSizeRecursive(fontSize, heightPerLine);
			}
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
			if( parent_ != null && parent_.parent_ != null )
			{
				level = parent_.Level + 1;
			}
			return level;
		}
	}

	public bool IsVisible
	{
		get
		{
			Line parent = parent_;
			while( parent != null )
			{
				if( parent.IsFolded )
				{
					return false;
				}
				parent = parent.parent_;
			}
			return true;
		}
		set
		{
			if( value )
			{
				Tree.ActionManager.StartChain();
				Line parent = parent_;
				while( parent != null )
				{
					if( parent.IsFolded )
					{
						Tree.OnFoldUpdated(parent, false);
					}
					parent = parent.parent_;
				}
				Tree.ActionManager.EndChain();
			}
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
					index += indexInParent + (parent_.parent_ == null ? 0 : 1);
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

	public Line NextSiblingLine
	{
		get
		{
			if( parent_ != null )
			{
				return parent_[Index + 1];
			}
			return null;
		}
	}

	public Line NextSiblingOrUnkleLine
	{
		get
		{
			Line sibling = NextSiblingLine;
			if( sibling != null ) return sibling;
			else if( parent_ != null && parent_.Parent != null )
			{
				return parent_.NextSiblingOrUnkleLine;
			}
			else return null;
		}
	}

	public Line PrevSiblingLine
	{
		get
		{
			if( parent_ != null )
			{
				return parent_[Index - 1];
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
	public static char TabChar = '	';
	public static string FoldTag = "<f>";
	public static string DoneTag = "<d>";
	public static string OnListTag = "<o>";
	public static string CloneTag = "<c>";
	public static string BoldTag = "<b>";

	public void AppendStringTo(StringBuilder builder, bool appendTag = false, int ignoreLevel = 0)
	{
		int level = Level;
		for( int i = 0; i < level - ignoreLevel; ++i )
		{
			builder.Append(TabString);
		}
		builder.Append(Text);
		if( appendTag )
		{
			if( IsBold )
			{
				builder.Append(BoldTag);
			}
			if( IsClone )
			{
				builder.Append(CloneTag);
			}
			if( IsFolded )
			{
				builder.Append(FoldTag);
			}
			if( IsDone )
			{
				builder.Append(DoneTag);
			}
			if( IsOnList )
			{
				builder.Append(OnListTag);
			}
		}
		builder.AppendLine();
	}

	public string GetTagStrings()
	{
		StringBuilder builder = new StringBuilder();
		if( IsBold )
		{
			builder.Append(BoldTag);
		}
		if( IsClone )
		{
			builder.Append(CloneTag);
		}
		if( IsFolded )
		{
			builder.Append(FoldTag);
		}
		if( IsDone )
		{
			builder.Append(DoneTag);
		}
		if( IsOnList )
		{
			builder.Append(OnListTag);
		}
		return builder.ToString();
	}

	public void LoadTag(ref string text)
	{
		if( text.EndsWith(OnListTag) )
		{
			text = text.Remove(text.Length - OnListTag.Length);
			IsOnList = true;
		}
		else
		{
			IsOnList = false;
		}

		if( text.EndsWith(DoneTag) )
		{
			text = text.Remove(text.Length - DoneTag.Length);
			IsDone = true;
		}
		else
		{
			IsDone = false;
		}

		if( text.EndsWith(FoldTag) )
		{
			text = text.Remove(text.Length - FoldTag.Length);
			IsFolded = true;
		}
		else
		{
			IsFolded = false;
		}

		if( text.EndsWith(CloneTag) )
		{
			text = text.Remove(text.Length - CloneTag.Length);
			IsClone = true;
		}
		else
		{
			IsClone = false;
		}

		if( text.EndsWith(BoldTag) )
		{
			text = text.Remove(text.Length - BoldTag.Length);
			IsBold = true;
		}
		else
		{
			IsBold = false;
		}
	}

	public void CheckIsLink()
	{
		isLinkText_ = text_.StartsWith("http");
		if( Field != null )
			Field.SetIsLinkText(isLinkText_);
	}
	
	public void CheckIsComment()
	{
		IsComment = text_.StartsWith("> ");
	}

	public bool NeedFixInput()
	{
		return textAction_ != null && Time.time - lastTextActionTime_ > GameContext.Config.TextInputFixIntervalTime;
	}

	public Line Clone()
	{
		Line line = new Line(text_);
		line.isDone_ = isDone_;
		line.isFolded_ = isFolded_;
		line.isLinkText_ = isLinkText_;
		line.isComment_ = isComment_;
		line.isOnList_ = false;
		line.isClone_ = true;
		return line;
	}

	#endregion
}
