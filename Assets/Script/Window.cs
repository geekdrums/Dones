#if UNITY_STANDALONE_WIN
//#define HOOK_WNDPROC
#endif
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif

// [ Window ] - Tree - Line
public class Window : MonoBehaviour, IEnumerable<Tree>
{
	#region editor params

	public TreeNote TreeNotePrefab;
	public TabButton TabButtonPrefab;
	public LogNote LogNotePrefab;
	public LogTabButton LogTabButtonPrefab;

	public GameObject TreeParent;
	public GameObject LogTreeParent;
	public GameObject TabParent;
	public GameObject LogTabParent;
	public RectTransform NoteAreaTransform;
	public RectTransform TreeNoteTransform;
	public RectTransform LogNoteTransform;
	public MenuButton FileMenu;
	public GameObject RecentFilesSubMenu;
	public ShortLineList LineList;
	public ModalDialog ModalDialog;

	public float DesiredTabWidth = 200.0f;

	#endregion


	#region params

	TreeNote activeNote_;
	List<TreeNote> trees_ = new List<TreeNote>();
	FileInfo settingFile_;
	FileInfo lineListFile_;

	string initialDirectory_;

	float currentScreenWidth_;
	float currentScreenHeight_;
	float currentTabWidth_;

	Vector2 initialLogTabOffsetMax_;
	List<string> recentOpenedFiles_ = new List<string>();
	Stack<string> recentClosedFiles_ = new Stack<string>();
	int numRecentFilesMenu = 0;
	bool saveConfirmed_ = false;

	public float HeaderWidth { get { return LineList.Width + 5.0f; } }

	#endregion


	#region unity functions

	void Awake()
	{
		GameContext.Window = this;
		currentScreenWidth_ = UnityEngine.Screen.width;
		currentScreenHeight_ = UnityEngine.Screen.height;
		currentTabWidth_ = DesiredTabWidth;

#if HOOK_WNDPROC
		InitWndProc();
#endif
	}

