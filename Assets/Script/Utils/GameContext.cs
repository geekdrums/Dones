﻿using UnityEngine;
using System;
using System.Collections.Generic;

public static class GameContext
{
	public static Camera MainCamera = GameObject.Find("Main Camera").GetComponent<Camera>();

	public static Config Config;

}