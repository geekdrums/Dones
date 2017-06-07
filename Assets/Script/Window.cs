using UnityEngine;
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

	public Tree TreePrefab;
	public TabButton TabButtonPrefab;

	public GameObject TreeParent;
	public GameObject TabParent;
	public MenuButton FileMenu;

	#endregion


	#region params

	Tree activeTree_;
	List<Tree> trees_ = new List<Tree>();
	FileInfo settingFile_;

	#endregion


	#region unity functions

	void Awake()
	{
		GameContext.Window = this;
	}

	// Use this for initialization
	void Start ()
	{
		settingFile_ = new FileInfo(UnityEngine.Application.streamingAssetsPath + "/settings.txt");
		StartCoroutine(InitialLoadCoroutine());
	}

	IEnumerator InitialLoadCoroutine()
	{
		// Editorではいいんだけど、アプリ版はこうしないとScrollがバグってその後一切操作できなくなる。。
		yield return new WaitForEndOfFrame();
		LoadSettings();
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
		}
	}

	void OnApplicationQuit()
	{
		SaveSettings();
	}

	#endregion


	#region file menu

	public void OpenFile()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Filter = "dones file (*.dtml)|*.dtml";
		openFileDialog.Multiselect = true;
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
		}

		FileMenu.Close();
	}

	public void NewFile()
	{
		Tree tree = Instantiate(TreePrefab.gameObject, TreeParent.transform).GetComponent<Tree>();
		TabButton tab = Instantiate(TabButtonPrefab.gameObject, TabParent.transform).GetComponent<TabButton>();
		tree.NewFile(tab);
		trees_.Add(tree);
		tree.IsActive = true;

		FileMenu.Close();
	}

	public void Save()
	{
		if( activeTree_ != null )
		{
			activeTree_.Save();
		}

		FileMenu.Close();
	}

	public void SaveAs()
	{
		if( activeTree_ != null )
		{
			activeTree_.Save(saveAs: true);
		}

		FileMenu.Close();
	}

	void LoadTree(string path, bool isActive)
	{
		foreach( Tree existTree in trees_ )
		{
			if( existTree.File != null && existTree.File.FullName == path )
			{
				if( existTree != activeTree_ )
				{
					existTree.IsActive = true;
				}
				return;
			}
		}
		Tree tree = Instantiate(TreePrefab.gameObject, TreeParent.transform).GetComponent<Tree>();
		TabButton tab = Instantiate(TabButtonPrefab.gameObject, TabParent.transform).GetComponent<TabButton>();
		tree.Load(path, tab);
		trees_.Add(tree);
		tree.IsActive = isActive;
	}

	#endregion


	#region events

	public void OnTreeActivated(Tree tree)
	{
		if( activeTree_ != null && tree != activeTree_ )
		{
			activeTree_.IsActive = false;
		}
		activeTree_ = tree;
	}

	public void OnTreeClosed(Tree tree)
	{
		int index = trees_.IndexOf(tree);
		trees_.Remove(tree);
		if( trees_.Count == 0 )
		{
			NewFile();
		}
		else if( tree == activeTree_ )
		{
			if( index >= trees_.Count ) index = trees_.Count - 1;
			trees_[index].IsActive = true;
		}
	}

	#endregion


	#region settings

	void LoadSettings()
	{
		if( settingFile_.Exists == false )
		{
			return;
		}

		StreamReader reader = new StreamReader(settingFile_.OpenRead());
		string text = null;
		while( (text = reader.ReadLine()) != null )
		{
			if( text.EndsWith(".dtml") && File.Exists(text) )
			{
				LoadTree(text, isActive: activeTree_ == null);
			}
		}
		if( activeTree_ == null )
		{
			NewFile();
		}
		reader.Close();
	}

	void SaveSettings()
	{
		StreamWriter writer = new StreamWriter(settingFile_.FullName, append: false);
		foreach(Tree tree in trees_)
		{
			if( tree.File != null )
			{
				writer.WriteLine(tree.File.FullName.ToString());
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

#if UNITY_EDITOR
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
					foreach( string path in DragAndDrop.paths )
					{
						if( path.EndsWith(".dtml") )
						{
							LoadTree(path, isActive: true);
							break;
						}
					}
					evt.Use();
				}
				break;
			}
		}
	}
#elif false // UNITY_STANDALONE_WIN

	// 参考：
	// Unity(x86/x64)でWindowsメッセージを受け取る方法 - Qiita http://qiita.com/DandyMania/items/d1404c313f67576d395f
	// how to get the drag&drop url in unity? | Unity Community https://forum.unity3d.com/threads/how-to-get-the-drag-drop-url-in-unity.23405/

	const int GWL_WNDPROC = -4;

	void Awake()
	{
		Init();
	}

	void OnGUI()
	{
		// ウィンドウハンドルが切り替わったので初期化 
		if( hMainWindow.Handle == IntPtr.Zero )
		{
			Init();
		}
	}

	void OnDestroy()//OnApplicationQuit()
	{
		Term();
	}

	#region hook window event

	private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
	private HandleRef hMainWindow;
	private IntPtr newWndProcPtr;
	private IntPtr oldWndProcPtr;
	private WndProcDelegate newWndProc;

	void Init()
	{
		// ウインドウプロシージャをフックする
		hMainWindow = new HandleRef(null, GetActiveWindow());
		newWndProc = new WndProcDelegate(WndProc);
		newWndProcPtr = Marshal.GetFunctionPointerForDelegate(newWndProc);
		oldWndProcPtr = SetWindowLongPtr(hMainWindow, GWL_WNDPROC, newWndProcPtr);
		DragAcceptFiles(hMainWindow.Handle, true);
	}

	void Term()
	{
		// todo: 終了時にクラッシュするので、どうすればいいかわからん。
		SetWindowLongPtr(hMainWindow, GWL_WNDPROC, oldWndProcPtr);
		hMainWindow = new HandleRef(null, IntPtr.Zero);
		oldWndProcPtr = IntPtr.Zero;
		newWndProcPtr = IntPtr.Zero;
		newWndProc = null;
	}
	
	[DllImport("user32.dll")]
	static extern System.IntPtr GetActiveWindow();

	[DllImport("user32.dll", EntryPoint = "SetWindowLong")]
	private static extern int SetWindowLong32(HandleRef hWnd, int nIndex, int dwNewLong);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
	private static extern IntPtr SetWindowLongPtr64(HandleRef hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport("user32.dll", EntryPoint = "DefWindowProcA")]
	private static extern IntPtr DefWindowProc(IntPtr hWnd, uint wMsg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll", EntryPoint = "CallWindowProc")]
	private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint wMsg, IntPtr wParam, IntPtr lParam);

	public static IntPtr SetWindowLongPtr(HandleRef hWnd, int nIndex, IntPtr dwNewLong)
	{
		if( IntPtr.Size == 8 )
		{
			return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
		}
		else
		{
			return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
		}
	}

	private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
	{
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

	private void HandleDropFiles(IntPtr hDrop)
	{
		const int MAX_PATH = 260;

		var count = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);

		for( uint i = 0; i < count; i++ )
		{
			int size = (int)DragQueryFile(hDrop, i, null, 0);

			var filename = new StringBuilder(size + 1);
			DragQueryFile(hDrop, i, filename, MAX_PATH);
			
			if( filename.ToString().EndsWith(".dtml") )
			{
				LoadTree(filename.ToString());
				break;
			}
		}

		DragFinish(hDrop);
	}


	#endregion

#endif

	#endregion
}
