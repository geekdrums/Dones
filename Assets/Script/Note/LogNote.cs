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

// Window - [ LogNote ] - LogTree - Line
public class LogNote : DiaryNoteBase
{
	public GameObject LogTreePrefab;
	public GameObject DateUIPrefab;
	public DateUI EndDateUI;

	public LogTree TodayTree { get { return todayTree_; } }
	private LogTree todayTree_;

	public TreeNote TreeNote { get { return treeNote_; } }
	TreeNote treeNote_;

	public LogNoteTabButton LogTabButton { get { return GameContext.Window.LogTabButton; } }

	public float OpenRatio
	{
		get { return openRatio_; }
		set { openRatio_ = value; }
	}
	private float openRatio_ = 0.5f;

	public bool IsFullArea { get { return isOpended_ && openRatio_ >= 1.0f; } }

	public bool IsOpended
	{
		get { return isOpended_; }
		set
		{
			isOpended_ = value;
			if( value )
			{
				if( OpenRatio <= 0.0f )
				{
					OpenRatio = 0.5f;
				}
				OnAreaOpened();
			}
			else
			{
				if( OpenRatio >= 1.0f )
				{
					OpenRatio = 0.5f;
				}
				OnAreaClosed();
			}
		}
	}
	private bool isOpended_ = false;

	public override string TitleText { get { return treeNote_ != null ? treeNote_.Tree.TitleText.Replace(".dtml", ".dones") : ""; } }
	

	#region input

	public void OnDoneChanged(Line line)
	{
		if( TodayTree != null )
		{
			if( line.IsDone )
			{
				TodayTree.AddLog(line);
			}
			else
			{
				TodayTree.RemoveLog(line);
			}
		}
	}

	#endregion


	#region layout

	public override void UpdateLayoutElement()
	{
		float preferredHeight = 0.0f;
		foreach( LogTree logTree in logTrees_ )
		{
			logTree.UpdateLayoutElement();
			preferredHeight += logTree.GetComponent<LayoutElement>().preferredHeight + 5;
			if( logTree.GetComponentInParent<DateUI>() != null )
			{
				logTree.GetComponentInParent<DateUI>().GetComponent<LayoutElement>().preferredHeight = logTree.GetComponent<LayoutElement>().preferredHeight;
				logTree.GetComponentInParent<DateUI>().GetComponent<ContentSizeFitter>().SetLayoutVertical();
			}
		}
		preferredHeight += 100;
		layout_.preferredHeight = preferredHeight;
		contentSizeFitter_.SetLayoutVertical();
	}

	#endregion


	#region events

	public void OnTreeNoteSelected()
	{
		scrollRect_.verticalScrollbar.value = targetScrollValue_;

		foreach( LogTree logTree in logTrees_ )
		{
			if( EditedLogTreeDict.ContainsKey(logTree.File.FullName) )
			{
				logTree.ReloadFile();
				EditedLogTreeDict.Remove(logTree.File.FullName);
			}
		}
	}

	public void OnTreeNoteDeselected()
	{
		targetScrollValue_ = scrollRect_.verticalScrollbar.gameObject.activeInHierarchy ? scrollRect_.verticalScrollbar.value : 1.0f;
	}

	public void OnAreaOpened()
	{
		LoadUntil(today_.AddDays(-LoadDateCount));
		GameContext.Window.UpdateVerticalLayout();
	}

	public void OnAreaClosed()
	{
		GameContext.Window.UpdateVerticalLayout();
	}

	#endregion


	#region file

	public void LoadToday(TreeNote treeNote)
	{
		treeNote_ = treeNote;

		today_ = DateTime.Now.Date;
		endDate_ = today_;
		
		LoadLog(today_, ToFileName(treeNote_, today_));

		LogTabButton.Text = TitleText;
	}

	public override void LoadUntil(DateTime endDate)
	{
		if( treeNote_.File == null ) return;

		DateTime date = endDate_;
		DateUI lastDateUI = logTrees_.Count == 0 ? null : logTrees_[logTrees_.Count - 1].GetComponentInChildren<DateUI>();
		while( date > endDate )
		{
			date = date.AddDays(-1.0);
			string filename = ToFileName(treeNote_, date);
			bool exist = File.Exists(filename);
			if( lastDateUI != null )
			{
				lastDateUI.SetEnableAddDateButtton(exist == false);
			}
			if( exist )
			{
				LogTree logTree = LoadLog(date, filename);
				lastDateUI = logTree.GetComponentInParent<DateUI>();
			}
			else
			{
				lastDateUI = null;
			}
		}
		if( lastDateUI != null )
		{
			lastDateUI.SetEnableAddDateButtton(false);
		}
		endDate_ = endDate;
		EndDateUI.Set(endDate.AddDays(-1), GameContext.Config.CommentTextColor);
		EndDateUI.transform.parent.SetAsLastSibling();
		UpdateLayoutElement();
	}

	public void AddDate(DateTime date)
	{
		LogTree logTree = LoadLog(date, ToFileName(treeNote_, date));
		logTree.GetComponentInParent<DateUI>().SetEnableAddDateButtton(treeNote_.File == null || File.Exists(ToFileName(treeNote_, date.AddDays(-1.0))) == false);
		SetSortedIndex(logTree);
	}

	protected LogTree LoadLog(DateTime date, string filename)
	{
		DateUI dateUI = Instantiate(DateUIPrefab.gameObject, this.transform).GetComponent<DateUI>();
		LogTree logTree = Instantiate(LogTreePrefab.gameObject, dateUI.transform).GetComponent<LogTree>();
		dateUI.Set(date, ToColor(date));
		logTree.Initialize(this, new ActionManagerProxy(actionManager_), heapFields_);
		logTree.LoadLog(new FileInfo(filename), date);
		logTree.SubscribeKeyInput();
		logTree.OnEdited += this.OnEdited;
		logTrees_.Add(logTree);
		return logTree;
	}

	public override void OnEdited(object sender, EventArgs e)
	{
		treeNote_.OnEdited(sender, e);
		LogTree logTree = sender as LogTree;
		if( EditedLogTreeDict.ContainsValue(logTree) == false )
		{
			EditedLogTreeDict.Add(logTree.File.FullName, logTree);
		}
	}

	public void SetSortedIndex(LogTree logTree)
	{
		if( logTrees_.Contains(logTree) )
		{
			logTrees_.Remove(logTree);
		}

		int insertIndex = logTrees_.Count;
		for( int i = 0; i < logTrees_.Count; ++i )
		{
			if( logTrees_[i].Date < logTree.Date )
			{
				insertIndex = i;
				break;
			}
		}
		logTrees_.Insert(insertIndex, logTree);
		logTree.GetComponentInParent<DateUI>().transform.SetSiblingIndex(insertIndex);
		UpdateLayoutElement();
	}

	public void SaveLog()
	{
		if( treeNote_.File == null )
		{
			Debug.LogError(treeNote_.ToString() + " doesn't have target File!");
			return;
		}

		foreach( LogTree logTree in logTrees_ )
		{
			if( logTree.IsEdited )
			{
				logTree.SaveFile();
			}
		}

		saveRequestedTrees_.Clear();
	}
	
	public void ReloadLog()
	{
		foreach( LogTree logTree in logTrees_ )
		{
			logTree.ReloadFile();
		}
		UpdateLayoutElement();
	}

	#endregion
}
