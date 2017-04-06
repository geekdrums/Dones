using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Dones
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public ObservableCollection<Line> VisibleLines;

		public MainWindow()
		{
			InitializeComponent();

			VisibleLines = new ObservableCollection<Line>();
			VisibleLines.Add(new Line("Hello World"));
			VisibleLines.Add(new Line("Hello"));
			VisibleLines.Add(new Line("Hello"));
			VisibleLines.Add(new Line("Hello"));

			TreeLine.ItemsSource = VisibleLines;
		}
	}
}
