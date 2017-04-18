using System;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dones
{
	public class Line : IEnumerable<Line>, INotifyPropertyChanged, INotifyCollectionChanged
	{
		static int SpacesPerLevel = 40;

		// params

		public string Text { get; set; }
		public bool IsDone { get; set; }
		public bool IsFolded { get; set; }
		public int Level
		{
			get { return level_; }
			set
			{
				if( value < 0 || value == level_ ) return;

				level_ = value;
				foreach(Line line in children_)
				{
					line.Level = level_ + 1;
				}
				if( PropertyChanged != null )
				{
					PropertyChanged(this, new PropertyChangedEventArgs("Level"));
					PropertyChanged(this, new PropertyChangedEventArgs("LevelSpace"));
				}
			}
		}
		protected int level_;


		// tree params

		public Line Parent
		{
			get { return parent_; }
			set
			{
				if( parent_ != null )
				{
					parent_.Remove(this);
				}
				parent_ = value;
				if( parent_ != null )
				{
					parent_.Add(this);
				}
				else
				{
					Level = 0;
				}
			}
		}
		protected Line parent_;

		public Line this[int index]
		{
			get
			{
				return children_[index];
			}
			private set
			{
				children_[index] = value;
			}
		}
		public void Add(Line child)
		{
			if( child.parent_ != null )
			{
				child.parent_.Remove(child);
			}
			child.parent_ = this;
			children_.Add(child);
			child.Level = level_ + 1;
			OnCollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, child, children_.Count - 1));
		}
		public void Insert(int index, Line child)
		{
			if( child.parent_ != null )
			{
				child.parent_.Remove(child);
			}
			child.parent_ = this;
			children_.Insert(index, child);
			child.Level = level_ + 1;
			OnCollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, child, index));
		}
		public void Remove(Line child)
		{
			int indexInParent = child.IndexInParent;
			child.parent_ = null;
			children_.Remove(child);
			OnCollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, child, indexInParent));
		}

		public int IndexInParent { get { return parent_ != null ? parent_.children_.IndexOf(this) : -1; } }

		public IEnumerable<Line> GetVisibleLines()
		{
			if( Level >= 0 )
			{
				yield return this;
			}
			if( this.IsFolded == false )
			{
				foreach( Line child in children_ )
				{
					foreach( Line visibleLine in child.GetVisibleLines() )
					{
						yield return visibleLine;
					}
				}
			}
		}
		
		public IEnumerator<Line> GetEnumerator()
		{
			foreach( Line line in children_ )
			{
				yield return line;
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		protected List<Line> children_ = new List<Line>();

		// INotifyPropertyChanged
		public event PropertyChangedEventHandler PropertyChanged;
		// INotifyCollectionChanged
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		protected void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if( CollectionChanged != null )
			{
				CollectionChanged(sender, e);
			}
		}

		// xaml properties
		public int LevelSpace { get { return Level * SpacesPerLevel; } }
		public List<Line> Children { get { return children_; } }
		public bool HasVisibleChild { get { return IsFolded == false && Children.Count > 0; } }
		public Line LastVisibleLine
		{
			get
			{
				if( HasVisibleChild )
				{
					return children_[children_.Count - 1].LastVisibleLine;
				}
				else return this;
			}
		}

		public Line(string text, int level = 0)
		{
			Text = text;
			level_ = level;
			IsFolded = false;
			IsDone = false;
		}
	}
}
