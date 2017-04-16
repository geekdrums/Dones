using System;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dones
{
	class LineTemplateSelector : DataTemplateSelector
	{
		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			Line line = item as Line;
			if( line.HasVisibleChild )
			{
				return ((FrameworkElement)container).FindResource("TextLineContainerTemplate") as DataTemplate;
			}
			else
			{
				return ((FrameworkElement)container).FindResource("TextLineTemplate") as DataTemplate;
			}
		}
	}
}
