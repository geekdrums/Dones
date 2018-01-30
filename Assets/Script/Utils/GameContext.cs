using UnityEngine;
using System;
using System.Collections.Generic;

public static class GameContext
{
	public static Camera MainCamera = GameObject.Find("Main Camera").GetComponent<Camera>();

	public static Config Config;
	public static ControlManager ControlManager;
	public static ActionManager CurrentActionManager;
	public static Window Window;
	public static TagList TagList;
}
