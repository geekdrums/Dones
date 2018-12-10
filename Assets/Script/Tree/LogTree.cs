using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Window - LogNote - [ LogTree ] - Line
public class LogTree : Tree
{
	#region params

	public LogNote OwnerLogNote { get { return ownerNote_ as LogNote; } }

	public DateTime Date { get { return date_; } }
	DateTime date_;

	public LayoutElement Layout { get { return layout_; } }
	LayoutElement layout_;
	ContentSizeFitter contentSizeFitter_;

	#endregion


	#region add / remove log

	public void AddLog(Line line)
	{
		SuspendLayout();

		// todo rootLineに限らず、入力用のLineを用意できるように方法を変える
		if( rootLine_.Count == 1 && rootLine_[0].Text == "" )
		{
			rootLine_.Remove(rootLine_[0]);
		}
		
		if( titleLine_ == null )
		{
			CreateTitleLine();
		}

		// clone & stack parents
		Stack<Line> cloneAncestors = new Stack<Line>();
		Line cloneLine = line.Clone();
		cloneAncestors.Push(cloneLine);
		Line originalParent = line.Parent;
		while( originalParent.IsTitleLine == false )
		{
			Line cloneParent = originalParent.Clone();
			cloneParent.IsDone = false;
			cloneParent.IsFolded = false;
			cloneAncestors.Push(cloneParent);
			originalParent = originalParent.Parent;
		}

		// search for same parents
		Line addLine = cloneAncestors.Pop();
		Line addParent = titleLine_;
		bool found = true;
		while( found && addLine != null )
		{
			found = false;
			foreach( Line child in addParent )
			{
				if( child.IsClone && child.Text == addLine.Text )
				{
					found = true;
					addParent = child;
					if( cloneAncestors.Count > 0 )
					{
						addLine = cloneAncestors.Pop();
					}
					else
					{
						// 同一のlineが見つかったので、Doneだけ更新する
						child.IsDone = addLine.IsDone;
						addLine = null;
					}
					break;
				}
			}
		}

		// add clone and its parents
		if( addLine != null )
		{
			addParent.Add(addLine);
			RequestLayout(addParent.NextSiblingLine);
			addParent = addLine;
			while( cloneAncestors.Count > 0 )
			{
				addLine = cloneAncestors.Pop();
				addParent.Add(addLine);
				addParent = addLine;
			}
			cloneLine.Field.SetIsDone(cloneLine.IsDone);
		}
		// and its children
		AddLogChildRecursive(line, addParent);
		RequestLayout(addParent.NextSiblingOrUnkleLine);
		ResumeLayout();
		IsEdited = true;
	}
	
	void AddLogChildRecursive(Line original, Line cloneParent)
	{
		foreach( Line originalChild in original )
		{
			if( originalChild.IsDone == false && cloneParent.FirstOrDefault((Line l) => l.Text == originalChild.Text) == null )
			{
				Line cloneChild = originalChild.Clone();
				cloneParent.Add(cloneChild);
				if( originalChild.Count > 0 )
				{
					AddLogChildRecursive(originalChild, cloneChild);
				}
			}
		}
	}

	void CreateTitleLine()
	{
		// path_に対応するtitleLine_が存在していないので、さかのぼってCloneLineを生成する
		if( titleLine_ == null )
		{
			Line line = rootLine_;
			for( int i = 0; i < path_.Length; ++i )
			{
				string lineStr = path_[i];
				bool find = false;
				foreach( Line child in line )
				{
					if( child.TextWithoutHashTags == lineStr )
					{
						line = child;
						find = true;
						break;
					}
				}
				if( find == false )
				{
					Line originalLine = OwnerLogNote.TreeNote.Tree.GetLineFromPath(path_.GetPartialPath(i + 1));
					Line cloneLine = originalLine.Clone();
					cloneLine.IsDone = false;
					cloneLine.IsFolded = false;
					line.Add(cloneLine);
					line = cloneLine;
				}
			}
			SetTitleLine(line);
		}
	}

