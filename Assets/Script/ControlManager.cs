using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControlManager : MonoBehaviour {

	// Use this for initialization
	void Start () {
		GameContext.ControlManager = this;
	}
	
	// Update is called once per frame
	void Update () {
		if( GameContext.CurrentActionManager != null )
		{
			if( Input.GetKeyDown(KeyCode.Z) && Input.GetKey(KeyCode.LeftControl) )
			{
				GameContext.CurrentActionManager.Undo();
			}
			else if( Input.GetKeyDown(KeyCode.Y) && Input.GetKey(KeyCode.LeftControl) )
			{
				GameContext.CurrentActionManager.Redo();
			}
		}
	}
}
