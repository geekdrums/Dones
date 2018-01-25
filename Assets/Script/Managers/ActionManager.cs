using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
	List<ActionBase> actions_ = new List<ActionBase>();
	int currentIndex_ = -1;
	Stack<ChainAction> chainStack_ = new Stack<ChainAction>();

	public event EventHandler<ActionEventArgs> Executed;
	public event EventHandler ChainStarted;
	public event EventHandler ChainEnded;
	public bool IsChaining { get { return chainStack_.Count > 0; } }

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
			if( currentIndex_ + 1 < actions_.Count )
			{
				actions_.RemoveRange(currentIndex_ + 1, actions_.Count - (currentIndex_ + 1));
			}
			++currentIndex_;
			actions_.Add(action);
		}
	}

	public void Undo()
	{
		if( 0 <= currentIndex_ && currentIndex_ < actions_.Count )
		{
			ActionBase action = actions_[currentIndex_--];
			if( action is ChainAction )
			{
				OnChainStarted();
				if( action.Proxy != null )
				{
					action.Proxy.OnChainStarted();
				}
				action.Undo();
				OnChainEnded();
				if( action.Proxy != null )
				{
					action.Proxy.OnChainEnded();
				}
			}
			else
			{
				action.Undo();
				if( Executed != null )
					Executed(this, new ActionEventArgs(action));
				if( action.Proxy != null )
				{
					action.Proxy.OnExecuted(new ActionEventArgs(action));
				}
			}
		}
	}

	public void Redo()
	{
		if( currentIndex_ + 1 < actions_.Count )
		{
			ActionBase action = actions_[++currentIndex_];
			if( action is ChainAction )
			{
				OnChainStarted();
				if( action.Proxy != null )
				{
					action.Proxy.OnChainStarted();
				}
				action.Redo();
				OnChainEnded();
				if( action.Proxy != null )
				{
					action.Proxy.OnChainEnded();
				}
			}
			else
			{
				action.Redo();
				if( Executed != null )
					Executed(this, new ActionEventArgs(action));
				if( action.Proxy != null )
				{
					action.Proxy.OnExecuted(new ActionEventArgs(action));
				}
			}
		}
	}

	public void Clear()
	{
		actions_.Clear();
		currentIndex_ = -1;
		chainStack_.Clear();
	}

	public void StartChain()
	{
		if( chainStack_.Count == 0 )
		{
			OnChainStarted();
		}
		chainStack_.Push(new ChainAction());
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
				if( currentIndex_ + 1 < actions_.Count )
				{
					actions_.RemoveRange(currentIndex_ + 1, actions_.Count - (currentIndex_ + 1));
				}
				++currentIndex_;
				actions_.Add(lastChainAction);
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
		actionManager_.StartChain();
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