	// Use this for initialization
	void Start ()
	{
		settingFile_ = new FileInfo(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/Dones/settings.txt");
		lineListFile_ = new FileInfo(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/Dones/linelist.txt");
		numRecentFilesMenu = RecentFilesSubMenu.GetComponentsInChildren<UnityEngine.UI.Button>().Length;
		StartCoroutine(InitialLoadCoroutine());
	}

	IEnumerator InitialLoadCoroutine()
	{
		// Editorではいいんだけど、アプリ版はこうしないとScrollがバグってその後一切操作できなくなる。。
		yield return new WaitForEndOfFrame();
		LoadSettings();
		LoadLineList();
		UpdateLogTabLayout();
	}

	// Update is called once per frame
	void Update()
	{
		bool ctrl = Input.GetKey(KeyCode.LeftControl);
		bool shift = Input.GetKey(KeyCode.LeftShift);
		bool alt = Input.GetKey(KeyCode.LeftAlt);
		bool ctrlOnly = ctrl && !alt && !shift;

		if( ctrlOnly )
		{
			if( Input.GetKeyDown(KeyCode.O) )
			{
				OpenFile();
			}
			else if( Input.GetKeyDown(KeyCode.N) )
			{
				NewFile();
			}
			else if( Input.GetKeyDown(KeyCode.LeftArrow) && Input.GetKey(KeyCode.LeftCommand) )
			{
				int index = trees_.IndexOf(activeNote_);
				if( index > 0 )
				{
					trees_[index - 1].IsActive = true;
				}
			}
			else if( Input.GetKeyDown(KeyCode.RightArrow) && Input.GetKey(KeyCode.LeftCommand) )
			{
				int index = trees_.IndexOf(activeNote_);
				if( index < trees_.Count - 1 )
				{
					trees_[index + 1].IsActive = true;
				}
			}
		}
		if( ctrl && shift )
		{
			if( Input.GetKeyDown(KeyCode.T) && recentClosedFiles_.Count > 0 )
			{
				LoadTree(recentClosedFiles_.Pop(), true);
			}
		}
		if( Input.GetKeyDown(KeyCode.F5) && activeNote_.File != null )
		{
			activeNote_.Reload();
		}

		if(	currentScreenWidth_ != UnityEngine.Screen.width )
		{
			UpdateTabLayout();
			currentScreenWidth_ = UnityEngine.Screen.width;
		}
		if( currentScreenHeight_ != UnityEngine.Screen.height )
		{
			UpdateLogTabLayout();
			currentScreenHeight_ = UnityEngine.Screen.height;
		}
	}

	void OnApplicationQuit()
	{
#if !UNITY_EDITOR
		if( saveConfirmed_ == false )
		{
			saveConfirmed_ = true;
			foreach( Tree tree in trees_ )
			{
				if( tree.IsEdited )
				{
					GameContext.Window.ModalDialog.Show("ファイルへの変更を保存しますか？", this.CloseConfirmCallback);
					UnityEngine.Application.CancelQuit();
					return;
				}
			}
		}
#endif

		SaveSettings();
		SaveLineList();

#if HOOK_WNDPROC
		TermWndProc();
#endif
	}

	void CloseConfirmCallback(ModalDialog.DialogResult result)
	{
		switch( result )
		{
		case ModalDialog.DialogResult.Yes:
			foreach( Tree tree in trees_ )
			{
				if( tree.IsEdited )
				{
					tree.Save();
				}
			}
			UnityEngine.Application.Quit();
			break;
		case ModalDialog.DialogResult.No:
			UnityEngine.Application.Quit();
			break;
		case ModalDialog.DialogResult.Cancel:
			saveConfirmed_ = false;
			break;
		}
	}

	#endregion


	#region file menu

	public void OpenFile()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Filter = "dones file (*.dtml)|*.dtml";
		openFileDialog.Multiselect = true;
		if( initialDirectory_ != null )
			openFileDialog.InitialDirectory = initialDirectory_;
		DialogResult dialogResult = openFileDialog.ShowDialog();
		if( dialogResult == DialogResult.OK )
		{
			bool isActive = true;
			foreach( string path in openFileDialog.FileNames )
			{
				if( path.EndsWith(".dtml") && File.Exists(path) )
				{
					LoadTree(path, isActive);
					isActive = false;
				}
			}

			if( activeNote_ != null )
			{
				initialDirectory_ = activeNote_.File.Directory.FullName;
			}
		}

		FileMenu.Close();
	}

	public void NewFile()
	{
		TreeNote treeNote = Instantiate(TreeNotePrefab.gameObject, TreeParent.transform).GetComponent<TreeNote>();
		TabButton tab = Instantiate(TabButtonPrefab.gameObject, TabParent.transform).GetComponent<TabButton>();
		LogNote logNote = Instantiate(LogNotePrefab.gameObject, LogTreeParent.transform).GetComponent<LogNote>();
		LogTabButton logtab = Instantiate(LogTabButtonPrefab.gameObject, LogTabParent.transform).GetComponent<LogTabButton>();
		treeNote.NewFile(tab, logNote);
		logNote.LoadToday(treeNote, logtab);
		OnTreeCreated(treeNote);
		treeNote.IsActive = true;

		FileMenu.Close();
	}

	public void Save()
	{
		if( activeNote_ != null )
		{
			activeNote_.Save();
		}

		FileMenu.Close();
	}

	public void SaveAs()
	{
		if( activeNote_ != null )
		{
			activeNote_.SaveAs();
		}

		FileMenu.Close();
	}

	public void RecentFiles()
	{
		if( recentOpenedFiles_.Count > 0 )
		{
			RecentFilesSubMenu.SetActive(true);
			UnityEngine.UI.Button[] buttons = RecentFilesSubMenu.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true);
			for( int i = 0; i < buttons.Length; ++i )
			{
				if( i < recentOpenedFiles_.Count )
				{
					buttons[i].gameObject.SetActive(true);
					buttons[i].GetComponentInChildren<Text>().text = recentOpenedFiles_[i];
				}
				else
				{
					buttons[i].gameObject.SetActive(false);
				}
			}
		}
	}

