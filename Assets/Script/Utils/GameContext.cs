using UnityEngine;
using System;
using System.Collections.Generic;

public static class GameContext
{
	public static Config Config;
	public static Camera MainCamera = GameObject.Find("Main Camera").GetComponent<Camera>();
}
