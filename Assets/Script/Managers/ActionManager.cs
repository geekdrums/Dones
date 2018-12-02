using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class ActionEventArgs : EventArgs
{
	public ActionBase Action { get; set; }

	public ActionEventArgs(ActionBase action)
	{
		Action = action;
	}
}

public class ActionManager
{
	List<ActionBase> undoActions_ = new List<ActionBase>();
	List<ActionBase> redoActions_ = new List<ActionBase>();
	int undoIndex_ = -1;
	int redoIndex_ = -1;
	Line titleLine_;
	Stack<ChainAction> chainStack_ = new Stack<ChainAction>();

	public event EventHandler<ActionEventArgs> Executed;
	public event EventHandler ChainStarted;
	public event EventHandler ChainEnded;
	public bool IsChaining { get { return chainStack_.Count > 0; } }

	public void SetTitleLine(Line titleLine)
	{
		titleLine_ = titleLine;
		undoIndex_ = undoActions_.Count - 1;
		redoIndex_ = redoActions_.Count - 1;
	}

	public void Execute(ActionBase action)
	{
		action.Execute();
		if( Executed != null )
			Executed(this, new ActionEventArgs(action));

		if( chainStack_.Count > 0 )
		{
			chainStack_.Peek().AddChain(action);
		}
		else
		{
			if( redoIndex_ >= 0 )
			{
                // 現在のtitleLine以下に関連するアクションは分岐してしまうのでRedoツリーから消す
                redoActions_.RemoveAll((ActionBase a) => a.IsRelatedTo(titleLine_));
				redoIndex_ = -1;
			}
			undoActions_.Add(action);
			undoIndex_ = undoActions_.Count - 1;
		}
	}

	public void Undo()
	{
		if( 0 <= undoIndex_ && undoIndex_ < undoActions_.Count )
		{
			ActionBase action = undoActions_[undoIndex_];
            // undoツリーから、現在のtitleLine以下に関係するアクションを取得する
			while( action.IsRelatedTo(titleLine_) == false )
			{
				if( undoIndex_ <= 0 )
				{
					return;
				}
				action = undoActions_[--undoIndex_];
            }
            if (action is ChainAction && titleLine_.IsChildOf((action as ChainAction).LeastCommonParentLine))
            {
                Debug.Log("can't undo " + action.ToString());
                return;
            }

            --undoIndex_;
			undoActions_.Remove(action);
			redoActions_.Add(action);
			redoIndex_ = redoActions_.Count - 1;
			if( action is ChainAction )
			{
				OnChainStarted();
				if( action.Proxy != null )
					action.Proxy.OnChainStarted();

				action.Undo();
				if( Executed != null )
					Executed(this, new ActionEventArgs(action));
				if( action.Proxy != null )
					action.Proxy.OnExecuted(new ActionEventArgs(action));

				OnChainEnded();
				if( action.Proxy != null )
					action.Proxy.OnChainEnded();
			}
			else
			{
				action.Undo();
				if( Executed != null )
					Executed(this, new ActionEventArgs(action));
				if( action.Proxy != null )
					action.Proxy.OnExecuted(new ActionEventArgs(action));
			}
		}
	}

	public void Redo()
	{
		if( 0 <= redoIndex_ && redoIndex_ < redoActions_.Count )
		{
			ActionBase action = redoActions_[redoIndex_];
			while( action.IsRelatedTo(titleLine_) == false )
			{
				if( redoIndex_ <= 0 )
				{
					return;
				}
				action = redoActions_[--redoIndex_];
            }
            if (action is ChainAction && titleLine_.IsChildOf((action as ChainAction).LeastCommonParentLine))
            {
                Debug.Log("can't redo " + action.ToString());
                return;
            }

            --redoIndex_;
			redoActions_.Remove(action);
			undoActions_.Add(action);
			undoIndex_ = undoActions_.Count - 1;
			if( action is ChainAction )
			{
				OnChainStarted();
				if( action.Proxy != null )
					action.Proxy.OnChainStarted();

				action.Redo();
				if( Executed != null )
					Executed(this, new ActionEventArgs(action));
				if( action.Proxy != null )
					action.Proxy.OnExecuted(new ActionEventArgs(action));

				OnChainEnded();
				if( action.Proxy != null )
					action.Proxy.OnChainEnded();
			}
			else
			{
				action.Redo();
				if( Executed != null )
					Executed(this, new ActionEventArgs(action));
				if( action.Proxy != null )
					action.Proxy.OnExecuted(new ActionEventArgs(action));
			}
		}
	}

	public void Clear()
	{
		undoActions_.Clear();
		redoActions_.Clear();
		undoIndex_ = -1;
		redoIndex_ = -1;
		chainStack_.Clear();
	}

	public void StartChain(ActionManagerProxy proxy = null)
	{
		if( chainStack_.Count == 0 )
		{
			OnChainStarted();
		}
		ChainAction chainAction = new ChainAction();
		chainAction.Proxy = proxy;
		chainStack_.Push(chainAction);
	}

	public void EndChain()
	{
		if( chainStack_.Count == 0 )
		{
			UnityEngine.Debug.LogError("チェインが開始していません");
			return;
		}

		ChainAction lastChainAction = chainStack_.Pop();
		if( lastChainAction.HasAction() )
		{
			if( chainStack_.Count > 0 )
			{
				chainStack_.Peek().AddChain(lastChainAction);
			}
			else
			{
				if( redoIndex_ >= 0 )
                {
                    redoActions_.RemoveAll((ActionBase a) => a.IsRelatedTo(titleLine_));
                    redoIndex_ = -1;
				}
				undoActions_.Add(lastChainAction);
				lastChainAction.CheckLeastCommonParent();
				undoIndex_ = undoActions_.Count - 1;
				OnChainEnded();
			}
		}
		else
		{
			if( chainStack_.Count == 0 )
			{
				OnChainEnded();
			}
		}
	}

	private void OnChainStarted()
	{
		if( ChainStarted != null )
			ChainStarted(this, EventArgs.Empty);
	}

	private void OnChainEnded()
	{
		if( ChainEnded != null )
			ChainEnded(this, EventArgs.Empty);
	}
}

public class ActionManagerProxy
{
	private ActionManager actionManager_;
	public static explicit operator ActionManager(ActionManagerProxy proxy)
	{
		return proxy.actionManager_;
	}

	public event EventHandler<ActionEventArgs> Executed;
	public event EventHandler ChainStarted;
	public event EventHandler ChainEnded;
	public bool IsChaining { get { return actionManager_.IsChaining; } }

	public ActionManagerProxy(ActionManager actionManager)
	{
		actionManager_ = actionManager;
	}

	public void Execute(ActionBase action)
	{
		action.Proxy = this;
		actionManager_.Execute(action);
		OnExecuted(new ActionEventArgs(action));
	}
	
	public void Clear()
	{
		actionManager_.Clear();
	}

	public void StartChain()
	{
		if( actionManager_.IsChaining == false )
		{
			OnChainStarted();
		}
		actionManager_.StartChain(proxy: this);
	}

	public void EndChain()
	{
		actionManager_.EndChain();
		if( actionManager_.IsChaining == false )
		{
			OnChainEnded();
		}
	}

	public void OnExecuted(ActionEventArgs e)
	{
		Executed(this, e);
	}

	public void OnChainStarted()
	{
		ChainStarted(this, EventArgs.Empty);
	}

	public void OnChainEnded()
	{
		ChainEnded(this, EventArgs.Empty);
	}
}