using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;

public class ControlManager : MonoBehaviour {

	// Use this for initialization
	void Start ()
	{
		GameContext.ControlManager = this;
		SubscribeKeyInput();
	}

	void SubscribeKeyInput()
	{
		KeyCode[] throttleKeys = new KeyCode[]
		{
			
#if UNITY_EDITOR
			KeyCode.Q,
			KeyCode.U,
#else
			KeyCode.Z,
			KeyCode.Y,
#endif
		};

		var updateStream = this.UpdateAsObservable();

		foreach( KeyCode key in throttleKeys )
		{
			// 最初の入力
			updateStream.Where(x => Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(key))
				.Merge(
			// 押しっぱなしにした時の自動連打
			updateStream.Where(x => Input.GetKey(KeyCode.LeftControl) && Input.GetKey(key))
				.Delay(TimeSpan.FromSeconds(GameContext.Config.ArrowStreamDelayTime))
				.ThrottleFirst(TimeSpan.FromSeconds(GameContext.Config.ArrowStreamIntervalTime))
				)
				.TakeUntil(this.UpdateAsObservable().Where(x => Input.GetKeyUp(key)))
				.RepeatUntilDestroy(this)
				.Subscribe(_ => OnThrottleInput(key));
		}
	}

	void OnThrottleInput(KeyCode key)
	{
		if( GameContext.CurrentActionManager != null )
		{
			switch( key )
			{
#if UNITY_EDITOR
			case KeyCode.Q:
				GameContext.CurrentActionManager.Undo();
				break;
			case KeyCode.U:
				GameContext.CurrentActionManager.Redo();
				break;
#else
			case KeyCode.Z:
				GameContext.CurrentActionManager.Undo();
				break;
			case KeyCode.Y:
				GameContext.CurrentActionManager.Redo();
				break;
#endif
			}
		}
	}

	// Update is called once per frame
	void Update ()
	{
	}
}
