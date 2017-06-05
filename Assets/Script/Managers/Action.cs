using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public interface IAction
{
	void Execute();
	void Undo();
	void Redo();
}

public class Action : IAction
{
	System.Action execute_;
	System.Action undo_;
	System.Action redo_;


	public Action(System.Action execute)
	{
		execute_ = execute;
		undo_ = execute;
		redo_ = execute;
	}

	public Action(System.Action execute, System.Action undo)
	{
		execute_ = execute;
		undo_ = undo;
		redo_ = execute;
	}
	
	public Action(System.Action execute, System.Action undo, System.Action redo)
	{
		execute_ = execute;
		undo_ = undo;
		redo_ = redo;
	}

	public void Execute()
	{
		execute_();
	}

	public void Undo()
	{
		undo_();
	}

	public void Redo()
	{
		redo_();
	}
}

public class ChainAction : IAction
{
	List<IAction> chain_ = new List<IAction>();

	public ChainAction()
	{
	}

	public bool HasAction()
	{
		return chain_.Count > 0;
	}

	public void AddChain(IAction action)
	{
		chain_.Add(action);
	}

	public void Execute()
	{
		foreach( IAction action in chain_ )
		{
			action.Execute();
		}
	}

	public void Undo()
	{
		for( int i = chain_.Count - 1; i >= 0; --i )
		{
			chain_[i].Undo();
		}
	}

	public void Redo()
	{
		foreach( IAction action in chain_ )
		{
			action.Redo();
		}
	}
}