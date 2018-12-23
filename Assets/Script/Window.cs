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
using System.Xml;
using System.Xml.Serialization;
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

	public GameObject TabParent;
	public TabButton HomeTabButton;
	public ContextMenu ContextMenu;
	
	public ModalDialog ModalDialog;
	public TagIncrementalDialog TagIncrementalDialog;
	public TagMenu TagMenu;
	public Text FontSizeText;
	public SaveText SaveText;
	public GameObject TitleLine;
	public Text DayText;

	#endregion


	#region params

	public TreeNote Note;
	public LogNote LogNote;

	FileInfo treeFile_;
	FileInfo settingFile_;
	DirectoryInfo logDirectory_;

	float currentScreenWidth_;
	float currentScreenHeight_;
	
	Stack<TreePath> recentClosedTabs_ = new Stack<TreePath>();

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
		treeFile_ = new FileInfo(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/Dones/tree.dtml");
		settingFile_ = new FileInfo(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/Dones/settings.xml");
		logDirectory_ = new DirectoryInfo(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/Dones/log");
		StartCoroutine(InitialLoadCoroutine());
	}

	IEnumerator InitialLoadCoroutine()
	{
		// Editorではいいんだけど、アプリ版はこうしないとScrollがバグってその後一切操作できなくなる。。
		yield return new WaitForEndOfFrame();
		GameContext.TagList.LoadTaggedLines();
		LoadNote();
		LoadLog();
		LoadSettings();
		foreach( TagParent tagParent in GameContext.TagList )
		{
			tagParent.ApplyLineOrder();
		}

		MainTabGroup.UpdateLayoutAll();
		Note.OnFontSizeChanged();

		DayText.text = String.Format("<size=12>{0}/ </size>{1}/{2}<size=12> ({3})</size>",
			DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MM").TrimStart('0'), DateTime.Now.ToString("dd").TrimStart('0'), DateTime.Now.ToString("ddd"));
	}

	// Update is called once per frame
	void Update()
	{
		bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
		bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
		bool ctrlOnly = ctrl && !alt && !shift;

		if( ctrlOnly )
		{
			if( Input.GetKeyDown(KeyCode.T) )
			{
				if( GameContext.TagList.IsOpened )
				{
					GameContext.TagList.Close();
				}
				else
				{
					GameContext.TagList.Open();
				}
			}

			if( Input.mouseScrollDelta.y > 0 )
			{
				if( GameContext.Config.FontSize < 20 )
				{
					GameContext.Config.FontSize += 1;
					foreach( TreeNote treeNote in MainTabGroup.TreeNotes )
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
					foreach( TreeNote treeNote in MainTabGroup.TreeNotes )
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
		if( ctrl && shift )
		{
			if( Input.GetKeyDown(KeyCode.T)  )
			{
				Line line = null;
				while( line == null && recentClosedTabs_.Count > 0 )
				{
					line = Note.Tree.GetLineFromPath(recentClosedTabs_.Pop());
				}

				if( line != null )
				{
					AddTab(line);
				}
			}
		}
		if( Input.GetKeyDown(KeyCode.F5) && MainTabGroup.ActiveNote != null )
		{
			MainTabGroup.ActiveNote.ReloadNote();
		}
		if( MainTabGroup.ActiveNote != null )
		{
			if( MainTabGroup.ActiveNote.TimeFromRequestedAutoSave() > GameContext.Config.AutoSaveTime )
			{
				MainTabGroup.ActiveNote.DoAutoSave();
			}
		}

		if(	currentScreenWidth_ != UnityEngine.Screen.width )
		{
			MainTabGroup.UpdateTabLayout();
			currentScreenWidth_ = UnityEngine.Screen.width;
		}
		if( currentScreenHeight_ != UnityEngine.Screen.height )
		{
			UpdateVerticalLayout();
			currentScreenHeight_ = UnityEngine.Screen.height;
		}
	}

	void OnApplicationQuit()
	{
		Note.SaveNote();
		
		SaveSettings();
		GameContext.TagList.SaveTaggedLines();

#if HOOK_WNDPROC
		TermWndProc();
#endif
	}

	#endregion
	
	
	#region load utils

	public void AddRecentClosedTab(TabButton tab)
	{
		recentClosedTabs_.Push(tab.ViewParam.Path);
	}

	public void AddTab(TreePath path, bool select = true)
	{
		foreach( TabButton existTab in MainTabGroup )
		{
			if( existTab.BindedNote is TreeNote )
			{
				if( existTab.ViewParam.Path.Equals(path) )
				{
					existTab.IsSelected = select;
					return;
				}
			}
		}

		TabButton tab = Instantiate(TabButtonPrefab.gameObject, TabParent.transform).GetComponent<TabButton>();
		MainTabGroup.OnTabCreated(tab);
		tab.Bind(Note, path);
		tab.IsSelected = select;
	}

	public void AddTab(Line line)
	{
		AddTab(line.GetTreePath());
	}

	void LoadNote()
	{
		Note.LoadNote(treeFile_.FullName);
		HomeTabButton.Bind(Note);
		MainTabGroup.OnTabCreated(HomeTabButton);
	}

	void LoadLog()
	{
		if( Directory.Exists(logDirectory_.FullName) == false )
		{
			Directory.CreateDirectory(logDirectory_.FullName);
		}

		LogNote.Initialize(Note);
	}

	#endregion


	#region events

	public void OnHeaderWidthChanged()
	{
		MainTabGroup.UpdateTabLayout();
	}

	#endregion


	#region layout

	public void UpdateVerticalLayout()
	{
		TreeNote treeNote = MainTabGroup.ActiveTreeNote;
		if( treeNote != null )
		{
			treeNote.LogNote.UpdateVerticalLayout();
			treeNote.UpdateVerticalLayout();
		}
	}

	#endregion


	#region settings save / load

	[XmlRoot("setting")]
	public class SettingXML
	{
		[XmlArray("tabparams")]
		[XmlArrayItem("tab")]
		public List<TabViewParam> TabParams { get; set; }

		[XmlElement("Screen")]
		public ScreenSetting Screen { get; set; }

		[XmlElement("SelectedTabIndex")]
		public int SelectedTabIndex { get; set; }

		[XmlElement("taglist")]
		public bool IsTagListOpened { get; set; }

		[XmlElement("LogNote")]
		public LogNote.OpenState LogNoteOpenState { get; set; }
	}

	public class TabViewParam
	{
		[XmlArray("path")]
		[XmlArrayItem("line")]
		public List<string> Path { get; set; }
	}

	public class ScreenSetting
	{
		[XmlAttribute("Width")]
		public int Width { get; set; }
		[XmlAttribute("Height")]
		public int Height { get; set; }
		[XmlAttribute("IsMaximized")]
		public bool IsMaximized { get; set; }
	}


	void LoadSettings()
	{
		if( settingFile_.Exists == false )
		{
			return;
		}

		XmlSerializer serializer = new XmlSerializer(typeof(SettingXML));
		SettingXML settingXml = (SettingXML)serializer.Deserialize(new StreamReader(settingFile_.OpenRead()));

		UnityEngine.Screen.SetResolution(settingXml.Screen.Width, settingXml.Screen.Height, settingXml.Screen.IsMaximized ? FullScreenMode.MaximizedWindow : FullScreenMode.Windowed);

		foreach(TabViewParam tabparam in settingXml.TabParams )
		{
			AddTab(new TreePath(tabparam.Path), select: false);
		}
		MainTabGroup[settingXml.SelectedTabIndex].IsSelected = true;

		if( settingXml.IsTagListOpened )
		{
			GameContext.TagList.Open();
		}
		else
		{
			GameContext.TagList.Close();
		}

		if( settingXml.LogNoteOpenState == LogNote.OpenState.Minimize )
		{
			LogNote.Minimize();
		}
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

		SettingXML setting = new SettingXML();

		setting.TabParams = new List<TabViewParam>();
		foreach( TabButton tabButton in MainTabGroup )
		{
			if( tabButton.BindedNote is TreeNote )
			{
				TabViewParam tabparam = new TabViewParam();
				tabparam.Path = new List<string>(tabButton.ViewParam.Path);
				setting.TabParams.Add(tabparam);
			}
		}
		setting.SelectedTabIndex = MainTabGroup.IndexOf(MainTabGroup.ActiveTab);

		setting.Screen = new ScreenSetting();
		setting.Screen.Width = UnityEngine.Screen.width;
		setting.Screen.Height = UnityEngine.Screen.height;
		setting.Screen.IsMaximized = (UnityEngine.Screen.fullScreenMode == FullScreenMode.MaximizedWindow);

		setting.IsTagListOpened = GameContext.TagList.IsOpened;
		setting.LogNoteOpenState = LogNote.State;

		StreamWriter writer = new StreamWriter(settingFile_.FullName);
		XmlSerializer serializer = new XmlSerializer(typeof(SettingXML));
		serializer.Serialize(writer, setting);
		writer.Flush();
		writer.Close();
	}

	void SaveAllTreeInOneFile()
	{
		string[] paths = new string[]
		{
			"C:\\Users\\geekdrums\\Dropbox\\Dones\\Archive\\note.dtml",
			"C:\\Users\\geekdrums\\Dropbox\\Dones\\Archive\\daily.dtml",
			"C:\\Users\\geekdrums\\Dropbox\\Dones\\Archive\\dones.dtml",
			"C:\\Users\\geekdrums\\Dropbox\\Dones\\Archive\\todo.dtml",
			"C:\\Users\\geekdrums\\Dropbox\\Dones\\Archive\\input.dtml",
			"C:\\Users\\geekdrums\\Dropbox\\Dones\\Archive\\Work.dtml",
			"C:\\Users\\geekdrums\\Dropbox\\Dones\\Archive\\VQNote.dtml",
			"C:\\Users\\geekdrums\\Dropbox\\Dones\\Archive\\SQNote4.dtml",
			"C:\\Users\\geekdrums\\Dropbox\\Dones\\Archive\\interactiveMusic.dtml",
			"C:\\Users\\geekdrums\\Dropbox\\Dones\\Archive\\gamedesign.dtml",
			"C:\\Users\\geekdrums\\Dropbox\\Dones\\Archive\\LudumDare.dtml",
		};

		string[] titles = new string[]
		{
			"",
			"Daily",
			"Dones",
			"ToDo",
			"Input",
			"Work",
			"VQNote",
			"SQNote",
			"InteractiveMusic",
			"GameDesign",
			"LudumDare",
		};

		List<TreeNote> treeNotes = new List<TreeNote>();
		StringBuilder builder = new StringBuilder();
		for( int i = 0; i < paths.Length; ++i )
		{
			TabButton tab = Instantiate(TabButtonPrefab.gameObject, TabParent.transform).GetComponent<TabButton>();
			TreeNote treeNote = Instantiate(Note.gameObject, Note.transform.parent).GetComponent<TreeNote>();
			LogNote logNote = Instantiate(LogNote.gameObject, LogNote.transform.parent).GetComponent<LogNote>();
			treeNote.LogNote = logNote;
			logNote.TreeNote = treeNote;

			tab.Bind(Note);
			MainTabGroup.OnTabCreated(tab);
			treeNote.LoadNote(paths[i]);

			treeNotes.Add(treeNote);
			treeNote.Tree.SaveAllTreeInOneFile(builder, titles[i]);
		}

		FileInfo file = new FileInfo(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/Dones/tree.dtml");
		StreamWriter writer = new StreamWriter(file.FullName, append: false);
		writer.Write(builder.ToString());
		writer.Flush();
		writer.Close();

		DirectoryInfo logDirectory = new DirectoryInfo(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/Dones/log");
		for( int i = 0; i < treeNotes.Count; ++i )
		{
			treeNotes[i].LogNote.SaveAllLogFilesToOneDirectory(logDirectory, titles[i]);
		}
	}

	#endregion



	#region window title

	// How can i change the title of the standalone player window? https://answers.unity3d.com/questions/148723/how-can-i-change-the-title-of-the-standalone-playe.html
	[DllImport("user32.dll", EntryPoint = "SetWindowText", CharSet = CharSet.Unicode)]
	public static extern bool SetWindowText(System.IntPtr hwnd, IntPtr lpString);

	[DllImport("user32.dll")]
	static extern System.IntPtr GetActiveWindow();

	public void SetTitle(string text)
	{
		SetWindowText(GetActiveWindow(), Marshal.StringToHGlobalUni(text));
	}

	#endregion
}
