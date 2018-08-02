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
public class LogNote : Note
{
	public GameObject LogTreePrefab;
	public GameObject ThisWeekDateUIPrefab;
	public GameObject ThisYearDateUIPrefab;
	public GameObject DateUIPrefab;

	public bool IsEdited
	{
		get
		{
			foreach( LogTree logTree in logTrees_ )
			{
				if( logTree.IsEdited )
					return true;
			}
			return false;
		}
	}

	List<LogTree> logTrees_ = new List<LogTree>();
	List<DateUI> dateUIlist_ = new List<DateUI>();
	DateTime today_;
	DateTime endDate_;
	
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
	private float openRatio_ = 0.0f;

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
					OpenRatio = 0.0f;
				}
				OnAreaClosed();
			}
		}
	}
	private bool isOpended_ = false;

	private bool isDateUIlistUpdating_ = false;
	
	protected override void Awake()
	{
		base.Awake();
		today_ = DateTime.Now.Date;
		endDate_ = today_;
	}

	protected override void Update()
	{
		base.Update();

		if( Input.mouseScrollDelta.y < 0 )
		{
			if( isDateUIlistUpdating_ == false && treeNote_ != null && 
				scrollRect_.verticalScrollbar.isActiveAndEnabled && scrollRect_.verticalScrollbar.value <= 100.0f / layout_.preferredHeight )
			{
				LoadMore();
			}
		}
	}



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
	
	public void SubscribeKeyInput()
	{
		foreach( LogTree logTree in logTrees_ )
		{
			logTree.SubscribeKeyInput();
		}
	}

	public void OnOverflowArrowInput(LogTree tree, KeyCode key)
	{
		int index = logTrees_.IndexOf(tree);
		int nextIndex = index + 1;
		while( nextIndex < logTrees_.Count - 1 && logTrees_[nextIndex].gameObject.activeInHierarchy == false )
		{
			++nextIndex;
		}
		int prevIndex = index - 1;
		while( prevIndex > 0 && logTrees_[prevIndex].gameObject.activeInHierarchy == false )
		{
			--prevIndex;
		}
		switch( key )
		{
		case KeyCode.DownArrow:
			{
				Line next = (nextIndex < logTrees_.Count ? logTrees_[nextIndex].TitleLine[0] : null);
				if( next != null )
				{
					next.Field.IsFocused = true;
				}
			}
			break;
		case KeyCode.UpArrow:
			{
				Line prev = (prevIndex >= 0 ? logTrees_[prevIndex].TitleLine.LastVisibleLine : null);
				if( prev != null )
				{
					prev.Field.IsFocused = true;
				}
			}
			break;
		case KeyCode.RightArrow:
			{
				Line next = (nextIndex < logTrees_.Count ? logTrees_[nextIndex].TitleLine[0] : null);
				if( next != null )
				{
					next.Field.CaretPosision = 0;
					next.Field.IsFocused = true;
				}
			}
			break;
		case KeyCode.LeftArrow:
			{
				Line prev = (prevIndex >= 0 ? logTrees_[prevIndex].TitleLine.LastVisibleLine : null);
				if( prev != null )
				{
					prev.Field.CaretPosision = prev.TextLength;
					prev.Field.IsFocused = true;
				}
			}
			break;
		}
	}

	public void OnHomeEndInput(KeyCode key)
	{
		if( logTrees_.Count > 0 )
		{
			if( key == KeyCode.Home )
			{
				Line line = logTrees_[0].TitleLine[0];
				line.Field.IsFocused = true;
				logTrees_[0].OnFocused(line);
			}
			else if( key == KeyCode.End )
			{
				Line line = logTrees_[logTrees_.Count - 1].TitleLine.LastVisibleLine;
				line.Field.IsFocused = true;
				logTrees_[logTrees_.Count - 1].OnFocused(line);
			}
		}
	}

	#endregion


	#region layout

	public override void UpdateLayoutElement()
	{
		float preferredHeight = 0.0f;
		foreach( DateUI dateUI in dateUIlist_ )
		{
			preferredHeight += dateUI.UpdatePreferredHeight() + 5;
		}
		preferredHeight += 100;
		layout_.preferredHeight = preferredHeight;
		contentSizeFitter_.SetLayoutVertical();
	}

	public void OnFontSizeChanged()
	{
		foreach( LogTree logTree in logTrees_ )
		{
			logTree.RootLine.AdjustFontSizeRecursive(GameContext.Config.FontSize, GameContext.Config.HeightPerLine);
			logTree.UpdateLayoutElement();
		}
		if( gameObject.activeInHierarchy )
		{
			UpdateLayoutElement();
		}
	}

	#endregion


	#region events
	
	public void OnTreeNoteDeselected()
	{
		targetScrollValue_ = scrollRect_.verticalScrollbar.gameObject.activeInHierarchy ? scrollRect_.verticalScrollbar.value : 1.0f;
	}

	public override void SetNoteViewParam(NoteViewParam param)
	{
		scrollRect_.verticalScrollbar.value = param.LogNoteTargetScrollValue;
		StartCoroutine(SetNoteViewParamCoroutine(param));
	}

	public override void CacheNoteViewParam(NoteViewParam param)
	{
		param.LogNoteTargetScrollValue = isOpended_ && scrollRect_.verticalScrollbar.gameObject.activeInHierarchy ? scrollRect_.verticalScrollbar.value : 1.0f;
	}

	IEnumerator SetNoteViewParamCoroutine(NoteViewParam param)
	{
		isDateUIlistUpdating_ = true;
		foreach( DateUI dateUI in dateUIlist_ )
		{
			if( dateUI.Tree != null )
			{
				dateUI.Tree.SetPath(param.Path);
			}
			dateUI.UpdateLayout();
			dateUI.UpdatePreferredHeight();
			if( dateUI.Tree != null )
			{
				yield return new WaitForSeconds(GameContext.Config.LogNoteSetPathCoroutineInterval);
			}
		}
		UpdateLayoutElement();
		isDateUIlistUpdating_ = false;
	}

	public void OnAreaOpened()
	{
		if( (today_ - endDate_).Days < 7 )
		{
			LoadMore();
		}
		GameContext.Window.UpdateVerticalLayout();
	}

	public void OnAreaClosed()
	{
		GameContext.Window.UpdateVerticalLayout();
	}

	#endregion


	#region file

	public void SaveAllLogFilesToOneDirectory(DirectoryInfo directory)
	{
		string header = treeNote_.File.Name.Replace(".dtml", "");
		SortedList<DateTime, string> logFileList_ = new SortedList<DateTime, string>();

		string directoryName = String.Format("{0}/{1}.dones/", treeNote_.File.DirectoryName, treeNote_.File.Name.Replace(".dtml", ""));
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

		foreach( KeyValuePair<DateTime, string> pair in logFileList_ )
		{
			FileInfo file = new FileInfo(String.Format("{0}/{1}.dones", directory.FullName, pair.Key.ToString("yyyy-MM-dd")));
			
			StreamWriter writer = new StreamWriter(file.FullName, append: file.Exists);
			writer.WriteLine(header);

			StreamReader reader = new StreamReader(new FileInfo(pair.Value).OpenRead());
			string text = null;
			while( (text = reader.ReadLine()) != null )
			{
				writer.Write("	");
				writer.WriteLine(text);
			}

			writer.Flush();
			writer.Close();
		}
	}

	public void Initialize(TreeNote treeNote)
	{
		treeNote_ = treeNote;

		today_ = DateTime.Now.Date;
		endDate_ = today_;
		endDate_ = endDate_.AddDays(-1.0);
		DateUI dateUI = Instantiate(ThisWeekDateUIPrefab, this.transform).GetComponent<DateUI>();
		todayTree_ = LoadLogTree(today_, dateUI.transform, ToFileName(treeNote_, today_));
		dateUI.Set(todayTree_, today_, today_, ToColor(today_));
		logTrees_.Add(todayTree_);
		dateUIlist_.Add(dateUI);
	}

	public void LoadMore()
	{
		DateTime date = endDate_;
		int loadCount = GameContext.Config.LogLoadUnit;
		while( loadCount > 0 )
		{
			GameObject selectedDateUIPrefab = (today_ - date).Days < (int)today_.DayOfWeek ? ThisWeekDateUIPrefab : (today_.Year == date.Year ? ThisYearDateUIPrefab : DateUIPrefab);
			DateUI dateUI = Instantiate(selectedDateUIPrefab, this.transform).GetComponent<DateUI>();
			LogTree logTree = null;
			string filename = ToFileName(treeNote_, date);
			if( File.Exists(filename) )
			{
				logTree = LoadLogTree(date, dateUI.transform, filename);
				logTrees_.Add(logTree);
			}
			dateUI.Set(logTree, date, today_, ToColor(date));
			dateUIlist_.Add(dateUI);

			date = date.AddDays(-1.0);
			--loadCount;
		}
		endDate_ = date;
		UpdateLayoutElement();
	}


	protected LogTree LoadLogTree(DateTime date, Transform parent, string filename)
	{
		LogTree logTree = Instantiate(LogTreePrefab.gameObject, parent).GetComponent<LogTree>();
		logTree.Initialize(this, new ActionManagerProxy(actionManager_), heapManager_);
		logTree.LoadLog(new FileInfo(filename), date);
		logTree.SetPath(treeNote_.Tree.Path);
		logTree.SubscribeKeyInput();
		logTree.OnEdited += this.OnEdited;

		return logTree;
	}

	public override void OnEdited(object sender, EventArgs e)
	{
		treeNote_.OnEdited(sender, e);
		LogTree logTree = sender as LogTree;
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



	public static Color ToColor(DateTime date)
	{
		if( date.Date == DateTime.Now.Date ) return GameContext.Config.DoneColor;
		else if( date.DayOfWeek == DayOfWeek.Sunday ) return GameContext.Config.AccentColor;
		else if( date.DayOfWeek == DayOfWeek.Saturday ) return GameContext.Config.AccentColor;
		else return GameContext.Config.TextColor;
	}

	public static string ToFileName(TreeNote treeNote, DateTime date)
	{
		return String.Format("{0}/log/{1}.dones", treeNote.File.DirectoryName, date.ToString("yyyy-MM-dd"));
	}
}
