using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class ActionEventArgs : EventArgs
{
	public IAction Action { get; set; }

	public ActionEventArgs(IAction action)
	{
		Action = action;
	}
}

public class ActionManager
{
	List<IAction> actions_ = new List<IAction>();
	int currentIndex_ = -1;
	Stack<ChainAction> chainStack_ = new Stack<ChainAction>();

	public event EventHandler<ActionEventArgs> Executed;
	public event EventHandler ChainStarted;
	public event EventHandler ChainEnded;
	public bool IsChaining { get { return chainStack_.Count > 0; } }

	public void Execute(IAction action)
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
			IAction action = actions_[currentIndex_--];
			if( action is ChainAction )
			{
				OnChainStarted();
				action.Undo();
				OnChainEnded();
			}
			else
			{
				action.Undo();
				if( Executed != null )
					Executed(this, new ActionEventArgs(action));
			}
		}
	}

	public void Redo()
	{
		if( currentIndex_ + 1 < actions_.Count )
		{
			IAction action = actions_[++currentIndex_];
			if( action is ChainAction )
			{
				OnChainStarted();
				action.Redo();
				OnChainEnded();
			}
			else
			{
				action.Redo();
				if( Executed != null )
					Executed(this, new ActionEventArgs(action));
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
