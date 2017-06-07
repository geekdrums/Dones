using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MenuButton : Button {

	bool isOpening_ = false;
	GameObject menuObject_;

	// Use this for initialization
	protected override void Start () {
		base.Start();
		onClick.AddListener(this.OnClick);
		menuObject_ = transform.FindChild("Menu").gameObject;
	}
	
	// Update is called once per frame
	void Update ()
	{
		if( isOpening_ && Input.GetMouseButtonDown(0) )
		{
			if( currentSelectionState != SelectionState.Highlighted )
			{
				OnClick();
			}
		}
	}

	public void OnClick()
	{
		isOpening_ = !isOpening_;
		menuObject_.SetActive(isOpening_);
	}


	public void Close()
	{
		if( isOpening_ )
		{
			isOpening_ = false;
			menuObject_.SetActive(false);
		}
	}
}
