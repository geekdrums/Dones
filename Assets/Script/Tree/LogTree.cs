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

	LayoutElement layout_;
	ContentSizeFitter contentSizeFitter_;

	#endregion


	#region add / remove log

	public void AddLog(Line line)
	{
		SuspendLayout();
		if( rootLine_.Count == 1 && rootLine_[0].Text == "" )
		{
			rootLine_.Remove(rootLine_[0]);
		}

		// clone & stack parents
		Stack<Line> cloneLines = new Stack<Line>();
		Line cloneLine = line.Clone();
		cloneLines.Push(cloneLine);
		Line originalParent = line.Parent;
		while( originalParent.Parent != null )
		{
			Line cloneParent = originalParent.Clone();
			cloneParent.IsDone = false;
			cloneLines.Push(cloneParent);
			originalParent = originalParent.Parent;
		}

		// search for same parents
		Line addLine = cloneLines.Pop();
		Line addParent = rootLine_;
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
					if( cloneLines.Count > 0 )
					{
						addLine = cloneLines.Pop();
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
			while( cloneLines.Count > 0 )
			{
				addLine = cloneLines.Pop();
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
				if( child.IsClone && child.Text == searchChild.Text )
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
		OwnerLogNote.OnOverflowArrowInput(this, key);
	}

	protected override void OnCtrlDInput()
	{

	}

	protected override void OnCtrlSpaceInput()
	{
		if( focusedLine_ == null ) return;

		actionManager_.StartChain();
		foreach( Line line in GetSelectedOrFocusedLines() )
		{
			if( line.Text != "" && line.IsDone == false )
			{
				Line targetLine = line;
				Line parentLine = targetLine;

				bool isClone = targetLine.IsClone;
				if( isClone == false )
				{
					Stack<Line> targetAncestors = new Stack<Line>();
					parentLine = targetLine;
					while( parentLine.Parent != null )
					{
						targetAncestors.Push(parentLine);
						parentLine = parentLine.Parent;
					}

					Line originalLine = null;
					Line searchParent = OwnerLogNote.TreeNote.Tree.RootLine;
					Line searchChild = targetAncestors.Pop();
					bool found = true;
					while( found && originalLine == null )
					{
						found = false;
						foreach( Line child in searchParent )
						{
							if( child.Text == searchChild.Text )
							{
								found = true;
								searchParent = child;
								if( targetAncestors.Count > 0 )
								{
									searchChild = targetAncestors.Pop();
								}
								else
								{
									originalLine = child;
									isClone = true;
								}
								break;
							}
						}
					}
				}

				bool needUpdateIsClone = targetLine.IsClone == false && isClone;
				List<Line> updateCloneList = new List<Line>();
				parentLine = targetLine;
				while( needUpdateIsClone && parentLine.Parent != null )
				{
					if( parentLine.IsClone == false )
					{
						updateCloneList.Add(parentLine);
					}
					parentLine = parentLine.Parent;
				}

				actionManager_.Execute(new Action(
					execute: () =>
					{
						if( needUpdateIsClone )
						{
							foreach( Line updateLine in updateCloneList)
							{
								updateLine.IsClone = true;
								updateLine.Field.SetIsClone(true);
							}
						}
						targetLine.IsDone = true;
					},
					undo: () =>
					{
						if( needUpdateIsClone )
						{
							foreach( Line updateLine in updateCloneList )
							{
								updateLine.IsClone = false;
								updateLine.Field.SetIsClone(false);
							}
						}
						targetLine.IsDone = false;
					}
					));
			}
		}
		actionManager_.EndChain();
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

		if( suspendLayoutCount_ <= 0 && rootLine_ != null )
		{
			Line lastLine = rootLine_.LastVisibleLine;
			if( lastLine != null && lastLine.Field != null )
			{
				layout_.preferredHeight = Math.Max(GameContext.Config.MinLogTreeHeight, -(lastLine.TargetAbsolutePosition.y - this.transform.position.y) + GameContext.Config.HeightPerLine * 1.0f);
				contentSizeFitter_.SetLayoutVertical();
			}
		}
	}

	#endregion


	#region file

	public void NewLog(DateTime date)
	{
		date_ = date;
		NewFile("new");
	}

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
			NewFile(file_.Name);
		}
	}
	
	public override void SaveFile()
	{
		if( file_ == null )
		{
			file_ = new FileInfo(DiaryNoteBase.ToFileName(OwnerLogNote.TreeNote, date_));
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
		string folderName = OwnerLogNote.TreeNote.File.FullName.Replace(".dtml", ".dones");
		if( Directory.Exists(folderName) == false )
		{
			Directory.CreateDirectory(folderName);
		}
		base.SaveFile();
	}

	public void OnDateChanged(DateTime date)
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

	#endregion
}
