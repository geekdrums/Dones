using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public abstract class ActionBase
{
	public abstract void Execute();
	public abstract void Undo();
	public abstract void Redo();
	public ActionManagerProxy Proxy { get; set; }
	public abstract IEnumerable<Line> GetTargetLines();

    public virtual bool IsRelatedTo(Line titleLine)
    {
		foreach( Line line in GetTargetLines() )
		{
			Line targetLine = line;

			if( line.Tree is LogTree )
			{
				targetLine = (line.Tree as LogTree).GetOriginalLine(line);
				if( targetLine == null )
				{
					continue;
				}
			}

			if( targetLine.HasBeenChildOrItselfOf(titleLine) )
			{
				return true;
			}
		}
        return false;
    }
}

public class LineAction : ActionBase
{
	protected System.Action execute_;
	protected System.Action undo_;
	protected System.Action redo_;
	public Line[] TargetLines { get; protected set; }
	public override IEnumerable<Line> GetTargetLines()
	{
		return TargetLines.AsEnumerable<Line>();
	}

	public LineAction(System.Action execute, System.Action undo, System.Action redo, params Line[] targetLines)
	{
		execute_ = execute;
		undo_ = undo;
		redo_ = redo;
        TargetLines = targetLines;
	}

	public LineAction(System.Action execute, System.Action undo, params Line[] targetLines)
		: this(execute, undo, execute, targetLines)
	{
	}

	public LineAction(System.Action execute, params Line[] targetLines)
		: this(execute, execute, execute, targetLines)
	{
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

	public override string ToString()
	{
		StringBuilder builder = new StringBuilder();
		foreach( Line line in TargetLines )
		{
			builder.AppendLine(line.GetTreePath().ToString());
		}
		return builder.ToString();
	}
}

public class ChainAction : ActionBase
{
	List<ActionBase> chain_ = new List<ActionBase>();
    public Line LeastCommonParentLine { get; protected set; }

    public ChainAction()
	{
	}

	public bool HasAction()
	{
		return chain_.Count > 0;
	}


	public override IEnumerable<Line> GetTargetLines()
	{
		foreach( ActionBase action in chain_ )
		{
			foreach( Line targetLine in action.GetTargetLines() )
			{
				yield return targetLine;
			}
		}
	}
	public override bool IsRelatedTo(Line titleLine)
    {
        foreach (ActionBase action in chain_)
        {
            if (action.IsRelatedTo(titleLine))
            {
                return true;
            }
        }
        return false;
    }

    public void CheckLeastCommonParent()
	{
		LeastCommonParentLine = Line.GetLeastCommonParent(GetTargetLines().ToArray<Line>());
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


    public override string ToString()
    {
        StringBuilder builder = new StringBuilder();
		foreach( ActionBase action in chain_ )
		{
			builder.AppendLine(action.ToString());
		}
        return builder.ToString();
    }
}