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

	DateTime date_;
	LogNote ownerLogNote_;

	#endregion


	#region initialize

	protected override void Awake()
	{
		base.Awake();
		ownerLogNote_ = GetComponentInParent<LogNote>();
	}

	public void Initialize(ActionManager actionManager, GameObject heapParent)
	{
		actionManager_ = actionManager;
		heapParent_ = heapParent;
		
		actionManager_.ChainStarted += this.actionManager__ChainStarted;
		actionManager_.ChainEnded += this.actionManager__ChainEnded;
		actionManager_.Executed += this.actionManager__Executed;
	}

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
		ResumeLayout();
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
			RequestLayout(removeLine.NextSiblingLine);
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
						RequestLayout(removeParent.NextSiblingLine);
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


	#region layout

	protected override void UpdateLayoutElement()
	{
		if( suspendLayoutCount_ <= 0 && layout_ != null && rootLine_ != null && gameObject.activeInHierarchy )
		{
			Line lastLine = rootLine_.LastVisibleLine;
			if( lastLine != null && lastLine.Field != null )
			{
				layout_.preferredHeight = Math.Max(GameContext.Config.MinLogTreeHeight, -(lastLine.TargetAbsolutePosition.y - this.transform.position.y) + GameContext.Config.HeightPerLine * 1.0f);
				contentSizeFitter_.SetLayoutVertical();
				ownerLogNote_.UpdateLayoutElement();
			}
		}
	}

	#endregion


	#region file

	public void NewTree(DateTime date)
	{
		date_ = date;
		rootLine_ = new Line("new.dones");
		rootLine_.Add(new Line(""));
		rootLine_.Bind(this.gameObject);
		IsEdited = true;
	}

	public void Load(string filepath, DateTime date)
	{
		date_ = date;
		file_ = new FileInfo(filepath);
		LoadInternal();
	}
	
	public override void Save()
	{
		if( file_ == null )
		{
			file_ = new FileInfo(LogNote.ToFileName(ownerLogNote_.TreeNote.File, date_));
		}
		SaveInternal();
	}

	#endregion
}
