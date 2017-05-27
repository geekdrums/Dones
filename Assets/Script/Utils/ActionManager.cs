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
	bool isChain_ = false;
	ChainAction lastChainAction_;

	public event EventHandler<ActionEventArgs> Executed;
	public event EventHandler ChainStarted;
	public event EventHandler ChainEnded;

	public void Execute(IAction action)
	{
		action.Execute();
		if( Executed != null )
			Executed(this, new ActionEventArgs(action));

		if( isChain_ )
		{
			lastChainAction_.AddChain(action);
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
		isChain_ = false;
		lastChainAction_ = null;
	}

	public void StartChain()
	{
		if( isChain_ )
		{
			UnityEngine.Debug.LogError("前のチェインが終了していません");
			actions_.Add(lastChainAction_);
		}
		isChain_ = true;
		lastChainAction_ = new ChainAction();
		OnChainStarted();
	}

	public void EndChain()
	{
		if( isChain_ == false || lastChainAction_ == null )
		{
			UnityEngine.Debug.LogError("チェインが開始していません");
			lastChainAction_ = new ChainAction();
		}
		isChain_ = false;
		if( lastChainAction_.HasAction() )
		{
			if( currentIndex_ + 1 < actions_.Count )
			{
				actions_.RemoveRange(currentIndex_ + 1, actions_.Count - (currentIndex_ + 1));
			}
			++currentIndex_;
			actions_.Add(lastChainAction_);
		}

		OnChainEnded();
		lastChainAction_ = null;
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
