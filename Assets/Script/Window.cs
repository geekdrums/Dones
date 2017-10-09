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
public class Window : MonoBehaviour
{
	#region editor params

	public TabGroup MainTabGroup;

	public TabButton TabButtonPrefab;
	public TreeNote TreeNotePrefab;
	public LogNote LogNotePrefab;

	public MenuButton FileMenu;
	public GameObject RecentFilesSubMenu;
	public ShortLineList LineList;
	public ModalDialog ModalDialog;
	public Text FontSizeText;

	#endregion


	#region params

	FileInfo settingFile_;
	FileInfo lineListFile_;

	string initialDirectory_;

	float currentScreenWidth_;
	float currentScreenHeight_;
	
	List<string> recentOpenedFiles_ = new List<string>();
	int numRecentFilesMenu = 0;
	bool saveConfirmed_ = false;

	public float HeaderWidth { get { return LineList.Width + 5.0f; } }

	#endregion


	#region unity events

	void Awake()
	{
		GameContext.Window = this;
		currentScreenWidth_ = UnityEngine.Screen.width;
		currentScreenHeight_ = UnityEngine.Screen.height;

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
		MainTabGroup.UpdateLayoutAll();
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

			if( Input.mouseScrollDelta.y > 0 )
			{
				if( GameContext.Config.FontSize < 20 )
				{
					GameContext.Config.FontSize += 1;
					foreach( TreeNote treeNote in MainTabGroup )
					{
						treeNote.OnFontSizeChanged();
					}
					FontSizeText.text = "FontSize:" + GameContext.Config.FontSize.ToString();
					FontSizeText.color = GameContext.Config.TextColor;
					FontSizeText.gameObject.SetActive(true);
					AnimManager.AddAnim(FontSizeText.gameObject, 0.0f, ParamType.TextAlphaColor, AnimType.Time, 3.0f, endOption: AnimEndOption.Deactivate);
				}
			}
			else if( Input.mouseScrollDelta.y < 0 )
			{
				if( GameContext.Config.FontSize > 12 )
				{
					GameContext.Config.FontSize -= 1;
					foreach( TreeNote treeNote in MainTabGroup )
					{
						treeNote.OnFontSizeChanged();
					}
					FontSizeText.text = "FontSize:" + GameContext.Config.FontSize.ToString();
					FontSizeText.color = GameContext.Config.TextColor;
					FontSizeText.gameObject.SetActive(true);
					AnimManager.AddAnim(FontSizeText.gameObject, 0.0f, ParamType.TextAlphaColor, AnimType.Time, 3.0f, endOption: AnimEndOption.Deactivate);
				}
			}
		}

		if(	currentScreenWidth_ != UnityEngine.Screen.width )
		{
			MainTabGroup.UpdateHorizontalLayout();
			currentScreenWidth_ = UnityEngine.Screen.width;
		}
		if( currentScreenHeight_ != UnityEngine.Screen.height )
		{
			MainTabGroup.UpdateVerticalLayout();
			currentScreenHeight_ = UnityEngine.Screen.height;
		}
	}

	void OnApplicationQuit()
	{
		if( GameContext.Config.IsAutoSave )
		{
			foreach( TreeNote tree in MainTabGroup )
			{
				tree.Save();
				if( GameContext.Config.DoBackUp )
				{
					tree.DeleteBackup();
				}
			}
		}
		else
		{
			if( saveConfirmed_ == false )
			{
				saveConfirmed_ = true;
				foreach( TreeNote tree in MainTabGroup )
				{
					if( tree.IsEdited || tree.LogNote.IsEdited )
					{
						GameContext.Window.ModalDialog.Show("ファイルへの変更を保存しますか？", this.CloseConfirmCallback);
						UnityEngine.Application.CancelQuit();
						return;
					}
				}
			}
		}

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
			SaveAll();
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

			if( MainTabGroup.ActiveNote != null )
			{
				initialDirectory_ = MainTabGroup.ActiveNote.File.Directory.FullName;
			}
		}

		FileMenu.Close();
	}

	public void NewFile()
	{
		MainTabGroup.NewFile();

		FileMenu.Close();
	}

	public void Save()
	{
		MainTabGroup.Save();

		FileMenu.Close();
	}

	public void SaveAs()
	{
		MainTabGroup.SaveAs();

		FileMenu.Close();
	}

	public void SaveAll()
	{
		foreach( TreeNote tree in MainTabGroup )
		{
			if( tree.IsEdited )
			{
				tree.Save();
			}
		}
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

	public void AddRecentOpenedFiles(string path)
	{
		path = path.Replace('\\', '/');
		if( recentOpenedFiles_.Count < numRecentFilesMenu && recentOpenedFiles_.Contains(path) == false && File.Exists(path) )
		{
			recentOpenedFiles_.Add(path);
		}
	}

	void LoadTree(string path, bool isActive)
	{
		AddRecentOpenedFiles(path);
		MainTabGroup.LoadTree(path, isActive);
	}

	#endregion


	#region events
	
	public void OnHeaderWidthChanged()
	{
		MainTabGroup.UpdateHorizontalLayout();
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
		FontSize,
		IsToDoListOpened,
		Count
	}

	static string[] SettingsTags = new string[(int)Settings.Count] {
		"[initial files]",
		"[log tab]",
		"[recent files]",
		"[initial files]",
		"[screen]",
		"[font size]",
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
					LoadTree(text, isActive: MainTabGroup.ActiveNote == null);
				}
				break;
			case Settings.LogTab:
				string[] tabparams = text.Split(',');
				for( int i = 0; i < MainTabGroup.Count; ++i )
				{
					if( MainTabGroup[i].TitleText == tabparams[0] )
					{
						MainTabGroup[i].LogNote.OpenRatio = float.Parse(tabparams[2].Remove(0, " ratio=".Length));
						MainTabGroup[i].LogNote.IsOpended = tabparams[1].EndsWith("open");
						MainTabGroup[i].Tab.UpdateTitleText();
						MainTabGroup[i].Tab.UpdateColor();
						break;
					}
				}
				MainTabGroup.UpdateVerticalLayout();
				MainTabGroup.UpdateLogTabButtons();
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
			case Settings.FontSize:
				GameContext.Config.FontSize = int.Parse(text);
				foreach( TreeNote treeNote in MainTabGroup )
				{
					treeNote.OnFontSizeChanged();
				}
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
		if( MainTabGroup.ActiveNote == null )
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
		foreach( TreeNote tree in MainTabGroup )
		{
			if( tree.File != null )
			{
				writer.WriteLine(tree.File.FullName.ToString());
			}
		}
		writer.WriteLine(SettingsTags[(int)Settings.LogTab]);
		foreach( TreeNote tree in MainTabGroup )
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
		writer.WriteLine(SettingsTags[(int)Settings.FontSize]);
		writer.WriteLine(GameContext.Config.FontSize);
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