	public void LoadRecentFile(int index)
	{
		LoadTree(recentOpenedFiles_[index], true);
		FileMenu.Close();
	}

	#endregion
	
	
	#region load utils

	void AddRecentOpenedFiles(string path)
	{
		path = path.Replace('\\', '/');
		if( recentOpenedFiles_.Count < numRecentFilesMenu && recentOpenedFiles_.Contains(path) == false )
		{
			recentOpenedFiles_.Add(path);
		}
	}

	void LoadTree(string path, bool isActive)
	{
		AddRecentOpenedFiles(path);
		foreach( TreeNote existTree in trees_ )
		{
			if( existTree.File != null && existTree.File.FullName.Replace('\\', '/') == path.Replace('\\', '/') )
			{
				if( existTree != activeNote_ )
				{
					existTree.IsActive = true;
				}
				return;
			}
		}
		TreeNote treeNote = Instantiate(TreeNotePrefab.gameObject, TreeParent.transform).GetComponent<TreeNote>();
		TabButton tab = Instantiate(TabButtonPrefab.gameObject, TabParent.transform).GetComponent<TabButton>();
		LogNote logNote = Instantiate(LogNotePrefab.gameObject, LogTreeParent.transform).GetComponent<LogNote>();
		LogTabButton logtab = Instantiate(LogTabButtonPrefab.gameObject, LogTabParent.transform).GetComponent<LogTabButton>();
		treeNote.Load(path, tab, logNote, isActive);
		logNote.LoadToday(treeNote, logtab);
		treeNote.IsActive = isActive;
		OnTreeCreated(treeNote);
	}

	#endregion


	#region events

	public void OnTreeCreated(TreeNote newTree)
	{
		trees_.Add(newTree);

		UpdateTabLayout();
	}

	public void OnTreeActivated(TreeNote treeNote)
	{
		if( activeNote_ != null && treeNote != activeNote_ )
		{
			activeNote_.IsActive = false;
		}
		activeNote_ = treeNote;
		UpdateLogTabLayout();
	}

	public void OnTreeClosed(TreeNote closedTree)
	{
		if( closedTree.File != null )
		{
			recentClosedFiles_.Push(closedTree.File.FullName);
		}
		List<ShortLine> removeList = new List<ShortLine>();
		foreach( ShortLine shortLine in LineList )
		{
			if( shortLine.BindedLine.Tree == closedTree )
			{
				removeList.Add(shortLine);
			}
		}
		foreach( ShortLine shortLine in removeList )
		{
			LineList.RemoveShortLine(shortLine);
		}

		int index = trees_.IndexOf(closedTree);
		trees_.Remove(closedTree);
		if( trees_.Count == 0 )
		{
			NewFile();
		}
		else if( closedTree == activeNote_ )
		{
			if( index >= trees_.Count ) index = trees_.Count - 1;
			trees_[index].IsActive = true;
		}

		UpdateTabLayout();
	}

	public void OnHeaderWidthChanged()
	{
		NoteAreaTransform.offsetMin = new Vector3(HeaderWidth, NoteAreaTransform.offsetMin.y);
		UpdateTabLayout();
	}

	public void OnLogTabClosed(LogNote logNote)
	{
		if( activeNote_ != null && logNote == activeNote_.LogNote )
		{
			UpdateLogTabLayout();
		}
	}

	public void OnLogTabOpened()
	{
		if( activeNote_.LogNote.IsOpended == false )
			activeNote_.LogNote.IsOpended = true;

		UpdateLogTabLayout();
	}

	#endregion


