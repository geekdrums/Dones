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

	public TabGroup TabGroup;

	public TagList TagList;

	public ContextMenu ContextMenu;
	public ModalDialog ModalDialog;
	public TagIncrementalDialog TagIncrementalDialog;
	public TagMenu TagMenu;
	public SearchField SearchField;

	public Text FontSizeText;
	public SaveText SaveText;
	public GameObject TitleLine;
	public Text DayText;

	#endregion


	#region params

	public TreeNote Note;
	public LogNote LogNote;

	DirectoryInfo donesDirectory_;
	FileInfo settingFile_;
	SettingXML settingXml_;
	FileInfo treeFile_;
	DirectoryInfo logDirectory_;

	float currentScreenWidth_;
	float currentScreenHeight_;

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
		StartCoroutine(InitialLoadCoroutine());
	}

	IEnumerator InitialLoadCoroutine()
	{
		// Editorではいいんだけど、アプリ版はこうしないとScrollがバグってその後一切操作できなくなる。。
		yield return new WaitForEndOfFrame();

		// donesDirectoryを設定、なければ生成
		InitializeDonesDirectory();

		// donesDirectoryから設定ファイルをロード
		GameContext.Config.LoadConfig(donesDirectory_.FullName + "/config.xml");
		LoadSettings(donesDirectory_.FullName + "/settings.xml");

		// noteのファイルとフォルダ情報を設定。保存先は設定により異なる。
		InitializeNoteDirectory();
		// noteより先にタグの情報を読み込む必要がある
		TagList.LoadTagListSettings(donesDirectory_.FullName + "/taglist.xml");
		TagList.ApplyTagListSetttings();
		// HomeタブをInitialize
		TabGroup.Initialize(Note);
		// noteとlogを読み込み
		Note.LoadNote(treeFile_.FullName);
		LogNote.Initialize(Note);

		// 各種UIの状態を復元
		ApplySettings();

		foreach( TagParent tagParent in TagList )
		{
			tagParent.ApplyLineOrder();
		}

		TabGroup.UpdateLayoutAll();
		Note.OnFontSizeChanged();

		DayText.text = String.Format("<size=12>{0}/ </size>{1}/{2}<size=12> ({3})</size>",
			DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MM").TrimStart('0'), DateTime.Now.ToString("dd").TrimStart('0'), DateTime.Now.ToString("ddd"));

		SearchField.Initialize();
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
				if( TagList.IsOpened )
				{
					TagList.Close();
				}
				else
				{
					TagList.Open();
				}
			}

			if( Input.GetKeyDown(KeyCode.F) )
			{
				SearchField.IsFocused = true;
			}

			if( Input.mouseScrollDelta.y > 0 )
			{
				if( GameContext.Config.FontSize < 20 )
				{
					GameContext.Config.FontSize += 1;
					Note.OnFontSizeChanged();
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
					Note.OnFontSizeChanged();
					FontSizeText.text = "FontSize:" + GameContext.Config.FontSize.ToString();
					FontSizeText.color = GameContext.Config.TextColor;
					FontSizeText.gameObject.SetActive(true);
					AnimManager.AddAnim(FontSizeText.gameObject, 0.0f, ParamType.TextAlphaColor, AnimType.Time, 3.0f, endOption: AnimEndOption.Deactivate);
				}
			}
		}
		if( Input.GetKeyDown(KeyCode.F5) && TabGroup.ActiveNote == Note )
		{
			TagList.ClearAll();
			TagList.ApplyTagListSetttings();
			Note.ReloadNote();
			TagList.OnTreePathChanged(Note.Tree.TitleLine);
		}
		if( TabGroup.ActiveNote == Note )
		{
			if( Note.TimeFromRequestedAutoSave() > GameContext.Config.AutoSaveTime )
			{
				Note.DoAutoSave();
			}
		}

		if(	currentScreenWidth_ != UnityEngine.Screen.width )
		{
			TabGroup.UpdateTabLayout();
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
		TagList.SaveTagListSettings();

		SaveSettings();

#if HOOK_WNDPROC
		TermWndProc();
#endif
	}

	#endregion


	#region events

	public void OnHeaderWidthChanged()
	{
		TabGroup.UpdateTabLayout();
	}

	#endregion


	#region layout

	public void UpdateVerticalLayout()
	{
		TreeNote treeNote = TabGroup.ActiveTreeNote;
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
		[XmlElement("SaveDirectory")]
		public string SaveDirectory { get; set; }

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

	void InitializeDonesDirectory()
	{
		donesDirectory_ = new DirectoryInfo(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/Dones");

		if( Directory.Exists(donesDirectory_.FullName) == false )
		{
			Directory.CreateDirectory(donesDirectory_.FullName);
		}
	}

	void LoadSettings(string filepath)
	{
		settingFile_ = new FileInfo(filepath);

		if( settingFile_.Exists == false )
		{
			return;
		}

		XmlSerializer serializer = new XmlSerializer(typeof(SettingXML));
		StreamReader reader = new StreamReader(settingFile_.FullName);
		settingXml_ = (SettingXML)serializer.Deserialize(reader);
		reader.Close();
	}

	void InitializeNoteDirectory()
	{
		string saveDirectory = (settingXml_ != null ? settingXml_.SaveDirectory : null);
		if( saveDirectory == null || saveDirectory == "" )
		{
			saveDirectory = donesDirectory_.FullName;
		}

		treeFile_ = new FileInfo(saveDirectory + "/tree.dtml");
		logDirectory_ = new DirectoryInfo(saveDirectory + "/log");

		if( Directory.Exists(logDirectory_.FullName) == false )
		{
			Directory.CreateDirectory(logDirectory_.FullName);
		}
	}

	void ApplySettings()
	{
		if( settingXml_ == null )
		{
			TabGroup[0].DoSelect();
			return;
		}

		UnityEngine.Screen.SetResolution(settingXml_.Screen.Width, settingXml_.Screen.Height, settingXml_.Screen.IsMaximized ? FullScreenMode.MaximizedWindow : FullScreenMode.Windowed);

		foreach(TabViewParam tabparam in settingXml_.TabParams )
		{
			TabGroup.AddTab(Note, new TreePath(tabparam.Path), select: false);
		}

		TabButton tab = TabGroup[Math.Max(0, settingXml_.SelectedTabIndex)];
		if( tab.CanSelect(showDialog: false) )
		{
			tab.DoSelect();
		}
		else
		{
			TabGroup[0].DoSelect();
		}

		if( settingXml_.IsTagListOpened )
		{
			TagList.Open();
		}
		else
		{
			TagList.Close();
		}

		if( settingXml_.LogNoteOpenState == LogNote.OpenState.Minimize )
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

		if( settingXml_ == null )
		{
			settingXml_ = new SettingXML();
		}

		settingXml_.TabParams = new List<TabViewParam>();
		foreach( TabButton tabButton in TabGroup )
		{
			if( tabButton.BindedNote is TreeNote )
			{
				TabViewParam tabparam = new TabViewParam();
				tabparam.Path = new List<string>(tabButton.ViewParam.Path);
				settingXml_.TabParams.Add(tabparam);
			}
		}
		settingXml_.SelectedTabIndex = TabGroup.IndexOf(TabGroup.ActiveTab);

		settingXml_.SaveDirectory = Note.Tree.File.Directory.FullName;
		settingXml_.Screen = new ScreenSetting();
		settingXml_.Screen.Width = UnityEngine.Screen.width;
		settingXml_.Screen.Height = UnityEngine.Screen.height;
		settingXml_.Screen.IsMaximized = (UnityEngine.Screen.fullScreenMode == FullScreenMode.MaximizedWindow);

		settingXml_.IsTagListOpened = TagList.IsOpened;
		settingXml_.LogNoteOpenState = LogNote.State;

		StreamWriter writer = new StreamWriter(settingFile_.FullName);
		XmlSerializer serializer = new XmlSerializer(typeof(SettingXML));
		serializer.Serialize(writer, settingXml_);
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
			TreeNote treeNote = Instantiate(Note.gameObject, Note.transform.parent).GetComponent<TreeNote>();
			LogNote logNote = Instantiate(LogNote.gameObject, LogNote.transform.parent).GetComponent<LogNote>();
			treeNote.LogNote = logNote;
			logNote.TreeNote = treeNote;
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
