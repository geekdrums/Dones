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
	public enum OpenState
	{
		Minimize,
		Default,
		Maximize,
	}

	public GameObject LogTreePrefab;
	public GameObject DateUIPrefab;

	public UnityEngine.UI.Button MaximizeButton;
	public UnityEngine.UI.Button MinimizeButton;
	public UnityEngine.UI.Button ResetToDefaultButton;
	public Text TitleText;

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

	public OpenState State { get { return openState_; } }
	OpenState openState_ = OpenState.Default;

	public float Height { get { return scrollRect_.GetComponent<RectTransform>().sizeDelta.y; } }

	List<LogTree> logTrees_ = new List<LogTree>();
	List<DateUI> dateUIlist_ = new List<DateUI>();

	public DateTime Today { get { return today_; } }
	DateTime today_;
	DateTime endDate_;
	
	public LogTree TodayTree { get { return todayTree_; } }
	private LogTree todayTree_;

	public TreeNote TreeNote { get { return treeNote_; } }
	TreeNote treeNote_;

	private int suspendLayoutCount_;

	private bool isDateUIlistLoading_ = false;
	
	protected override void Awake()
	{
		base.Awake();
		today_ = DateTime.Now.Date;
		endDate_ = today_;
	}

	protected override void Update()
	{
		base.Update();

		if( openState_ == OpenState.Maximize && Input.mouseScrollDelta.y < 0 )
		{
			if( isDateUIlistLoading_ == false && treeNote_ != null && 
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

	public void Maximize()
	{
		openState_ = OpenState.Maximize;
		UpdateVerticalLayout();
		treeNote_.UpdateVerticalLayout();

		MaximizeButton.gameObject.SetActive(false);
		MinimizeButton.gameObject.SetActive(true);
		ResetToDefaultButton.gameObject.SetActive(true);

		float totalAreaHight = GameContext.Window.MainTabGroup.NoteAreaTransform.rect.height;
		while( layout_.preferredHeight < totalAreaHight )
		{
			LoadMore();
		}
	}

	public void Minimize()
	{
		openState_ = OpenState.Minimize;
		UpdateVerticalLayout();
		treeNote_.UpdateVerticalLayout();

		MaximizeButton.gameObject.SetActive(false);
		MinimizeButton.gameObject.SetActive(false);
		ResetToDefaultButton.gameObject.SetActive(false);
	}

	public void ResetToDefault()
	{
		openState_ = OpenState.Default;
		UpdateLayoutElement();
		UpdateVerticalLayout();
		treeNote_.UpdateVerticalLayout();

		MaximizeButton.gameObject.SetActive(true);
		MinimizeButton.gameObject.SetActive(true);
		ResetToDefaultButton.gameObject.SetActive(false);
	}

	public void ChangeOpenState()
	{
		switch( openState_ )
		{
			case OpenState.Default:
				Maximize();
				break;
			case OpenState.Minimize:
				ResetToDefault();
				break;
			case OpenState.Maximize:
				Minimize();
				break;
		}
	}

	public void UpdateVerticalLayout()
	{
		RectTransform logNoteTransform = scrollRect_.GetComponent<RectTransform>();

		float totalAreaHight = GameContext.Window.MainTabGroup.NoteAreaTransform.rect.height;
		float logNoteAreaHight = 0;
		switch( openState_ )
		{
			case OpenState.Minimize:
				logNoteAreaHight = 0;
				break;
			case OpenState.Default:
				logNoteAreaHight = dateUIlist_[0].UpdatePreferredHeight() + 5;
				break;
			case OpenState.Maximize:
				logNoteAreaHight = totalAreaHight;
				break;
		}
		logNoteTransform.anchoredPosition = new Vector2(logNoteTransform.anchoredPosition.x, -(totalAreaHight - logNoteAreaHight));
		logNoteTransform.sizeDelta = new Vector2(logNoteTransform.sizeDelta.x, logNoteAreaHight);

		CheckScrollbarEnabled();
	}

	protected void SuspendLayout()
	{
		++suspendLayoutCount_;
	}

	protected void ResumeLayout()
	{
		--suspendLayoutCount_;
		if( suspendLayoutCount_ <= 0 )
		{
			suspendLayoutCount_ = 0;
			UpdateLayoutElement();
		}
	}

	public override void UpdateLayoutElement()
	{
		if( suspendLayoutCount_ > 0 )
		{
			return;
		}

		float preferredHeight = 0.0f;
		if( openState_ == OpenState.Default )
		{
			preferredHeight += dateUIlist_[0].UpdatePreferredHeight() + 5;
		}
		else
		{
			foreach( DateUI dateUI in dateUIlist_ )
			{
				preferredHeight += dateUI.UpdatePreferredHeight() + 5;
			}
			preferredHeight += 100;
		}
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

	public override void SetNoteViewParam(NoteViewParam param)
	{
		scrollRect_.verticalScrollbar.value = param.LogNoteTargetScrollValue;
		StartCoroutine(SetNoteViewParamCoroutine(param));
	}

	public void UpdateTitleLine(TreePath path)
	{
		TitleText.text = ( path.Length > 0 ? path[path.Length - 1] : "Home" ) + ".dones";
	}

	public override void CacheNoteViewParam(NoteViewParam param)
	{
		if( openState_ == OpenState.Maximize && scrollRect_.verticalScrollbar.gameObject.activeInHierarchy )
		{
			param.LogNoteTargetScrollValue = scrollRect_.verticalScrollbar.value;
		}
		else
		{
			param.LogNoteTargetScrollValue = 1.0f;
		}
	}

	IEnumerator SetNoteViewParamCoroutine(NoteViewParam param)
	{
		isDateUIlistLoading_ = true;
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
		isDateUIlistLoading_ = false;
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

		SuspendLayout();
		{
			DateUI dateUI = Instantiate(DateUIPrefab, this.transform).GetComponent<DateUI>();
			todayTree_ = LoadLogTree(today_, dateUI.transform, ToFileName(treeNote_, today_));
			dateUI.Set(todayTree_, today_, today_, ToColor(today_));
			logTrees_.Add(todayTree_);
			dateUIlist_.Add(dateUI);
		}
		ResumeLayout();

		ResetToDefault();
	}

	public void LoadMore()
	{
		SuspendLayout();
		DateTime date = endDate_;
		int loadCount = GameContext.Config.LogLoadUnit;
		while( loadCount > 0 )
		{
			// 日にちの近さによってUI変える仕様、逆にわかりにくいのでボツ
			// GameObject selectedDateUIPrefab = (today_ - date).Days < (int)today_.DayOfWeek ? ThisWeekDateUIPrefab : (today_.Year == date.Year ? ThisYearDateUIPrefab : DateUIPrefab);
			DateUI dateUI = Instantiate(DateUIPrefab, this.transform).GetComponent<DateUI>();
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
		ResumeLayout();
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
		if( openState_ == OpenState.Default && logTree == todayTree_ )
		{
			UpdateVerticalLayout();
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