	public void RemoveLog(Line line)
	{
		SuspendLayout();
		// stack parents
		Stack<Line> originalLines = new Stack<Line>();
		originalLines.Push(line);
		Line parent = line;
		while( parent.Parent.Parent != null )
		{
			parent = parent.Parent;
			originalLines.Push(parent);
		}

		// search for clone line
		Line removeLine = null;
		Line searchParent = rootLine_;
		Line searchChild = originalLines.Pop();
		bool found = true;
		while( found && removeLine == null )
		{
			found = false;
			foreach( Line child in searchParent )
			{
				if( child.IsClone && child.Text == searchChild.TextWithoutHashTags )
				{
					found = true;
					searchParent = child;
					if( originalLines.Count > 0 )
					{
						searchChild = originalLines.Pop();
					}
					else
					{
						removeLine = child;
					}
					break;
				}
			}
		}

		// remove clone and its parents
		if( removeLine != null )
		{
			RequestLayout(removeLine.NextSiblingOrUnkleLine);
			Line removeParent = removeLine.Parent;
			bool hasDoneChild = RemoveLogChildRecursive(removeLine);
			
			if( hasDoneChild )
			{
				removeLine.IsDone = line.IsDone;
			}
			else
			{
				while( removeParent.Parent != null )
				{
					if( removeParent.Count == 0 && removeParent.IsDone == false )
					{
						// Childも無くてDoneもしてないなら、残さなくてよい
						Line nextParent = removeParent.Parent;
						RequestLayout(removeParent.NextSiblingOrUnkleLine);
						removeParent.Parent.Remove(removeParent);
						if( removeParent.IsTitleLine )
						{
							titleLine_ = null;
							titleLineObject_ = null;
						}
						removeParent = nextParent;
					}
					else
					{
						// Doneしてるやつの共通の親またはそれ自身がDoneなので残す
						break;
					}
				}
			}
		}

		if( rootLine_.Count == 0 )
		{
			rootLine_.Add(new Line(""));
		}
		ResumeLayout();
		IsEdited = true;
	}

	bool RemoveLogChildRecursive(Line removeLine)
	{
		bool hasDoneChild = false;
		List<Line> recursiveCheckLineList = new List<Line>();
		foreach( Line removeChild in removeLine )
		{
			if( removeChild.IsDone )
			{
				hasDoneChild = true;
			}
			else
			{
				recursiveCheckLineList.Add(removeChild);
			}
		}
		foreach( Line recursiveCheckLine in recursiveCheckLineList )
		{
			if( RemoveLogChildRecursive(recursiveCheckLine) )
			{
				hasDoneChild = true;
			}
		}

		if( hasDoneChild == false )
		{
			RequestLayout(removeLine.NextSiblingLine);
			removeLine.Parent.Remove(removeLine);
		}
		return hasDoneChild;
	}

	#endregion


	#region input override

	protected override void OnOverflowArrowInput(KeyCode key)
	{
		(ownerNote_ as LogNote).OnOverflowArrowInput(this, key);
	}

	protected override void OnCtrlDInput()
	{

	}

	#endregion


	#region layout

	public void UpdateLayoutElement()
	{
		if( layout_ == null )
		{
			layout_ = GetComponent<LayoutElement>();
			contentSizeFitter_ = GetComponent<ContentSizeFitter>();
		}

		if( suspendLayoutCount_ <= 0 && titleLine_ != null )
		{
			Line lastLine = titleLine_.LastVisibleLine;
			if( lastLine != null && lastLine.Field != null )
			{
				layout_.preferredHeight = Math.Max(GameContext.Config.MinLogTreeHeight, -(lastLine.TargetAbsolutePosition.y - this.transform.position.y) + GameContext.Config.HeightPerLine * 1.0f);
				contentSizeFitter_.SetLayoutVertical();
			}
		}
		else
		{
			layout_.preferredHeight = 0;
		}
	}

	#endregion


	#region file

	public void LoadLog(FileInfo file, DateTime date)
	{
		date_ = date;
		file_ = file;
		if( file_.Exists )
		{
			LoadFile(file_);
		}
		else
		{
			NewFile(file_);
		}
	}
	
	public override void SaveFile()
	{
		if( file_ == null )
		{
			if( OwnerLogNote != null )
			{
				file_ = new FileInfo(LogNote.ToFileName(OwnerLogNote.TreeNote, date_));
			}
			else
			{
				print("failed to save file.");
				return;
			}
		}
		if( file_.Exists == false )
		{
			if( rootLine_.Count == 0 || ( rootLine_.Count == 1 && rootLine_[0].Count == 0 && rootLine_[0].Text == "" ) )
			{
				// 何も書くことがなければファイル生成しない
				IsEdited = false;
				return;
			}
		}
		base.SaveFile();
	}

	public void OnDateChanged(DateTime date)
	{
		if( OwnerLogNote != null )
		{
			date_ = date;

			if( file_ != null )
			{
				file_.Delete();
				file_ = null;
			}

			SaveFile();

			OwnerLogNote.SetSortedIndex(this);
		}
	}

	#endregion
}
