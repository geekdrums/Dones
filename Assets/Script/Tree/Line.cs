using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

// Window > Note > Tree > [ Line ]
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
			if( Tree is LogTree == false )
			{
				foreach( string tag in Tags )
				{
					TagParent tagParent = GameContext.TagList.GetTagParent(tag);
					if( tagParent != null )
					{
						TaggedLine taggedline = tagParent.FindBindedLine(this);
						if( taggedline != null )
						{
							taggedline.Text = text_;
						}
					}
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

				if( isFolded_ )
				{
					foreach( Line line in children_ )
					{
						line.BackToHeap();
						foreach( Line child in line.GetVisibleChildren() )
						{
							child.BackToHeap();
						}
					}
				}
				else
				{
					foreach( Line line in children_ )
					{
						line.OnFoundParent();
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
					//Field.SetIsOnList(isOnList_);
				}
				if( Tree is LogTree == false )
				{
					foreach( string tag in Tags )
					{
						TaggedLine bindedLine = GameContext.TagList.GetTagParent(tag).FindBindedLine(this);
						if( bindedLine != null )
						{
							bindedLine.IsDone = isDone_;
						}
					}
				}
			}
		}
	}
	protected bool isDone_ = false;

	public bool HasAnyTags { get { return tags_.Count > 0; } }
	public IEnumerable<string> Tags
	{
		get
		{
			foreach(string tag in tags_)
			{
				yield return tag;
			}
		}
	}
	protected List<string> tags_ = new List<string>();

	public void AddTag(string tag)
	{
		tags_.Add(tag);
		Text = String.Format("{0} #{1}", text_, tag);
	}

	public void RemoveTag(string tag)
	{
		if( tags_.Contains(tag) )
		{
			tags_.Remove(tag);
			Text = text_.Remove(text_.LastIndexOf(tag) - 2, tag.Length + 2);
		}
	}

	public string TextWithoutHashTags
	{
		get
		{
			string text = text_;
			foreach( string tag in tags_ )
			{
				text = text.Remove(text.LastIndexOf(tag) - 1, tag.Length + 1);
			}
			text.TrimEnd(spaces);
			return text;
		}
	}

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

	public enum EBindState
	{
		Unbind,		// まだBindされていない、あるいはWeakBind状態から他のLineにFieldを使われた
		Bind,		// Bindされている
		WeakBind,	// Field変数にBindを残しているがHeapに返している状態。他のに使われる可能性がある。
	}

	public EBindState BindState { get; protected set; }

	public GameObject Binding { get; protected set; }
	public Tree Tree { get; protected set; }
	public LineField Field { get; protected set; }
	public TreeToggle Toggle { get; protected set; }
	
	protected IDisposable fieldSubscription_;
	protected IDisposable toggleSubscription_;

	#endregion
	

	public Line(string text = "", bool loadTag = false)
	{
		BindState = EBindState.Unbind;
		if( loadTag )
		{
			LoadLineTag(ref text);
		}
		text_ = text;
		tags_ = GetHashTags(text);
		CheckIsLink();
	}


	#region TextInputAction

	public abstract class TextAction : ActionBase
	{
		public StringBuilder Text = new StringBuilder();
		public int CaretPos { get; set; }
		public TagTextEditAction TagEdit { get; set; }

		protected Line line_;

		public TextAction(Line line)
		{
			line_ = line;
			CaretPos = line.Field.CaretPosision;
		}
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
			if( Text.Length == 0 ) return;

			line_.Field.IsFocused = true;
			line_.Field.CaretPosision = CaretPos;
			line_.Text = line_.text_.Remove(CaretPos, Text.Length);

			if( TagEdit != null )
			{
				TagEdit.Undo();
			}
		}

		public override void Redo()
		{
			if( Text.Length == 0 ) return;

			line_.Field.IsFocused = true;
			line_.Text = line_.text_.Insert(CaretPos, Text.ToString());
			line_.Field.CaretPosision = CaretPos + Text.Length;

			if( TagEdit != null )
			{
				TagEdit.Redo();
			}
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

			if( TagEdit != null )
			{
				TagEdit.Undo();
			}
		}

		public override void Redo()
		{
			line_.Field.IsFocused = true;
			line_.Field.CaretPosision = CaretPos;
			line_.Text = line_.text_.Remove(CaretPos, Text.Length);

			if( TagEdit != null )
			{
				TagEdit.Redo();
			}
		}
	}

	public class TagTextEditAction : ActionBase
	{
		Line line_;
		List<string> newTags_;
		List<string> oldTags_;

		public TagTextEditAction(Line line, List<string> tags)
		{
			line_ = line;
			oldTags_ = new List<string>(tags);
		}

		public override void Execute()
		{

		}

		public override void Undo()
		{
			TagEditDiff diff = CheckTagChanged(oldTags_, newTags_);
			UpdateTags(diff.RemoveTags, diff.AddTags);
		}

		public override void Redo()
		{
			TagEditDiff diff = CheckTagChanged(newTags_, oldTags_);
			UpdateTags(diff.RemoveTags, diff.AddTags);
		}

		public void SetNewTag(List<string> tags)
		{
			newTags_ = tags;
		}

		public void UpdateTags(List<string> removeTags, List<string> addTags)
		{
			foreach( string removeTag in removeTags )
			{
				line_.tags_.Remove(removeTag);
				TagParent tagParent = GameContext.TagList.GetTagParent(removeTag);
				if( tagParent != null )
				{
					tagParent.RemoveLine(line_);
				}
			}
			foreach( string addTag in addTags )
			{
				line_.tags_.Add(addTag);
				TagParent tagParent = GameContext.TagList.GetOrInstantiateTagParent(addTag);
				if( tagParent != null )
				{
					tagParent.InstantiateTaggedLine(line_);
				}
			}
		}

		public class TagEditDiff
		{
			public List<string> RemoveTags = new List<string>();
			public List<string> AddTags = new List<string>();
			public bool IsEdited { get { return RemoveTags.Count > 0 || AddTags.Count > 0; } }
		}

		static TagEditDiff SharedDiffObj = new TagTextEditAction.TagEditDiff();
		public static TagEditDiff CheckTagChanged(List<string> newTags, List<string> oldTags)
		{
			SharedDiffObj.RemoveTags.Clear();
			SharedDiffObj.AddTags.Clear();

			foreach( string oldTag in oldTags )
			{
				if( newTags.Contains(oldTag) == false )
				{
					SharedDiffObj.RemoveTags.Add(oldTag);
				}
			}
			foreach( string newTag in newTags )
			{
				if( oldTags.Contains(newTag) == false )
				{
					SharedDiffObj.AddTags.Add(newTag);
				}
			}

			return SharedDiffObj;
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
			// キャレットが正方向に移動しているので入力
			if( textAction_ == null || textAction_ is TextInputAction == false || Time.time - lastTextActionTime_ > GameContext.Config.TextInputFixIntervalTime )
			{
				FixTextInputAction();
				textAction_ = new TextInputAction(this);
				Tree.ActionManager.Execute(textAction_);
			}
			string appendText = newText.Substring(oldCaretPos, currentCaretPos - oldCaretPos);
			textAction_.Text.Append(appendText);
		}
		else
		{
			// キャレットが負方向に移動しているので削除
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

		if( Tree is LogTree == false )
		{
			// タグに変化があればそれもアクションに乗せる
			List<string> newTags = GetHashTags(newText);
			TagTextEditAction.TagEditDiff tagEditDiff = TagTextEditAction.CheckTagChanged(newTags, tags_);
			if( tagEditDiff.IsEdited )
			{
				if( textAction_.TagEdit == null )
				{
					textAction_.TagEdit = new TagTextEditAction(this, tags_);
				}
				textAction_.TagEdit.SetNewTag(newTags);
				textAction_.TagEdit.UpdateTags(tagEditDiff.RemoveTags, tagEditDiff.AddTags);
			}
		}

		lastTextActionTime_ = Time.time;

		text_ = newText;
		Tree.IsEdited = true;

#if UNITY_EDITOR
		if( Binding != null )
		{
			Binding.gameObject.name = text_;
		}
#endif

		if( IsDone || HasAnyTags || IsLinkText )
		{
			Field.OnTextLengthChanged();
		}
		if( Tree is LogTree == false )
		{
			foreach( string tag in Tags )
			{
				TaggedLine shortline = GameContext.TagList.GetTagParent(tag).FindBindedLine(this);
				if( shortline != null )
				{
					shortline.Text = text_;
				}
			}
		}
		CheckIsComment();
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
		Field = Binding.GetComponent<LineField>();
		BindState = EBindState.Bind;
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
		Tree.OnReBind(this);
		Bind(Binding);
	}

	public void UnBind()
	{
		if( Field != null )
		{
			Field.BindedLine = null;
			foreach( LineField childField in Field.GetComponentsInChildren<LineField>() )
			{
				childField.transform.SetParent(Tree.transform);
			}
		}
		Binding = null;
		Field = null;
		BindState = EBindState.Unbind;
	}

	public void BackToHeap()
	{
		if( fieldSubscription_ != null )
		{
			fieldSubscription_.Dispose();
			toggleSubscription_.Dispose();
			fieldSubscription_ = null;
			toggleSubscription_ = null;
		}
		Tree.BackToHeap(this);
		BindState = EBindState.WeakBind;
	}

	public void UpdateBindingField()
	{
		if( Field != null )
		{
			Field.SetIsClone(isClone_);
			Field.SetIsDone(isDone_, withAnim: false);
			//Field.SetIsOnList(isOnList_, withAnim: false);
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

		if( child.IsDone == false && child.Tree is LogTree == false )
		{
			foreach( string tag in child.Tags )
			{
				TagParent tagParent = GameContext.TagList.GetOrInstantiateTagParent(tag);
				TaggedLine taggedline = tagParent.FindBindedLine(child);
				if( taggedline == null )
				{
					tagParent.InstantiateTaggedLine(child);
				}
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
		if( IsVisible == false )
		{
			return;
		}

		if( Field == null || Field.BindedLine != this )
		{
			if( BindState != EBindState.Unbind )
			{
				Debug.Log(String.Format("{0} | state is not Unbind, is {1}", Text, BindState.ToString()));
			}
			// Fieldがまだ無い、またはヒープに返して他のLineに使われた
			Tree.Bind(this);
			foreach( Line child in this.GetAllChildren() )
			{
				child.OnFoundParent();
			}
		}
		else if( Field.BindedLine == this && Field.gameObject.activeSelf == false )
		{
			if( BindState != EBindState.WeakBind )
			{
				Debug.Log(String.Format("{0} | state is not WeakBind, is {1}", Text, BindState.ToString()));
			}
			// ヒープに返したが、他のものには使われていなかった
			ReBind();
			foreach( Line child in this.GetAllChildren() )
			{
				child.OnFoundParent();
			}
		}
		else // Field != null && Field.BindedLine == this && && Field.gameObject.activeSelf
		{
			if( BindState != EBindState.Bind )
			{
				Debug.Log(String.Format("{0} | state is not Bind, is {1}", Text, BindState.ToString()));
			}
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
		BackToHeap();
		if( Tree is LogTree == false )
		{
			foreach( string tag in Tags )
			{
				TagParent tagParent = GameContext.TagList.GetTagParent(tag);
				if( tagParent != null )
				{
					tagParent.RemoveLine(this);
				}
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
		if( 0 <= startIndex && startIndex < Count )
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
				List<Line> foldedLines = new List<Line>();
				while( parent != null )
				{
					if( parent.IsFolded )
					{
						foldedLines.Insert(0, parent);
					}
					parent = parent.parent_;
				}
				foreach( Line line in foldedLines )
				{
					Tree.OnFoldUpdated(line, false);
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

	public Line NextUnkleLine
	{
		get
		{
			if( parent_ != null )
			{
				return parent_.NextSiblingOrUnkleLine;
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
	public static string CloneTag = "<c>";
	public static string BoldTag = "<b>";
	public static string CommentTag = "> ";
	public static char[] spaces = new char[] { ' ' };

	public static List<string> GetHashTags(string text)
	{
		List<string> tags = new List<string>();
		string[] splitText = text.Split(spaces);
		for( int i = splitText.Length - 1; i >= 0; --i )
		{
			if( splitText[i].StartsWith("#") )
			{
				string tag = splitText[i].TrimStart('#');
				// todo check validate
				if( String.IsNullOrEmpty(tag) || tag.Contains('#') || tag.Contains('.') || tag.Contains(',') )
				{
					break;
				}
				bool invaid = false;
				foreach( char invalidChar in System.IO.Path.GetInvalidFileNameChars() )
				{
					if( tag.Contains(invalidChar) )
					{
						invaid = true;
						break;
					}
				}
				if( invaid )
				{
					break;
				}

				if( tags.Contains(tag) == false )
				{
					tags.Add(tag);
				}
			}
			else
			{
				break;
			}
		}
		return tags;
	}

	public void AppendStringTo(StringBuilder builder, bool appendTag = false, int ignoreLevel = 0)
	{
		int level = Level;
		for( int i = 0; i < level - ignoreLevel; ++i )
		{
			builder.Append(TabString);
		}
		if( appendTag )
		{
			if( IsComment )
			{
				builder.Append(CommentTag);
			}
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
		return builder.ToString();
	}

	public void LoadLineTag(ref string text)
	{
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

		if( text.StartsWith(CommentTag) )
		{
			text = text.Remove(0, CommentTag.Length);
			IsComment = true;
		}
		else
		{
			IsComment = false;
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
		if( text_.StartsWith(CommentTag) )
		{
			text_ = text_.Remove(0, CommentTag.Length);

			if( Field != null )
			{
				Field.SetTextDirectly(text_);
				Field.CaretPosision = 0;
			}

			if( textAction_ != null && textAction_ is TextInputAction )
			{
				textAction_.Text.Remove(0, CommentTag.Length);
			}
			FixTextInputAction();
			Tree.ActionManager.Execute(new Action(
				execute: () =>
				{
					IsComment = true;
				},
				undo: () =>
				{
					IsComment = false;
				}
				));
		}
	}

	public void CheckHashTags()
	{
		List<string> newTags = GetHashTags(text_);
		Line.TagTextEditAction.TagEditDiff tagEditDiff = Line.TagTextEditAction.CheckTagChanged(newTags, tags_);
		if( tagEditDiff.IsEdited )
		{
			Line.TagTextEditAction tagEdit = new Line.TagTextEditAction(this, tags_);
			tagEdit.SetNewTag(newTags);
			tagEdit.UpdateTags(tagEditDiff.RemoveTags, tagEditDiff.AddTags);
			Tree.ActionManager.Execute(tagEdit);
		}
	}

	public bool NeedFixInput()
	{
		return textAction_ != null && Time.time - lastTextActionTime_ > GameContext.Config.TextInputFixIntervalTime;
	}

	public Line Clone(bool removeHashTags = true)
	{
		Line line = new Line(text_);
		line.isDone_ = isDone_;
		line.isFolded_ = isFolded_;
		line.isLinkText_ = isLinkText_;
		line.isComment_ = isComment_;
		if( removeHashTags )
		{
			line.text_ = TextWithoutHashTags;
		}
		else
		{
			line.tags_ = new List<string>(tags_);
		}
		line.isClone_ = true;
		return line;
	}

	#endregion
}
