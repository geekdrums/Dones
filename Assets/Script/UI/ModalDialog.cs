using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ModalDialog : MonoBehaviour {

	public enum DialogResult
	{
		Yes,
		No,
		OK,
		Cancel,
	}

	public enum DialogType
	{
		YesNoCancel,
		YesNo,
		OKCancel,
		OK,
	}

	public delegate void DialogCallback(DialogResult result);

	public Text DialogText;
	public Button YesButton;
	public Button OKButton;
	public Button NoButton;
	public Button CancelButton;

	public DialogResult Result { get; private set; }

	DialogCallback callback_;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	public void Show(string text, DialogType type, DialogCallback callback)
	{
		callback_ = callback;
		DialogText.text = text;
		gameObject.SetActive(true);
		switch(type)
		{
		case DialogType.YesNoCancel:
			YesButton.gameObject.SetActive(true);
			NoButton.gameObject.SetActive(true);
			CancelButton.gameObject.SetActive(true);
			OKButton.gameObject.SetActive(false);
			break;
		case DialogType.YesNo:
			YesButton.gameObject.SetActive(true);
			NoButton.gameObject.SetActive(true);
			CancelButton.gameObject.SetActive(false);
			OKButton.gameObject.SetActive(false);
			break;
		case DialogType.OKCancel:
			YesButton.gameObject.SetActive(false);
			NoButton.gameObject.SetActive(false);
			CancelButton.gameObject.SetActive(true);
			OKButton.gameObject.SetActive(true);
			break;
		case DialogType.OK:
			YesButton.gameObject.SetActive(false);
			NoButton.gameObject.SetActive(false);
			CancelButton.gameObject.SetActive(false);
			OKButton.gameObject.SetActive(true);
			break;
		}
	}

	public void YesClicked()
	{
		Result = DialogResult.Yes;
		gameObject.SetActive(false);
	}
	public void OKClicked()
	{
		Result = DialogResult.OK;
		gameObject.SetActive(false);
	}
	public void NoClicked()
	{
		Result = DialogResult.No;
		gameObject.SetActive(false);
	}
	public void CancelClicked()
	{
		Result = DialogResult.Cancel;
		gameObject.SetActive(false);
	}

	void OnDisable()
	{
		if( callback_ != null )
		{
			callback_(Result);
			callback_ = null;
		}
	}
}
