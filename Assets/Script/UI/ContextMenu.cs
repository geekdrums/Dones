using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ContextMenu : MonoBehaviour {


    public Tree Tree;

	public Button NewTabButton;
	public Button DoneButton;
	public Button RepeatDoneButton;
	public Button AddTagButton;
	public Button CopyButton;
	public Button PasteButton;
	public Button CopyWithoutFormatButton;
	public Button FoldButton;
	public Button UnfoldButton;
	public Button FoldAllButton;
	public Button UnfoldAllButton;

	bool isOpening_;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update ()
	{
		if( isOpening_ && Input.GetMouseButtonDown(0) )
		{
			if( EventSystem.current.currentSelectedGameObject == null || EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform) == false )
			{
				Close();
			}
		}
	}

	public void Open(Vector3 position)
	{
		gameObject.SetActive(true);
		transform.position = position;
		isOpening_ = true;

		NewTabButton			.interactable = Tree.HasSelection == false && Tree.FocusedLine != null;
		DoneButton				.interactable = Tree.FocusedLine != null;
		RepeatDoneButton		.interactable = Tree.FocusedLine != null;
		AddTagButton			.interactable = Tree.FocusedLine != null;
		CopyButton				.interactable = Tree.HasSelection;
		PasteButton				.interactable = Tree.Clipboard != null && Tree.Clipboard != "";
		CopyWithoutFormatButton	.interactable = Tree.HasSelection;
		FoldButton				.interactable = Tree.FocusedLine != null;
		UnfoldButton			.interactable = Tree.FocusedLine != null;
		FoldAllButton			.interactable = Tree.FocusedLine != null;
		UnfoldAllButton			.interactable = Tree.FocusedLine != null;
	}

	public void Close()
	{
		gameObject.SetActive(false);
		isOpening_ = false;
	}

	public void OnClickNewTab()
    {
        if( Tree.HasSelection == false && Tree.FocusedLine != null )
        {
            GameContext.Window.AddTab(Tree.FocusedLine);
		}
		Close();
	}

    public void OnClickDone()
    {
		if( Tree.FocusedLine != null )
		{
			Tree.OnCtrlSpaceInput();
		}
		Close();
	}

	public void OnClickRepeatDone()
	{
		if( Tree.FocusedLine != null )
		{
			Tree.OnCtrlShiftSpaceInput();
		}
		Close();
	}

	public void OnClickCopy()
    {
        Tree.Copy(withformat: true);
		Close();
	}

    public void OnClickCopyWithoutFormat()
    {
        Tree.Copy(withformat: false);
		Close();
	}

    public void OnClickPaste()
    {
        Tree.Paste();
		Close();
	}

	public void OnClickFold()
	{
		if( Tree.FocusedLine != null )
		{
			Tree.OnCtrlArrowInput(KeyCode.UpArrow);
		}
		Close();
	}

	public void OnClickUnfold()
	{
		if( Tree.FocusedLine != null )
		{
			Tree.OnCtrlArrowInput(KeyCode.DownArrow);
		}
		Close();
	}

	public void OnClickFoldAll()
	{
		if( Tree.FocusedLine != null )
		{
			Tree.OnCtrlShiftArrowInput(KeyCode.UpArrow);
		}
		Close();
	}

	public void OnClickUnfoldAll()
	{
		if( Tree.FocusedLine != null )
		{
			Tree.OnCtrlShiftArrowInput(KeyCode.DownArrow);
		}
		Close();
	}

}
