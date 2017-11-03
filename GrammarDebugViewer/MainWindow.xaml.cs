using GrammarDebugViewer.View;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace GrammarDebugViewer
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public DeferableObservableCollection<DebugFrame> RootFrames { get; } = new DeferableObservableCollection<DebugFrame>();

		public MainWindow()
		{
			DataContext = this;
			InitializeComponent();
		}

		private void BrowseButtonClick(object sender, RoutedEventArgs args)
		{
			var dlg = new OpenFileDialog();
			dlg.Filter = "Debug files (*.debug) | *.debug";

			if (dlg.ShowDialog() == true)
			{
				var chosen = dlg.FileName;

				try
				{
					XDocument doc = XDocument.Load(chosen);

					var entityTable = new Dictionary<int, XElement>();
					var entityTableEl = doc.Root.Element("EntityTable");
					foreach (var el in entityTableEl.Elements())
					{
						var index = int.Parse(el.Name.ToString().Remove(0, 1));
						entityTable[index] = el.Elements().FirstOrDefault();
					}

					var frames = new List<DebugFrame>();
					foreach (var frameEl in doc.Root.Elements())
					{
						if (frameEl.Name == "EntityTable") continue;

						var frame = new DebugFrame(null);
						frame.Parse(frameEl, entityTable);

						frames.Add(frame);
					}

					DebugFrame.GridSize = new IntPoint(frames[0].Grid.GetLength(0), frames[0].Grid.GetLength(1));

					RootFrames.BeginChange();
					RootFrames.Clear();
					foreach (var frame in frames)
					{
						RootFrames.Add(frame);
					}
					RootFrames.EndChange();
				}
				catch (Exception ex)
				{

				}
			}
		}
	}
}
