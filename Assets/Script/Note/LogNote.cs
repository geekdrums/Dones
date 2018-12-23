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
		Default,
		Minimize,
		Maximize,
	}

	#region editor params

	public GameObject LogTreePrefab;
	public GameObject DateUIPrefab;
	public GameObject DoneMark;

	public UnityEngine.UI.Button MaximizeButton;
	public UnityEngine.UI.Button MinimizeButton;
	public UnityEngine.UI.Button ResetToDefaultButton;

	public Text TitleText;
	public UIMidairPrimitive TitleArrow;

	#endregion


	#region properties

	public TreeNote TreeNote { get { return treeNote_; } set { treeNote_ = value; } }
	public OpenState State { get { return openState_; } }
	public IEnumerable<LogTree> LogTrees { get { return logTrees_.AsEnumerable<LogTree>(); } }
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

	#endregion


	#region private params

	TreeNote treeNote_;
	LogTree todayTree_;
	DateUI todayUI_;

	DateTime today_;
	DateTime endDate_;

	OpenState openState_ = OpenState.Default;
	OpenState oldOpenState_ = OpenState.Default;
	
	List<LogTree> logTrees_ = new List<LogTree>();
	List<DateUI> dateUIlist_ = new List<DateUI>();

	List<UIMidairPrimitive> doneMarks_ = new List<UIMidairPrimitive>();
	int numTodayDones_ = 0;

	int suspendLayoutCount_;
	bool isDateUIlistLoading_ = false;

	#endregion



	#region unity functions

	protected override void Awake()
	{
		base.Awake();
		today_ = DateTime.Now.Date;
		endDate_ = today_;
		doneMarks_.Add(DoneMark.GetComponent<UIMidairPrimitive>());
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

	#endregion
	

	#region input

	public void OnDoneChanged(Line line)
	{
		if( todayTree_ != null )
		{
			if( line.IsDone )
			{
				todayTree_.AddLog(line);
			}
			else
			{
				todayTree_.RemoveLog(line);
			}
			UpdateDoneCount();
		}
	}

	void UpdateDoneCount()
	{
		int oldNumDones = numTodayDones_;
		numTodayDones_ = todayTree_.TitleLine != null ? todayTree_.TitleLine.GetNumDoneLines() : 0;
		int l = Math.Max(numTodayDones_, doneMarks_.Count);
		for( int i = 0; i < l; ++i )
		{
			if( i < numTodayDones_ )
			{
				while( i >= doneMarks_.Count )
				{
					doneMarks_.Add(Instantiate(DoneMark, DoneMark.transform.parent).GetComponent<UIMidairPrimitive>());
				}
				doneMarks_[i].gameObject.SetActive(true);
				if( oldNumDones < numTodayDones_ && i == 0 )
				{
					doneMarks_[i].transform.localScale = Vector3.zero;
					AnimManager.AddAnim(doneMarks_[i], Vector3.one * 1.5f, ParamType.Scale, AnimType.Time, 0.1f);
					AnimManager.AddAnim(doneMarks_[i], Vector3.one * 1.0f, ParamType.Scale, AnimType.Time, 0.05f, 0.1f);
				}
				else
				{
					doneMarks_[i].transform.localScale = Vector3.one;
				}
			}
			else
			{
				if( i < doneMarks_.Count )
				{
					doneMarks_[i].gameObject.SetActive(false);
				}
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
		SetOpenState(OpenState.Maximize);
		UpdateVerticalLayout();
		treeNote_.UpdateVerticalLayout();

		MaximizeButton.gameObject.SetActive(false);
		MinimizeButton.gameObject.SetActive(true);
		MinimizeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);
		ResetToDefaultButton.gameObject.SetActive(true);
		ResetToDefaultButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(25, 0);
		DoneMark.transform.parent.gameObject.SetActive(false);

		float totalAreaHight = GameContext.Window.MainTabGroup.NoteAreaTransform.rect.height;
		while( layout_.preferredHeight < totalAreaHight )
		{
			LoadMore();
			UpdateLayoutElement();
		}
		TitleArrow.Angle = 180;
		TitleArrow.SetColor(GameContext.Config.ToggleOpenedColor);
		TitleArrow.RecalculatePolygon();
	}

	public void Minimize()
	{
		SetOpenState(OpenState.Minimize);
		UpdateVerticalLayout();
		treeNote_.UpdateVerticalLayout();
		UpdateDoneCount();

		MaximizeButton.gameObject.SetActive(true);
		MaximizeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(25, 0);
		ResetToDefaultButton.gameObject.SetActive(true);
		ResetToDefaultButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);
		MinimizeButton.gameObject.SetActive(false);
		DoneMark.transform.parent.gameObject.SetActive(true);
		TitleArrow.Angle = -90;
		TitleArrow.SetColor(GameContext.Config.DoneColor);
		TitleArrow.RecalculatePolygon();
	}

	public void ResetToDefault()
	{
		SetOpenState(OpenState.Default);
		UpdateLayoutElement();
		UpdateVerticalLayout();
		treeNote_.UpdateVerticalLayout();

		MaximizeButton.gameObject.SetActive(true);
		MaximizeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(25, 0);
		MinimizeButton.gameObject.SetActive(true);
		MinimizeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);
		ResetToDefaultButton.gameObject.SetActive(false);
		DoneMark.transform.parent.gameObject.SetActive(false);
		TitleArrow.Angle = 180;
		TitleArrow.SetColor(GameContext.Config.ToggleOpenedColor);
		TitleArrow.RecalculatePolygon();
	}

	private void SetOpenState(OpenState state)
	{
		oldOpenState_ = openState_;
		openState_ = state;
	}

	public void BackToLastOpenState()
	{
		switch( oldOpenState_ )
		{
			case OpenState.Default:
				ResetToDefault();
				break;
			case OpenState.Minimize:
				Minimize();
				break;
			case OpenState.Maximize:
				Maximize();
				break;
		}
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
		float totalAreaHight = GameContext.Window.MainTabGroup.NoteAreaTransform.rect.height;
		float logNoteAreaHight = 0;
		switch( openState_ )
		{
			case OpenState.Minimize:
				logNoteAreaHight = 0;
				break;
			case OpenState.Default:
				logNoteAreaHight = Math.Min(totalAreaHight / 2, todayUI_.UpdateLayoutElement() + 5);
				break;
			case OpenState.Maximize:
				logNoteAreaHight = totalAreaHight - 30;
				break;
		}
		scrollRectTransform_.anchoredPosition = new Vector2(scrollRectTransform_.anchoredPosition.x, -(totalAreaHight - logNoteAreaHight));
		scrollRectTransform_.sizeDelta = new Vector2(scrollRectTransform_.sizeDelta.x, logNoteAreaHight);

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
			preferredHeight += todayUI_.UpdateLayoutElement() + 5;
		}
		else
		{
			foreach( DateUI dateUI in dateUIlist_ )
			{
				preferredHeight += dateUI.UpdateLayoutElement() + 5;
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
		actionManager_.SetTitleLine(treeNote_.Tree.TitleLine);

		if( todayTree_ != null )
		{
			todayTree_.SetPath(param.Path);
			todayUI_.OnTreeTitleLineChanged();
		}
		todayUI_.UpdateLayoutElement();
		UpdateDoneCount();

		StartCoroutine(SetNoteViewParamCoroutine(param));
	}

	public void UpdateTitleText(TreePath path)
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
		int count = GameContext.Config.LogNoteSetPathCoroutineCount;
		for( int i = 1; i < dateUIlist_.Count; ++i )
		{
			DateUI dateUI = dateUIlist_[i];
			if( dateUI.Tree != null )
			{
				dateUI.Tree.SetPath(param.Path);
				dateUI.OnTreeTitleLineChanged();
			}
			dateUI.UpdateLayoutElement();
			if( dateUI.Tree != null )
			{
				if( count > 0 )
				{
					--count;
				}
				else
				{
					yield return new WaitForSeconds(GameContext.Config.LogNoteSetPathCoroutineInterval);
				}
			}
		}

		UpdateLayoutElement();
		if( openState_ == OpenState.Maximize )
		{
			float totalAreaHight = GameContext.Window.MainTabGroup.NoteAreaTransform.rect.height;
			while( layout_.preferredHeight < totalAreaHight )
			{
				LoadMore();
			}
		}

		isDateUIlistLoading_ = false;
	}

	#endregion


	#region file

	public void SaveAllLogFilesToOneDirectory(DirectoryInfo directory, string title)
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
			if( title != "" )
			{
				writer.WriteLine(title);
			}

			StreamReader reader = new StreamReader(new FileInfo(pair.Value).OpenRead());
			string text = null;
			while( (text = reader.ReadLine()) != null )
			{
				if( title != "" )
				{
					writer.Write("	");
				}
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
			todayUI_ = Instantiate(DateUIPrefab, this.transform).GetComponent<DateUI>();
			todayTree_ = LoadLogTree(today_, todayUI_.transform, ToFileName(treeNote_, today_));
			todayUI_.Set(todayTree_, today_, today_, ToColor(today_));
			logTrees_.Add(todayTree_);
			dateUIlist_.Add(todayUI_);
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


	public LogTree LoadLogTree(DateTime date, Transform parent, string filename)
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
		if( logTree == todayTree_ )
		{
			if( openState_ == OpenState.Default )
			{
				UpdateVerticalLayout();
				treeNote_.UpdateVerticalLayout();
			}
			else if( openState_ == OpenState.Minimize )
			{
				UpdateDoneCount();
			}
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
		TreePath path = treeNote_.Tree.Path;
		foreach( LogTree logTree in logTrees_ )
		{
			logTree.ReloadFile();
			logTree.SetPath(path);
		}
		actionManager_.SetTitleLine(treeNote_.Tree.TitleLine);
		UpdateTitleText(path);
		UpdateDoneCount();
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
