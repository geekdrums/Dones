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
	public Tree TreePrefab;
	public GameObject CanvasContent;

	List<Tree> trees_ = new List<Tree>();

	// Use this for initialization
	void Start () {

	}

	// Update is called once per frame
	void Update () {

	}

	void OpenFile()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Filter = "dones file (*.dtml)|*.dtml";
		DialogResult dialogResult = openFileDialog.ShowDialog();
		if( dialogResult == DialogResult.OK )
		{
			LoadTree(openFileDialog.FileName);
		}
	}

	void LoadTree(string path)
	{
		Tree tree = Instantiate(TreePrefab.gameObject, CanvasContent.transform).GetComponent<Tree>();
		tree.Load(path);
		trees_.Add(tree);
	}

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
							LoadTree(path);
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

}