	#region tab

	void UpdateTabLayout()
	{
		currentTabWidth_ = DesiredTabWidth;
		if( DesiredTabWidth * trees_.Count > UnityEngine.Screen.width - HeaderWidth )
		{
			currentTabWidth_ = (UnityEngine.Screen.width - HeaderWidth) / trees_.Count;
		}
		foreach( TreeNote tree in trees_ )
		{
			tree.Tab.Width = currentTabWidth_;
			tree.Tab.TargetPosition = GetTabPosition(tree.Tab);
		}
	}

	Vector3 GetTabPosition(TabButton tab)
	{
		return Vector3.right * currentTabWidth_ * (trees_.IndexOf(tab.BindedNote));
	}

	void UpdateLogTabLayout()
	{
		if( activeNote_ == null || activeNote_.LogNote == null ) return;

		LogTabParent.transform.parent.gameObject.SetActive(activeNote_.LogNote.IsOpended);

		float logNoteRatio = activeNote_.LogNote.IsOpended ? activeNote_.LogNote.OpenRatio : 0.0f;
		float height = NoteAreaTransform.rect.height;

		TreeNoteTransform.sizeDelta = new Vector2(TreeNoteTransform.sizeDelta.x, height * (1.0f - logNoteRatio) - (logNoteRatio > 0.0f ? 40.0f : 0.0f));
		LogNoteTransform.sizeDelta = new Vector2(LogNoteTransform.sizeDelta.x, height * logNoteRatio);
		LogNoteTransform.anchoredPosition = new Vector2(LogNoteTransform.anchoredPosition.x, -height + LogNoteTransform.sizeDelta.y);

		activeNote_.UpdateLayoutElement();
	}

	public void OnBeginTabDrag(TabButton tab)
	{
		tab.IsOn = true;
		if( tab.BindedNote.FocusedLine != null )
		{
			tab.BindedNote.FocusedLine.Field.IsFocused = false;
		}
		tab.transform.SetAsLastSibling();
	}

