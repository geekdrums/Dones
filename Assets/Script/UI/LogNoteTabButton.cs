using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class LogNoteTabButton : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
	#region params

	public TreeNote OwnerNote { get; set; }
	
	public string Text
	{
		get { return textComponent_.text; }
		set
		{
			if( textComponent_ == null )
				textComponent_ = GetComponentInChildren<Text>();
			textComponent_.text = value;
		}
	}
	Text textComponent_;

	public Color Background { get { return image_.canvasRenderer.GetColor(); } set { image_.color = value; image_.CrossFadeColor(Color.white, 0.0f, true, true); } }
	Image image_;

	#endregion


	#region unity events

	void Start()
	{
		image_ = GetComponent<Image>();
		textComponent_ = GetComponentInChildren<Text>();
	}

	// Update is called once per frame
	void Update () {
		
	}

	#endregion


	#region click, close

	public void Close()
	{
		if( OwnerNote != null && OwnerNote.LogNote.IsEdited )
		{
			GameContext.Window.ModalDialog.Show(OwnerNote.LogNote.TitleText + "ログファイルへの変更を保存しますか？", this.CloseConfirmCallback);
			return;
		}

		DoClose();
	}

	void DoClose()
	{
		OwnerNote.LogNote.IsOpended = false;
	}

	void CloseConfirmCallback(ModalDialog.DialogResult result)
	{
		switch( result )
		{
		case ModalDialog.DialogResult.Yes:
			if( OwnerNote != null )
			{
				OwnerNote.LogNote.SaveLog();
			}
			DoClose();
			break;
		case ModalDialog.DialogResult.No:
			if( OwnerNote != null )
			{
				OwnerNote.LogNote.ReloadLog();
			}
			DoClose();
			break;
		case ModalDialog.DialogResult.Cancel:
			// do nothing
			break;
		}
	}


	#endregion


	#region drag

	public void OnBeginDrag(PointerEventData eventData)
	{
	}

	public void OnDrag(PointerEventData eventData)
	{
		if( OwnerNote != null )
		{
			OwnerNote.OnLogSplitLineDragging(this, eventData);
		}
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if( OwnerNote != null )
		{
			OwnerNote.OnLogSplitLineEndDrag(this, eventData);
		}
	}

	#endregion
}
