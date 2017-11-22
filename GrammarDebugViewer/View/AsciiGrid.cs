using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GrammarDebugViewer.View
{
	public class AsciiGrid : Control, INotifyPropertyChanged
	{
		//-----------------------------------------------------------------------
		public Point ViewPos { get; set; }

		//-----------------------------------------------------------------------
		public int PixelsATile { get; set; } = 10;

		//-----------------------------------------------------------------------
		public Dictionary<string, Brush> Brushes = new Dictionary<string, Brush>();
		private Brush GetBrush(string col)
		{
			Brush brush;
			if (!Brushes.TryGetValue(col, out brush))
			{
				brush = new SolidColorBrush(col.ToColour().Value);
				brush.Freeze();
				Brushes[col] = brush;
			}

			return brush;
		}

		//-----------------------------------------------------------------------
		protected Brush InactiveBrush
		{
			get
			{
				if (m_inActiveBrush == null)
				{
					m_inActiveBrush = new SolidColorBrush("30,30,30".ToColour().Value);
					m_inActiveBrush.Freeze();
				}

				return m_inActiveBrush;
			}
		}
		private Brush m_inActiveBrush;

		//-----------------------------------------------------------------------
		protected Brush BackgroundBrush { get { return (Application.Current.TryFindResource("WindowBackgroundBrush") as SolidColorBrush); } }

		//-----------------------------------------------------------------------
		protected Brush UsedAreaBrush { get { return (Application.Current.TryFindResource("BackgroundDarkBrush") as SolidColorBrush); } }

		//-----------------------------------------------------------------------
		protected Brush GridBrush { get { return (Application.Current.TryFindResource("BackgroundNormalBrush") as SolidColorBrush); } }

		//-----------------------------------------------------------------------
		protected Brush IndicatorBrush { get { return (Application.Current.TryFindResource("BorderDarkBrush") as SolidColorBrush); } }

		//-----------------------------------------------------------------------
		protected Brush SelectedBrush { get { return (Application.Current.TryFindResource("SelectionBorderBrush") as SolidColorBrush); } }

		//-----------------------------------------------------------------------
		protected Color SelectedColour { get { return (Color)(Application.Current.TryFindResource("SelectionBorderColour")); } }

		//-----------------------------------------------------------------------
		protected Brush UnselectedBrush { get { return (Application.Current.TryFindResource("BorderLightBrush") as SolidColorBrush); } }

		//-----------------------------------------------------------------------
		protected Brush FontBrush { get { return (Application.Current.TryFindResource("FontDarkBrush") as SolidColorBrush); } }

		//-----------------------------------------------------------------------
		private DebugTile[,] Grid { get; set; }

		//-----------------------------------------------------------------------
		private int GridWidth { get { return Grid?.GetLength(0) ?? 0; } }
		private int GridHeight { get { return Grid?.GetLength(1) ?? 0; } }

		//-----------------------------------------------------------------------
		private IntPoint? SelectedPoint
		{
			get { return m_selectedPoint; }
			set
			{
				m_selectedPoint = value;
				RaisePropertyChangedEvent("SelectedTile");
			}
		}
		private IntPoint? m_selectedPoint;

		//-----------------------------------------------------------------------
		public DebugTile SelectedTile
		{
			get
			{
				if (SelectedPoint != null)
				{
					var x = SelectedPoint.Value.X + ZeroPoint.X;
					var y = (DebugFrame.GridSize.Y - 1 + ZeroPoint.Y) - SelectedPoint.Value.Y;

					if (x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
					{
						return Grid[x, y];
					}
				}

				return null;
			}
		}

		//-----------------------------------------------------------------------
		public string InfoText
		{
			get { return m_infoText; }
			set
			{
				m_infoText = value;
				RaisePropertyChangedEvent();
			}
		}
		private string m_infoText = "";

		//-----------------------------------------------------------------------
		public IntPoint ZeroPoint = new IntPoint(0, 0);

		//-----------------------------------------------------------------------
		private GlyphRunBuilder GlyphRunBuilder;

		//-----------------------------------------------------------------------
		private DebugFrame DebugFrame { get { return DataContext as DebugFrame; } }

		//-----------------------------------------------------------------------
		public AsciiGrid()
		{
			DataContextChanged += (e, args) =>
			{
				DatacontextChanged();
				InvalidateVisual();
			};

			redrawTimer = new Timer();
			redrawTimer.Interval = 1.0 / 15.0;
			redrawTimer.Elapsed += (e, args) =>
			{
				if (m_dirty)
				{
					Application.Current.Dispatcher.BeginInvoke(new Action(() =>
					{
						InvalidateVisual();
						UpdateInfoText();
						RaisePropertyChangedEvent("ModeString");
					}));
					m_dirty = false;
				}
			};
			redrawTimer.Start();

			Focusable = true;

			Typeface typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
			GlyphRunBuilder = new GlyphRunBuilder(typeface);
		}

		//-----------------------------------------------------------------------
		~AsciiGrid()
		{
			redrawTimer.Stop();
		}

		//-----------------------------------------------------------------------
		private void DatacontextChanged()
		{
			var frame = DebugFrame;
			if (frame == null) return;

			Grid = frame.Grid;
			ZeroPoint = new IntPoint(-frame.Offset.X, -frame.Offset.Y);

			if (ActualWidth == 0 || double.IsNaN(ActualWidth) || ActualHeight == 0 || double.IsNaN(ActualHeight))
			{
				Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
				{
					DatacontextChanged();
				}));
			}
			else
			{
				RaisePropertyChangedEvent("SelectedTile");
			}
		}

		//-----------------------------------------------------------------------
		private void UpdateInfoText()
		{
			var text = "";

			InfoText = text;
		}

		//-----------------------------------------------------------------------
		protected override void OnRender(DrawingContext drawingContext)
		{
			if (Grid == null) return;

			if (selectionBackBrush == null)
			{
				selectionBackBrush = new SolidColorBrush(Color.FromScRgb(0.1f, SelectedColour.ScR, SelectedColour.ScG, SelectedColour.ScB));
				selectionBackBrush.Freeze();
			}

			if (mouseOverBackPen == null)
			{
				var brush = new SolidColorBrush(Color.FromScRgb(0.5f, SelectedColour.ScR, SelectedColour.ScG, SelectedColour.ScB));
				mouseOverBackPen = new Pen(brush, 1);
				mouseOverBackPen.Freeze();
			}

			if (gridPen == null)
			{
				gridPen = new Pen(GridBrush, 1);
				gridPen.Freeze();
			}

			if (selectedPen == null)
			{
				selectedPen = new Pen(SelectedBrush, 1);
				selectedPen.Freeze();
			}

			base.OnRender(drawingContext);

			drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));

			drawingContext.DrawRectangle(BackgroundBrush, null, new System.Windows.Rect(0, 0, ActualWidth, ActualHeight));

			// draw used area
			drawingContext.DrawRectangle(InactiveBrush, null,
							new System.Windows.Rect(
								-ViewPos.X,
								-ViewPos.Y ,
								DebugFrame.GridSize.X * PixelsATile,
								DebugFrame.GridSize.Y * PixelsATile));

			// draw grid lines
			var startX = (Math.Floor(ViewPos.X / PixelsATile) * PixelsATile) - ViewPos.X;
			var startY = (Math.Floor(ViewPos.Y / PixelsATile) * PixelsATile) - ViewPos.Y;

			for (int x = 0; x < GridWidth; x++)
			{
				var drawX = -ViewPos.X + x * PixelsATile - (ZeroPoint.X * PixelsATile);
				if (drawX > ActualWidth || drawX + PixelsATile < 0) { continue; }

				for (int y = 0; y < GridHeight; y++)
				{
					var tile = Grid[x, y];
					if (!string.IsNullOrWhiteSpace(tile.Colour))
					{
						var drawY = -ViewPos.Y + (((DebugFrame.GridSize.Y - 1) * PixelsATile) - (y * PixelsATile - (ZeroPoint.Y * PixelsATile)));
						if (drawY > ActualHeight || drawY + PixelsATile < 0) { continue; }

						drawingContext.DrawRectangle(GetBrush(tile.Colour), null,
							new System.Windows.Rect(drawX, drawY, PixelsATile, PixelsATile));
					}
				}
			}

			for (double x = startX; x < ActualWidth; x += PixelsATile)
			{
				drawingContext.DrawLine(gridPen, new Point(x, 0), new Point(x, ActualHeight));
			}

			for (double y = startY; y < ActualHeight; y += PixelsATile)
			{
				drawingContext.DrawLine(gridPen, new Point(0, y), new Point(ActualWidth, y));
			}

			var usedTiles = new HashSet<int>();
			if (SelectedPoint != null)
			{
				usedTiles.Add(SelectedPoint.Value.FastHash);
			}

			if (mouseInside)
			{
				drawingContext.DrawRectangle(null, mouseOverBackPen, new Rect(mouseOverTile.X * PixelsATile - ViewPos.X, mouseOverTile.Y * PixelsATile - ViewPos.Y, PixelsATile, PixelsATile));
			}

			if (SelectedPoint != null)
			{
				var x = SelectedPoint.Value.X * PixelsATile - ViewPos.X;
				var y = SelectedPoint.Value.Y * PixelsATile - ViewPos.Y;

				if (!usedTiles.Contains(SelectedPoint.Value.OffsetHash(0, -1)))
				{
					// draw top
					drawingContext.DrawLine(selectedPen, new Point(x, y), new Point(x + PixelsATile, y));
				}
				if (!usedTiles.Contains(SelectedPoint.Value.OffsetHash(0, 1)))
				{
					// draw bottom
					drawingContext.DrawLine(selectedPen, new Point(x, y + PixelsATile), new Point(x + PixelsATile, y + PixelsATile));
				}
				if (!usedTiles.Contains(SelectedPoint.Value.OffsetHash(-1, 0)))
				{
					// draw left
					drawingContext.DrawLine(selectedPen, new Point(x, y), new Point(x, y + PixelsATile));
				}
				if (!usedTiles.Contains(SelectedPoint.Value.OffsetHash(1, 0)))
				{
					// draw right
					drawingContext.DrawLine(selectedPen, new Point(x + PixelsATile, y), new Point(x + PixelsATile, y + PixelsATile));
				}

				drawingContext.DrawRectangle(selectionBackBrush, null, new Rect(x, y, PixelsATile, PixelsATile));
			}

			GlyphRunBuilder.StartRun(PixelsATile, -ViewPos.X + PixelsATile / 4, -ViewPos.Y - PixelsATile / 5);

			// draw characters
			usedTiles.Clear();

			// draw characters
			for (int x = 0; x < GridWidth; x++)
			{
				var drawX = -ViewPos.X + x * PixelsATile - (ZeroPoint.X * PixelsATile);
				if (drawX > ActualWidth || drawX + PixelsATile < 0) { continue; }

				for (int y = 0; y < GridHeight; y++)
				{
					var drawY = -ViewPos.Y + (((DebugFrame.GridSize.Y - 1) * PixelsATile) - (y * PixelsATile - (ZeroPoint.Y * PixelsATile)));
					if (drawY > ActualHeight || drawY + PixelsATile < 0) { continue; }

					var drawPos = new IntPoint(x - ZeroPoint.X, (DebugFrame.GridSize.Y - 1) - (y - ZeroPoint.Y));

					var trueDrawPos = new Point(drawPos.X * PixelsATile - ViewPos.X, drawPos.Y * PixelsATile - ViewPos.Y);

					usedTiles.Add(drawPos.FastHash);
					GlyphRunBuilder.AddGlyph(drawPos.X, drawPos.Y, Grid[x, y].Char);
				}
			}

			var current = DebugFrame.Parent;
			while (current != null)
			{
				for (int x = 0; x < current.Grid.GetLength(0); x++)
				{
					var drawX = -ViewPos.X + x * PixelsATile - (current.Offset.X * PixelsATile);
					if (drawX > ActualWidth || drawX + PixelsATile < 0) { continue; }

					for (int y = 0; y < current.Grid.GetLength(1); y++)
					{
						var drawY = -ViewPos.Y + (((DebugFrame.GridSize.Y - 1) * PixelsATile) - (y * PixelsATile + (current.Offset.Y * PixelsATile)));
						if (drawY > ActualHeight || drawY + PixelsATile < 0) { continue; }

						var drawPos = new IntPoint(x + current.Offset.X, (DebugFrame.GridSize.Y - 1) - (y + current.Offset.Y));

						if (!usedTiles.Contains(drawPos.FastHash))
						{
							usedTiles.Add(drawPos.FastHash);
							GlyphRunBuilder.AddGlyph(drawPos.X, drawPos.Y, current.Grid[x, y].Char);
						}
					}
				}

				current = current.Parent;
			}

			if (GlyphRunBuilder.HasGlyphs())
			{
				var run = GlyphRunBuilder.GetRun();
				drawingContext.DrawGlyphRun(FontBrush, run);
			}
		}

		//-----------------------------------------------------------------------
		protected override void OnMouseWheel(MouseWheelEventArgs e)
		{
			var pos = e.GetPosition(this);

			var local = new Point((pos.X + ViewPos.X) / PixelsATile, (pos.Y + ViewPos.Y) / PixelsATile);

			PixelsATile += (e.Delta / 120) * (int)Math.Ceiling((double)PixelsATile / 10.0);
			if (PixelsATile < 2) PixelsATile = 2;

			ViewPos = new Point(local.X * PixelsATile - pos.X, local.Y * PixelsATile - pos.Y);

			InvalidateVisual();

			e.Handled = true;

			base.OnMouseWheel(e);
		}

		//-----------------------------------------------------------------------
		protected override void OnMouseDown(MouseButtonEventArgs e)
		{
			var pos = e.GetPosition(this);

			if (e.MiddleButton == MouseButtonState.Pressed)
			{
				panPos = pos;
				isPanning = true;
			}

			Keyboard.Focus(this);
		}

		//-----------------------------------------------------------------------
		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs args)
		{
			base.OnMouseLeftButtonDown(args);

			Keyboard.Focus(this);

			var pos = args.GetPosition(this);

			var local = new Point((pos.X + ViewPos.X) / PixelsATile, (pos.Y + ViewPos.Y) / PixelsATile);
			var roundedX = local.X < 0 ? (int)Math.Floor(local.X) : (int)local.X;
			var roundedY = local.Y < 0 ? (int)Math.Floor(local.Y) : (int)local.Y;

			SelectedPoint = new IntPoint(roundedX, roundedY);

			m_dirty = true;
		}

		//-----------------------------------------------------------------------
		protected override void OnMouseLeave(MouseEventArgs e)
		{
			base.OnMouseLeave(e);

			mouseInside = false;
			m_dirty = true;
		}

		//-----------------------------------------------------------------------
		protected override void OnMouseMove(MouseEventArgs args)
		{
			Keyboard.Focus(this);

			mouseInside = true;

			var pos = args.GetPosition(this);
			mousePos = pos;

			var local = new Point((pos.X + ViewPos.X) / PixelsATile, (pos.Y + ViewPos.Y) / PixelsATile);
			var roundedX = local.X < 0 ? (int)Math.Floor(local.X) : (int)local.X;
			var roundedY = local.Y < 0 ? (int)Math.Floor(local.Y) : (int)local.Y;

			mouseOverTile = new IntPoint(roundedX, roundedY);

			if (args.MiddleButton == MouseButtonState.Pressed && isPanning)
			{
				var diff = pos - panPos;
				ViewPos -= diff;
				m_dirty = true;

				if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
				{
					CaptureMouse();
					Mouse.OverrideCursor = Cursors.ScrollAll;
				}

				panPos = pos;
			}

			m_dirty = true;
		}

		//-----------------------------------------------------------------------
		protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
		{
			ReleaseMouseCapture();
			Mouse.OverrideCursor = null;

			base.OnPreviewMouseUp(e);
		}

		//-----------------------------------------------------------------------
		private void ZoomToBestFit()
		{
			if (ActualWidth == 0 || ActualHeight == 0)
			{
				return;
			}

			if (GridWidth == 0 && GridHeight == 0)
			{
				PixelsATile = 50;
				return;
			}

			var xSize = ActualWidth / (GridWidth + 2);
			var ySize = ActualHeight / (GridHeight + 2);

			PixelsATile = (int)Math.Min(xSize, ySize);
			if (PixelsATile < 5) PixelsATile = 5;

			var visibleTilesX = (int)(ActualWidth / PixelsATile);
			var visibleTilesY = (int)(ActualHeight / PixelsATile);

			var padTilesX = (visibleTilesX - GridWidth) / 2;
			var padTilesY = (visibleTilesY - GridHeight) / 2;

			ViewPos = new Point(-PixelsATile * padTilesX, -PixelsATile * padTilesY);
		}

		//-----------------------------------------------------------------------
		protected override void OnKeyDown(KeyEventArgs args)
		{
			base.OnKeyDown(args);

			if (args.Key == Key.Left)
			{
				if (SelectedPoint != null)
				{
					SelectedPoint = new IntPoint(SelectedPoint.Value.X - 1, SelectedPoint.Value.Y);

					var viewMinX = SelectedPoint.Value.X * PixelsATile - ViewPos.X;
					if (viewMinX < 0)
					{
						ViewPos = new Point(ViewPos.X - PixelsATile, ViewPos.Y);
					}
				}
			}
			else if (args.Key == Key.Up)
			{
				if (SelectedPoint != null)
				{
					SelectedPoint = new IntPoint(SelectedPoint.Value.X, SelectedPoint.Value.Y - 1);

					var viewMinY = SelectedPoint.Value.Y * PixelsATile - ViewPos.Y;
					if (viewMinY < 0)
					{
						ViewPos = new Point(ViewPos.X, ViewPos.Y - PixelsATile);
					}
				}
			}
			else if (args.Key == Key.Right)
			{
				if (SelectedPoint != null)
				{
					SelectedPoint = new IntPoint(SelectedPoint.Value.X + 1, SelectedPoint.Value.Y);

					var viewMaxX = SelectedPoint.Value.X * PixelsATile - ViewPos.X;
					if (viewMaxX + PixelsATile > ActualWidth)
					{
						ViewPos = new Point(ViewPos.X + PixelsATile, ViewPos.Y);
					}
				}
			}
			else if (args.Key == Key.Down)
			{
				if (SelectedPoint != null)
				{
					SelectedPoint = new IntPoint(SelectedPoint.Value.X, SelectedPoint.Value.Y + 1);

					var viewMaxY = SelectedPoint.Value.Y * PixelsATile - ViewPos.Y;
					if (viewMaxY + PixelsATile > ActualHeight)
					{
						ViewPos = new Point(ViewPos.X, ViewPos.Y + PixelsATile);
					}
				}
			}

			m_dirty = true;
		}

		//-----------------------------------------------------------------------
		public string KeyCodeToUnicode(Key key)
		{
			byte[] keyboardState = new byte[255];
			bool keyboardStateStatus = GetKeyboardState(keyboardState);

			if (!keyboardStateStatus)
			{
				return "";
			}

			uint virtualKeyCode = (uint)KeyInterop.VirtualKeyFromKey(key);
			uint scanCode = MapVirtualKey(virtualKeyCode, 0);
			IntPtr inputLocaleIdentifier = GetKeyboardLayout(0);

			StringBuilder result = new StringBuilder();
			ToUnicodeEx(virtualKeyCode, scanCode, keyboardState, result, (int)5, (uint)0, inputLocaleIdentifier);

			return result.ToString();
		}

		//-----------------------------------------------------------------------
		[DllImport("user32.dll")]
		static extern bool GetKeyboardState(byte[] lpKeyState);

		[DllImport("user32.dll")]
		static extern uint MapVirtualKey(uint uCode, uint uMapType);

		[DllImport("user32.dll")]
		static extern IntPtr GetKeyboardLayout(uint idThread);

		[DllImport("user32.dll")]
		static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);

		//--------------------------------------------------------------------------
		public event PropertyChangedEventHandler PropertyChanged;

		//-----------------------------------------------------------------------
		public void RaisePropertyChangedEvent
		(
			[CallerMemberName] string i_propertyName = ""
		)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(i_propertyName));
			}
		}

		//-----------------------------------------------------------------------

		Point mousePos;
		IntPoint mouseOverTile;
		bool mouseInside = false;

		Point panPos;
		bool isDragging = false;
		bool isPanning = false;
		private bool m_dirty;
		Timer redrawTimer;

		Pen gridPen;
		Pen selectedPen;
		Brush selectionBackBrush;
		Pen mouseOverBackPen;
	}

	public struct IntPoint
	{
		public int X { get; set; }
		public int Y { get; set; }

		public IntPoint(int x, int y)
		{
			this.X = x;
			this.Y = y;
		}

		public int OffsetHash(int x, int y)
		{
			return GetFastHash(X + x, Y + y);
		}

		public static int GetFastHash(int x, int y)
		{
			return x * 100000 + y;
		}

		public int FastHash { get { return GetFastHash(X, Y); } }
	}

	public class GlyphRunBuilder
	{
		private HashSet<int> usedPoints = new HashSet<int>();
		private double tileSize;
		private double xOff;
		private double yOff;
		private List<ushort> glyphIndices = new List<ushort>();
		private List<double> advanceWidths = new List<double>();
		private List<Point> glyphOffsets = new List<Point>();
		private GlyphTypeface typeface;

		public GlyphRunBuilder(Typeface type)
		{
			type.TryGetGlyphTypeface(out typeface);
		}

		public void StartRun(double tileSize, double xOff, double yOff)
		{
			usedPoints.Clear();
			glyphIndices.Clear();
			advanceWidths.Clear();
			glyphOffsets.Clear();

			this.tileSize = tileSize;
			this.xOff = xOff;
			this.yOff = yOff;
		}

		public void AddGlyph(int x, int y, char c)
		{
			int posHash = x * 100000 + y;
			if (!usedPoints.Contains(posHash))
			{
				usedPoints.Add(posHash);

				if (c == ' ') return;

				var glyphIndex = typeface.CharacterToGlyphMap[c];
				glyphIndices.Add(glyphIndex);
				advanceWidths.Add(0);
				glyphOffsets.Add(new Point(x * tileSize, (y+1) * tileSize * -1));
			}
		}

		public bool HasGlyphs()
		{
			return glyphIndices.Count > 0;
		}

		public GlyphRun GetRun()
		{
			if (usedPoints.Count == 0) return null;
			return new GlyphRun(
				typeface,
				0,
				false,
				tileSize,
				glyphIndices,
				new Point(xOff, yOff),
				advanceWidths,
				glyphOffsets,
				null,
				null,
				null,
				null,
				null);
		}
	}
}
