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
	public DateTime Date { get; private set; }

	LogNote ownerLogNote_;

	protected override void Awake()
	{
		base.Awake();
		ownerLogNote_ = GetComponentInParent<LogNote>();
	}


	#region add / remove log

	public void AddLog(Line line)
	{
		// clone parents
		Line cloneLine = line.Clone();
		Line addLine = cloneLine;
		Line originalParent = line.Parent;
		while( originalParent.Parent != null )
		{
			Line cloneParent = originalParent.Clone();
			cloneParent.Add(addLine);
			addLine = cloneParent;
			originalParent = originalParent.Parent;
		}

		// search for same parents
		Line addParent = rootLine_;
		Line foundChild = rootLine_;
		while( foundChild != null )
		{
			bool found = false;
			addParent = foundChild;
			foreach( Line child in addParent )
			{
				if( child.IsClone && child.Text == addLine.Text )
				{
					found = true;
					foundChild = child;
					break;
				}
			}

			if( found )
			{
				if( addLine.Count > 0 )
				{
					// 同じ親が見つかったので、追加するのはこの子供以降でOK
					addLine = addLine[0];
				}
				else
				{
					// 既に同じLineが親を含めて全部あった
					foundChild.IsDone = addLine.IsDone;
					return;
				}
			}
			else
			{
				break;
			}
		}

		// add clone and its children
		addParent.Add(addLine);
		line.CloneRecursive(cloneLine);
	}

	public void RemoveLog(Line line)
	{
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
		Line removeLine = rootLine_;
		Line searchChild = originalLines.Pop();
		while( originalLines.Count > 0 )
		{
			bool found = false;
			foreach( Line child in removeLine )
			{
				if( child.IsClone && child.Text == searchChild.Text )
				{
					found = true;
					removeLine = child;
					searchChild = originalLines.Pop();
					break;
				}
			}

			if( found == false )
			{
				removeLine = null;
				break;
			}
		}

		// remove clone and its parents
		if( removeLine != null )
		{
			Line cloneParent = removeLine.Parent;
			removeLine.Parent.Remove(removeLine);
			while( cloneParent.Parent.Parent != null )
			{
				cloneParent = cloneParent.Parent;
				if( cloneParent.Count == 1 )
				{
					// それしか無いならCloneしてる意味が無いので消す
					cloneParent.Remove(cloneParent[0]);
				}
				else
				{
					// 他にもあるなら共通の親として使われているので残す
					break;
				}
			}
		}
	}

	#endregion



	#region file

	public void NewTree(DateTime date)
	{
		Date = date;
		rootLine_ = new Line(file_.Name);
		rootLine_.Bind(this.gameObject);
		rootLine_.Add(new Line(""));
		IsEdited = true;
	}

	public void Load(string filepath, DateTime date)
	{
		Date = date;
		file_ = new FileInfo(filepath);
		LoadInternal();
	}
	
	public override void Save()
	{
		if( file_ == null )
		{
			file_ = new FileInfo(LogNote.ToFileName(ownerLogNote_.TreeNote.File, Date));
		}
		SaveInternal();
	}

	#endregion
}