	public void OnTabDragging(TabButton tab, PointerEventData eventData)
	{
		int index = trees_.IndexOf(tab.BindedNote);
		tab.transform.localPosition += new Vector3(eventData.delta.x, 0);
		if( tab.transform.localPosition.x < 0 )
		{
			tab.transform.localPosition = new Vector3(0, tab.transform.localPosition.y);
		}
		float tabmax = (UnityEngine.Screen.width - HeaderWidth) - currentTabWidth_;
		if( tab.transform.localPosition.x > tabmax )
		{
			tab.transform.localPosition = new Vector3(tabmax, tab.transform.localPosition.y);
		}
		int desiredIndex = Mathf.Clamp((int)(tab.transform.localPosition.x / currentTabWidth_), 0, trees_.Count - 1);
		if( index != desiredIndex )
		{
			trees_.Remove(tab.BindedNote);
			trees_.Insert(desiredIndex, tab.BindedNote);
			int sign = (int)Mathf.Sign(desiredIndex - index);
			for( int i = index; i != desiredIndex; i += sign )
			{
				AnimManager.AddAnim(trees_[i].Tab, GetTabPosition(trees_[i].Tab), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
			}
		}
	}

	public void OnEndTabDrag(TabButton tab)
	{
		AnimManager.AddAnim(tab, GetTabPosition(tab), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
	}

	public void OnLogTabDragging(LogTabButton tab, PointerEventData eventData)
	{
		LogNoteTransform.anchoredPosition += new Vector2(0, eventData.delta.y);
		float height = NoteAreaTransform.rect.height;
		if( LogNoteTransform.anchoredPosition.y < -height )
		{
			LogNoteTransform.anchoredPosition = new Vector2(LogNoteTransform.anchoredPosition.x, -height);
		}
		else if( LogNoteTransform.anchoredPosition.y > 0 )
		{
			LogNoteTransform.anchoredPosition = new Vector2(LogNoteTransform.anchoredPosition.x, 0);
		}
		LogNoteTransform.sizeDelta = new Vector2(LogNoteTransform.sizeDelta.x, LogNoteTransform.anchoredPosition.y + height);
		activeNote_.LogNote.OpenRatio = LogNoteTransform.sizeDelta.y / height;
		TreeNoteTransform.sizeDelta = new Vector2(TreeNoteTransform.sizeDelta.x, height * (1.0f - activeNote_.LogNote.OpenRatio) - 40.0f);
	}

	#endregion


	#region IEnumerable<Tree>

	public IEnumerator<Tree> GetEnumerator()
	{
		foreach( Tree tree in trees_ )
		{
			yield return tree;
		}
	}
	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.GetEnumerator();
	}

	#endregion


	#region settings save / load

	enum Settings
	{
		InitialFiles,
		LogTab,
		RecentFiles,
		InitialDirectory,
		ScreenSize,
		IsToDoListOpened,
		Count
	}

	static string[] SettingsTags = new string[(int)Settings.Count] {
		"[initial files]",
		"[log tab]",
		"[recent files]",
		"[initial files]",
		"[screen]",
		"[todo list]"
		};

	void LoadSettings()
	{
		if( settingFile_.Exists == false )
		{
			return;
		}

		StreamReader reader = new StreamReader(settingFile_.OpenRead());
		string text = null;

		Settings setting = Settings.InitialFiles;
		while( (text = reader.ReadLine()) != null )
		{
			foreach(Settings set in (Settings[])Enum.GetValues(typeof(Settings)))
			{
				if( set == Settings.Count ) break;
				else if( SettingsTags[(int)set] == text )
				{
					setting = set;
					text = reader.ReadLine();
					break;
				}
			}
			switch(setting)
			{
			case Settings.InitialFiles:
				if( text.EndsWith(".dtml") && File.Exists(text) )
				{
					LoadTree(text, isActive: activeNote_ == null);
				}
				break;
			case Settings.LogTab:
				string[] tabparams = text.Split(',');
				for( int i = 0; i < trees_.Count; ++i )
				{
					if( trees_[i].TitleText == tabparams[0] )
					{
						trees_[i].LogNote.IsOpended = tabparams[1].EndsWith("open");
						trees_[i].LogNote.OpenRatio = float.Parse(tabparams[2].Remove(0, " ratio=".Length));
						break;
					}
				}
				UpdateLogTabLayout();
				break;
			case Settings.RecentFiles:
				if( text.EndsWith(".dtml") && File.Exists(text) )
				{
					AddRecentOpenedFiles(text);
				}
				break;
			case Settings.InitialDirectory:
				if( Directory.Exists(text) )
				{
					initialDirectory_ = text;
				}
				break;
			case Settings.ScreenSize:
				string[] size = text.Split(',');
				UnityEngine.Screen.SetResolution(int.Parse(size[0]), int.Parse(size[1]), size[2] == "true");
				break;
			case Settings.IsToDoListOpened:
				if( text == "open" )
				{
				}
				else if( text == "close" )
				{
					LineList.Close();
				}
				break;
			}
		}
		if( activeNote_ == null )
		{
			NewFile();
		}
		reader.Close();
	}

	void SaveSettings()
	{
		if( settingFile_.Exists == false )
		{
			if( Directory.Exists(settingFile_.DirectoryName) == false )
			{
				Directory.CreateDirectory(settingFile_.DirectoryName);
			}
		}

		StreamWriter writer = new StreamWriter(settingFile_.FullName, append: false);

		writer.WriteLine(SettingsTags[(int)Settings.InitialFiles]);
		foreach( TreeNote tree in trees_ )
		{
			if( tree.File != null )
			{
				writer.WriteLine(tree.File.FullName.ToString());
			}
		}
		writer.WriteLine(SettingsTags[(int)Settings.LogTab]);
		foreach( TreeNote tree in trees_ )
		{
			if( tree.File != null )
			{
				writer.WriteLine(String.Format("{0}, log={1}, ratio={2}", tree.File.Name, tree.LogNote.IsOpended ? "open" : "close", tree.LogNote.OpenRatio.ToString("F2")));
			}
		}
		writer.WriteLine(SettingsTags[(int)Settings.RecentFiles]);
		foreach( string recentFile in recentOpenedFiles_ )
		{
			writer.WriteLine(recentFile);
		}
		if( initialDirectory_ != null )
		{
			writer.WriteLine(SettingsTags[(int)Settings.InitialDirectory]);
			writer.WriteLine(initialDirectory_);
		}
		writer.WriteLine(SettingsTags[(int)Settings.ScreenSize]);
		writer.WriteLine(String.Format("{0},{1},{2}", UnityEngine.Screen.width, UnityEngine.Screen.height, UnityEngine.Screen.fullScreen ? "true" : "false"));
		writer.WriteLine(SettingsTags[(int)Settings.IsToDoListOpened]);
		writer.WriteLine(LineList.IsOpened ? "open" : "close");

		writer.Flush();
		writer.Close();
	}

	#endregion


	#region todo list save/load

	void LoadLineList()
	{
		if( lineListFile_.Exists == false )
		{
			return;
		}

		StreamReader reader = new StreamReader(lineListFile_.OpenRead());
		string text = null;
		int index = 0;
		while( (text = reader.ReadLine()) != null )
		{
			ShortLine shortLine = null;
			foreach( ShortLine line in LineList )
			{
				if( line.BindedLine.Text == text )
				{
					shortLine = line;
					break;
				}
			}
			if( shortLine != null )
			{
				LineList.SetLineIndex(shortLine, index);
				++index;
			}
		}
		reader.Close();
	}

	void SaveLineList()
	{
		if( lineListFile_.Exists == false )
		{
			if( Directory.Exists(lineListFile_.DirectoryName) == false )
			{
				Directory.CreateDirectory(lineListFile_.DirectoryName);
			}
		}

		StreamWriter writer = new StreamWriter(lineListFile_.FullName, append: false);

		foreach( ShortLine shortLine in LineList )
		{
			if( shortLine.IsDone )
			{
				break;
			}
			if( shortLine.BindedLine != null )
			{
				writer.WriteLine(shortLine.BindedLine.Text);
			}
		}
		writer.Flush();
		writer.Close();
	}

	#endregion


	#region window title

	// How can i change the title of the standalone player window? https://answers.unity3d.com/questions/148723/how-can-i-change-the-title-of-the-standalone-playe.html
	[DllImport("user32.dll", EntryPoint = "SetWindowText")]
	public static extern bool SetWindowText(System.IntPtr hwnd, System.String lpString);

	[DllImport("user32.dll")]
	static extern System.IntPtr GetActiveWindow();

	public void SetTitle(string text)
	{
		SetWindowText(GetActiveWindow(), text);
	}

	#endregion


	#region drop file

#if !HOOK_WNDPROC && UNITY_EDITOR
	void OnGUI()
	{
		var evt = Event.current;
		if( evt != null )
		{
			switch( evt.type )
			{
			case EventType.DragUpdated:
				{
					DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
				}
				break;
			case EventType.DragPerform:
				{
					DragAndDrop.AcceptDrag();
					bool isActive = true;
					foreach( string path in DragAndDrop.paths )
					{
						if( path.EndsWith(".dtml") )
						{
							LoadTree(path, isActive);
							isActive = false;
						}
					}
					evt.Use();
				}
				break;
			}
		}
	}
#elif HOOK_WNDPROC

	// 参考：
	// Unity(x86/x64)でWindowsメッセージを受け取る方法 - Qiita http://qiita.com/DandyMania/items/d1404c313f67576d395f
	// how to get the drag&drop url in unity? | Unity Community https://forum.unity3d.com/threads/how-to-get-the-drag-drop-url-in-unity.23405/

	const int GWL_WNDPROC = -4;

	void OnGUI()
	{
		// ウィンドウハンドルが切り替わったので初期化 
		if( hMainWindow.Handle == IntPtr.Zero )
		{
			InitWndProc();
		}
	}

	void OnDisable()
	{
		TermWndProc();
	}

	#region hook window event

	private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
	private HandleRef hMainWindow;
	private IntPtr newWndProcPtr;
	private IntPtr oldWndProcPtr;
	private WndProcDelegate newWndProc;

	void InitWndProc()
	{
		// ウインドウプロシージャをフックする
		hMainWindow = new HandleRef(null, GetActiveWindow());
		newWndProc = new WndProcDelegate(WndProc);
		newWndProcPtr = Marshal.GetFunctionPointerForDelegate(newWndProc);
		oldWndProcPtr = SetWindowLongPtr(hMainWindow.Handle, GWL_WNDPROC, newWndProcPtr);
		DragAcceptFiles(hMainWindow.Handle, true);
	}

	void TermWndProc()
	{
		if( newWndProc != null )
		{
			SetWindowLongPtr(hMainWindow.Handle, GWL_WNDPROC, oldWndProcPtr);
			hMainWindow = new HandleRef(null, IntPtr.Zero);
			newWndProcPtr = IntPtr.Zero;
			oldWndProcPtr = IntPtr.Zero;
			newWndProc = null;
		}
	}
	
	[DllImport("user32.dll", EntryPoint = "SetWindowLong")]
	private static extern int SetWindowLong32(HandleRef hWnd, int nIndex, int dwNewLong);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
	private static extern IntPtr SetWindowLongPtr64(HandleRef hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport("user32.dll")]
	static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport("user32.dll")]
	static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport("user32.dll", EntryPoint = "DefWindowProcA")]
	private static extern IntPtr DefWindowProc(IntPtr hWnd, uint wMsg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll", EntryPoint = "CallWindowProc")]
	private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint wMsg, IntPtr wParam, IntPtr lParam);

	private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
	{
		if( msg == WM_NCDESTROY || msg == WM_WINDOWPOSCHANGING )
		{
			TermWndProc();
			return DefWindowProc(hwnd, msg, wParam, lParam);
		}
		if( msg == WM_DROPFILES )
		{
			HandleDropFiles(wParam);
		}

		return CallWindowProc(oldWndProcPtr, hwnd, msg, wParam, lParam);
	}

	#endregion


	#region hook drag event

	[DllImport("shell32.dll")]
	static extern void DragAcceptFiles(IntPtr hwnd, bool fAccept);

	[DllImport("shell32.dll")]
	static extern uint DragQueryFile(IntPtr hDrop, uint iFile, [Out] StringBuilder filename, uint cch);

	[DllImport("shell32.dll")]
	static extern void DragFinish(IntPtr hDrop);

	const int WM_DROPFILES = 0x233;
	const int WM_MOUSEHWHEEL = 0x20E;
	const int WM_NCHITTEST = 0x084;
	const int WM_NCDESTROY = 0x082;
	const int WM_WINDOWPOSCHANGING = 0x046;

	private void HandleDropFiles(IntPtr hDrop)
	{
		const int MAX_PATH = 260;

		var count = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);

		bool isActive = true;
		for( uint i = 0; i < count; i++ )
		{
			int size = (int)DragQueryFile(hDrop, i, null, 0);

			var filename = new StringBuilder(size + 1);
			DragQueryFile(hDrop, i, filename, MAX_PATH);

			if( filename.ToString().EndsWith(".dtml") )
			{
				LoadTree(filename.ToString(), isActive);
				isActive = false;
			}
		}

		DragFinish(hDrop);
	}


	#endregion

#endif

	#endregion
}
