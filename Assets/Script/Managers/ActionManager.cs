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

public class ChainActionEventArgs : EventArgs
{
	public ChainAction Action { get; set; }

	public ChainActionEventArgs(ChainAction action)
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
	public event EventHandler<ChainActionEventArgs> ChainStarted;
	public event EventHandler<ChainActionEventArgs> ChainEnded;
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
				OnChainStarted(action as ChainAction);
				if( action.Proxy != null )
					action.Proxy.OnChainStarted(action as ChainAction);

				action.Undo();
				if( Executed != null )
					Executed(this, new ActionEventArgs(action));
				if( action.Proxy != null )
					action.Proxy.OnExecuted(new ActionEventArgs(action));

				OnChainEnded(action as ChainAction);
				if( action.Proxy != null )
					action.Proxy.OnChainEnded(action as ChainAction);
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
				OnChainStarted(action as ChainAction);
				if( action.Proxy != null )
					action.Proxy.OnChainStarted(action as ChainAction);

				action.Redo();
				if( Executed != null )
					Executed(this, new ActionEventArgs(action));
				if( action.Proxy != null )
					action.Proxy.OnExecuted(new ActionEventArgs(action));

				OnChainEnded(action as ChainAction);
				if( action.Proxy != null )
					action.Proxy.OnChainEnded(action as ChainAction);
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

	public ChainAction StartChain(ActionManagerProxy proxy = null)
	{
		ChainAction chainAction = new ChainAction();
		chainAction.Proxy = proxy;
		chainStack_.Push(chainAction);
		if( chainStack_.Count == 0 )
		{
			OnChainStarted(chainAction);
		}
		return chainAction;
	}

	public ChainAction EndChain()
	{
		if( chainStack_.Count == 0 )
		{
			UnityEngine.Debug.LogError("チェインが開始していません");
			return null;
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
				OnChainEnded(lastChainAction);
			}
		}
		else
		{
			if( chainStack_.Count == 0 )
			{
				OnChainEnded(lastChainAction);
			}
		}

		return lastChainAction;
	}

	private void OnChainStarted(ChainAction action)
	{
		if( ChainStarted != null )
			ChainStarted(this, new ChainActionEventArgs(action));
	}

	private void OnChainEnded(ChainAction action)
	{
		if( ChainEnded != null )
			ChainEnded(this, new ChainActionEventArgs(action));
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
	public event EventHandler<ChainActionEventArgs> ChainStarted;
	public event EventHandler<ChainActionEventArgs> ChainEnded;
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
		bool wasActionChaining = actionManager_.IsChaining;
		ChainAction action = actionManager_.StartChain(proxy: this);
		if( wasActionChaining == false )
		{
			OnChainStarted(action);
		}
	}

	public void EndChain()
	{
		ChainAction action = actionManager_.EndChain();
		if( actionManager_.IsChaining == false )
		{
			OnChainEnded(action);
		}
	}

	public void OnExecuted(ActionEventArgs e)
	{
		Executed(this, e);
	}

	public void OnChainStarted(ChainAction action)
	{
		ChainStarted(this, new ChainActionEventArgs(action));
	}

	public void OnChainEnded(ChainAction action)
	{
		ChainEnded(this, new ChainActionEventArgs(action));
	}
}