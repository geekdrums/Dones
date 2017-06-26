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
		menuObject_ = transform.Find("Menu").gameObject;
	}
	
	// Update is called once per frame
	void Update ()
	{
		if( isOpening_ && Input.GetMouseButtonDown(0) )
		{
			if( currentSelectionState != SelectionState.Highlighted )
			{
				Close();
			}
		}
	}

	public void OnClick()
	{
		isOpening_ = !isOpening_;
		if( isOpening_ )
		{
			menuObject_.SetActive(true);
		}
		else
		{
			Close();
		}
	}


	public void Close()
	{
		isOpening_ = false;
		foreach(Button menubutton in menuObject_.GetComponentsInChildren<Button>())
		{
			Transform subMenu = menubutton.transform.Find("SubMenu");
			if( subMenu != null )
			{
				subMenu.gameObject.SetActive(false);
			}
		}
		menuObject_.SetActive(false);
	}

}
