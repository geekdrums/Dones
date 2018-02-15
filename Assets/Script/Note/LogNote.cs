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

	SortedList<DateTime, string> logFileList_ = new SortedList<DateTime, string>();

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
		LogTabButton.Text = TitleText;

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
		if( endDate_ == today_ )
		{
			LoadMore();
		}
		GameContext.Window.UpdateVerticalLayout();
		if( treeNote_.IsActive )
		{
			LogTabButton.Text = TitleText;
		}
	}

	public void OnAreaClosed()
	{
		GameContext.Window.UpdateVerticalLayout();
	}

	#endregion


	#region file

	public void Initialize(TreeNote treeNote)
	{
		treeNote_ = treeNote;

		string directoryName = ToDirectoryName(treeNote_);
		if( Directory.Exists(directoryName) == false )
		{
			Directory.CreateDirectory(directoryName);
		}
		string header = treeNote_.File.Name.Replace(".dtml", "");
		foreach( string path in Directory.GetFiles(directoryName) )
		{
			// path would be like "dones-2017-01-01.dtml"
			if( Path.GetExtension(path) == ".dtml" )
			{
				// splitPath would be like "[dones][2017][01][01]"
				string[] splitPath = Path.GetFileNameWithoutExtension(path).Split('-');
				int year, month, day;
				if( splitPath.Length == 4 && splitPath[0] == header && int.TryParse(splitPath[1], out year) && int.TryParse(splitPath[2], out month) && int.TryParse(splitPath[3], out day) )
				{
					DateTime date = new DateTime(year, month, day);
					logFileList_.Add(date, path);
				}
			}
		}

		today_ = DateTime.Now.Date;
		endDate_ = today_;
		todayTree_ = LoadLogTree(today_, ToFileName(treeNote_, today_));
		if( logFileList_.ContainsKey(today_.Date) )
		{
			logFileList_.Remove(today_.Date);
		}
	}

	public override void LoadMore()
	{
		if( treeNote_.File == null ) return;
		
		int loadCount = GameContext.Config.LogLoadUnit;
		while( logFileList_.Count > 0 && loadCount > 0 )
		{
			DateTime date = logFileList_.Last().Key;
			LoadLogTree(date, logFileList_[date]);
			--loadCount;
			logFileList_.Remove(date);
			endDate_ = date;
		}
		endLoad_ = logFileList_.Count == 0;
		UpdateLayoutElement();
	}

	public void AddDate(DateTime date)
	{
		LogTree logTree = LoadLogTree(date, ToFileName(treeNote_, date));
		SetSortedIndex(logTree);
	}

	protected LogTree LoadLogTree(DateTime date, string filename)
	{
		DateUI dateUI = Instantiate(DateUIPrefab.gameObject, this.transform).GetComponent<DateUI>();
		LogTree logTree = Instantiate(LogTreePrefab.gameObject, dateUI.transform).GetComponent<LogTree>();
		dateUI.Set(date, ToColor(date));
		dateUI.SetEnableAddDateButtton(File.Exists(ToFileName(treeNote_, date.AddDays(-1.0))) == false);
		logTree.Initialize(this, new ActionManagerProxy(actionManager_), heapManager_);
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
