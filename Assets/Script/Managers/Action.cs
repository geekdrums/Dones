using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public abstract class ActionBase
{
	public abstract void Execute();
	public abstract void Undo();
	public abstract void Redo();
	
	public ActionManagerProxy Proxy { get; set; }
}

public class Action : ActionBase
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

	public override void Execute()
	{
		execute_();
	}

	public override void Undo()
	{
		undo_();
	}

	public override void Redo()
	{
		redo_();
	}
}

public class ChainAction : ActionBase
{
	List<ActionBase> chain_ = new List<ActionBase>();

	public ChainAction()
	{
	}

	public bool HasAction()
	{
		return chain_.Count > 0;
	}

	public void AddChain(ActionBase action)
	{
		chain_.Add(action);
	}

	public override void Execute()
	{
		foreach( ActionBase action in chain_ )
		{
			action.Execute();
		}
	}

	public override void Undo()
	{
		for( int i = chain_.Count - 1; i >= 0; --i )
		{
			chain_[i].Undo();
		}
	}

	public override void Redo()
	{
		foreach( ActionBase action in chain_ )
		{
			action.Redo();
		}
	}
}