using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ModalDialog : MonoBehaviour {

	public enum DialogResult
	{
		Yes,
		No,
		Cancel,
	}

	public delegate void DialogCallback(DialogResult result);

	public Text DialogText;

	public DialogResult Result { get; private set; }

	DialogCallback callback_;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	public void Show(string text, DialogCallback callback)
	{
		callback_ = callback;
		DialogText.text = text;
		gameObject.SetActive(true);
	}

	public void YesClicked()
	{
		Result = DialogResult.Yes;
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
