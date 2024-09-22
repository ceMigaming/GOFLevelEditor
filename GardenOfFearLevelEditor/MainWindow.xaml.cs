using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using MapLib;
using Microsoft.UI.Windowing;
using Windows.Storage.Pickers;
using Windows.Globalization;
using CommunityToolkit.Mvvm.Input;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GardenOfFearLevelEditor
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private bool isDrawing = false;
        private Point startPos = new(0, 0);
        private Point startPanPos = new(0, 0);

        private readonly static int TILE_SIZE = 64;

        private Map.TileType currentTileType = Map.TileType.Floor;
        private readonly static Map.TileType TILE_NONE = Map.TileType.None;

        private readonly List<List<KeyValuePair<Point, Map.TileType>>> undoList = [];
        private readonly List<List<KeyValuePair<Point, Map.TileType>>> redoList = [];

        private readonly Dictionary<Map.TileType, ImageBrush> imageBrushes;

        private readonly Dictionary<Map.TileType, BitmapImage> textures;
        private readonly Dictionary<Map.TileType, BitmapImage> icons;

        private readonly Dictionary<Map.TileType, int> tileTypeLayers = new()
        {
            {Map.TileType.None, -1},
            {Map.TileType.Floor, 0},
            {Map.TileType.Wall, 1},
            {Map.TileType.Door, 1}
        };

        private Map map = new(16, 16, 1);

        public MainWindow()
        {
            this.InitializeComponent();
            MyScrollViewer01.MinWidth = MyScrollViewer01.Width = Bounds.Width;
            MyScrollViewer01.MinHeight = MyScrollViewer01.Height = Bounds.Height;
            MainGrid.Width = Bounds.Width;
            MainGrid.Height = Bounds.Height;

            IEnumerable<Map.TileType> tiles = Enum.GetValues<Map.TileType>().Where(x => x != TILE_NONE);

            textures = new Dictionary<Map.TileType, BitmapImage>(tiles.Count());
            imageBrushes = new Dictionary<Map.TileType, ImageBrush>(tiles.Count());
            icons = new Dictionary<Map.TileType, BitmapImage>(tiles.Count());

            foreach (Map.TileType tile in tiles)
            {
                textures[tile] = new BitmapImage(new Uri($"ms-appx:///Assets/Textures/{tile}.png"));
                icons[tile] = new BitmapImage(new Uri($"ms-appx:///Assets/Icons/{tile}.png"));
                imageBrushes[tile] = new ImageBrush
                {
                    ImageSource = textures[tile]
                };

                StackPanel stackPanel = new();
                stackPanel.Children.Add(new Image
                {
                    Source = icons[tile],
                    Width = 64
                });
                stackPanel.Children.Add(new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Text = tile.ToString()
                });

                TileSelectorFlyout.Items.Add(
                    new MenuFlyoutItem
                    {
                        Text = tile.ToString(),
                        Command = new RelayCommand(() =>
                        {
                            currentTileType = tile;
                            MyDropDownButton.Content = stackPanel;

                            switch (tile)
                            {
                                case Map.TileType.Floor:
                                    MyCanvas02.Visibility = Visibility.Collapsed;
                                    MyCanvas01.Opacity = 1;
                                    break;
                                case Map.TileType.Wall:
                                case Map.TileType.Door:
                                    MyCanvas02.Visibility = Visibility.Visible;
                                    MyCanvas01.Opacity = 0.2;
                                    break;
                            }
                        })
                    });
            }
        }

        private void MyCanvas01_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (currentTileType != Map.TileType.Floor) return;
            redoList.Clear();
            if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
            {
                var properties = e.GetCurrentPoint(MyCanvas01).Properties;
                if (properties.IsLeftButtonPressed)
                {
                    isDrawing = true;
                    startPos = e.GetCurrentPoint(MyCanvas01).Position;
                    AddObjectToGrid(startPos, currentTileType, true);
                }
                else if (properties.IsMiddleButtonPressed)
                {
                    startPanPos = e.GetCurrentPoint(MyCanvas01).Position;
                }
                else if (properties.IsRightButtonPressed)
                {
                    Point p = e.GetCurrentPoint(MyCanvas01).Position;
                    AddObjectToGrid(p, TILE_NONE, true);
                }
            }
        }

        private void MyCanvas01_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (currentTileType != Map.TileType.Floor) return;
            if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
            {
                var properties = e.GetCurrentPoint(MyCanvas01).Properties;
                if (properties.IsLeftButtonPressed)
                {
                    if (!isDrawing) return;
                    Point p = e.GetCurrentPoint(MyCanvas01).Position;
                    AddObjectToGrid(p, currentTileType, false);
                }
                else if (properties.IsMiddleButtonPressed)
                {
                    MyScrollViewer01.ChangeView(MyScrollViewer01.HorizontalOffset + startPanPos.X - e.GetCurrentPoint(MyCanvas01).Position.X, MyScrollViewer01.VerticalOffset + startPanPos.Y - e.GetCurrentPoint(MyCanvas01).Position.Y, null);
                }
                else if (properties.IsRightButtonPressed)
                {
                    Point p = e.GetCurrentPoint(MyCanvas01).Position;
                    AddObjectToGrid(p, TILE_NONE, false);
                }
            }
        }

        private void MyCanvas01_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            isDrawing = false;
            startPos = new Point(0, 0);
        }

        private void AddObjectToGrid(Point point, Map.TileType tileType, bool isFirstInStroke)
        {
            int x = ((int)point.X / TILE_SIZE) * TILE_SIZE;
            int y = ((int)point.Y / TILE_SIZE) * TILE_SIZE;

            Map.TileType lastTile = map.GetTile(x / TILE_SIZE, y / TILE_SIZE, 0);

            if (tileType != TILE_NONE)
            {
                if (lastTile == currentTileType)
                {
                    return;
                }

                Rectangle rect = new Rectangle
                {
                    Width = TILE_SIZE,
                    Height = TILE_SIZE,
                    Fill = imageBrushes[tileType]
                };
                MyCanvas01.Children.Add(rect);

                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
            }
            else
            {
                if (lastTile == TILE_NONE)
                {
                    return;
                }

                MyCanvas01.Children.Remove(MyCanvas01.Children.Where(x => x.GetType() == typeof(Rectangle)).Cast<Rectangle>().FirstOrDefault(r => Canvas.GetLeft(r) == x && Canvas.GetTop(r) == y));
            }
            map.SetTile(x / TILE_SIZE, y / TILE_SIZE, 0, tileType);
            var p = new Point(x, y);
            if (!isFirstInStroke && undoList.Count > 0)
            {
                undoList[undoList.Count - 1].Add(KeyValuePair.Create(p, lastTile));
            }
            else
            {
                undoList.Add(new List<KeyValuePair<Point, Map.TileType>> { KeyValuePair.Create(p, lastTile) });
            }
        }

        private async void NewDocument_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new()
            {
                XamlRoot = Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Title = "Creating new document",
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = new NewDocumentPage()
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                NewDocumentPage page = dialog.Content as NewDocumentPage;
                map = new Map(page.GetWidth(), page.GetHeight(), 2);
                MyCanvas01.Children.Clear();
                RepaintCanvas(page.GetWidth(), page.GetHeight());
            }
        }

        private void RepaintCanvas(int width, int height)
        {
            MyCanvas01.Width = width * TILE_SIZE;
            MyCanvas01.Height = height * TILE_SIZE;
            Border border1 = new Border
            {
                VerticalAlignment = VerticalAlignment.Stretch,
                Width = MyCanvas01.Width,
                Height = MyCanvas01.Height,
                Background = new LinearGradientBrush
                {
                    EndPoint = new Point(TILE_SIZE / 2, 0),
                    SpreadMethod = GradientSpreadMethod.Repeat,
                    MappingMode = BrushMappingMode.Absolute,
                    StartPoint = new Point(-TILE_SIZE / 2, 0),
                    RelativeTransform = new CompositeTransform
                    {
                        CenterY = 0.5,
                        CenterX = 0.5
                    },
                    GradientStops =
                    {
                        new GradientStop { Color = Colors.Transparent, Offset = 0.53 },
                        new GradientStop { Color = Colors.Transparent, Offset = 0.48 },
                        new GradientStop { Color = Color.FromArgb(0xFF, 0xEA, 0xE5, 0xEF), Offset = 0.49 },
                        new GradientStop { Color = Color.FromArgb(0xFF, 0xEA, 0xE5, 0xEF), Offset = 0.51 },
                        new GradientStop { Color = Color.FromArgb(0xFF, 0xEA, 0xE5, 0xEF), Offset = 0.51 }
                    }
                }
            };
            MyCanvas01.Children.Add(border1);

            Border border2 = new Border
            {
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = MyCanvas01.Width,
                Height = MyCanvas01.Height,
                Background = new LinearGradientBrush
                {
                    EndPoint = new Point(0, TILE_SIZE / 2),
                    SpreadMethod = GradientSpreadMethod.Repeat,
                    MappingMode = BrushMappingMode.Absolute,
                    StartPoint = new Point(0, -TILE_SIZE / 2),
                    RelativeTransform = new CompositeTransform
                    {
                        CenterY = 0.5,
                        CenterX = 0.5
                    },
                    GradientStops =
                    {
                        new GradientStop { Color = Colors.Transparent, Offset = 0.53 },
                        new GradientStop { Color = Colors.Transparent, Offset = 0.48 },
                        new GradientStop { Color = Color.FromArgb(0xFF, 0xEA, 0xE5, 0xEF), Offset = 0.49 },
                        new GradientStop { Color = Color.FromArgb(0xFF, 0xEA, 0xE5, 0xEF), Offset = 0.51 },
                        new GradientStop { Color = Color.FromArgb(0xFF, 0xEA, 0xE5, 0xEF), Offset = 0.51 }
                    }
                }
            };
            MyCanvas01.Children.Add(border2);
        }

        private void Window_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            MyScrollViewer01.MinWidth = MyScrollViewer01.Width = args.Size.Width;
            MyScrollViewer01.MinHeight = MyScrollViewer01.Height = args.Size.Height;
            MainGrid.Width = args.Size.Width;
            MainGrid.Height = args.Size.Height;
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (undoList.Count == 0)
            {
                return;
            }

            var currentRedoList = new List<KeyValuePair<Point, Map.TileType>>();

            foreach (KeyValuePair<Point, Map.TileType> kv in undoList[undoList.Count - 1])
            {
                var p = kv.Key;
                var t = kv.Value;
                if (t != Map.TileType.None)
                {
                    Rectangle rect = new Rectangle
                    {
                        Width = TILE_SIZE,
                        Height = TILE_SIZE,
                        Fill = imageBrushes[t]
                    };
                    MyCanvas01.Children.Add(rect);

                    Canvas.SetLeft(rect, p.X);
                    Canvas.SetTop(rect, p.Y);
                }
                else
                {
                    MyCanvas01.Children.Remove(MyCanvas01.Children.Where(x => x.GetType() == typeof(Rectangle)).Cast<Rectangle>().FirstOrDefault(r => Canvas.GetLeft(r) == ((int)p.X / TILE_SIZE) * TILE_SIZE && Canvas.GetTop(r) == ((int)p.Y / TILE_SIZE) * TILE_SIZE));
                }
                Map.TileType lastTile = map.GetTile((int)p.X / TILE_SIZE, (int)p.Y / TILE_SIZE, 0);
                currentRedoList.Add(KeyValuePair.Create(p, lastTile));
                map.SetTile((int)p.X / TILE_SIZE, (int)p.Y / TILE_SIZE, 0, t);
            }
            redoList.Add(currentRedoList);
            undoList.RemoveAt(undoList.Count - 1);
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (redoList.Count == 0)
            {
                return;
            }

            var currentUndoList = new List<KeyValuePair<Point, Map.TileType>>();

            foreach (KeyValuePair<Point, Map.TileType> kv in redoList[redoList.Count - 1])
            {
                var p = kv.Key;
                var t = kv.Value;
                if (t != Map.TileType.None)
                {
                    Rectangle rect = new Rectangle
                    {
                        Width = TILE_SIZE,
                        Height = TILE_SIZE,
                        Fill = imageBrushes[t]
                    };
                    MyCanvas01.Children.Add(rect);

                    Canvas.SetLeft(rect, p.X);
                    Canvas.SetTop(rect, p.Y);
                }
                else
                {
                    MyCanvas01.Children.Remove(MyCanvas01.Children.Where(x => x.GetType() == typeof(Rectangle)).Cast<Rectangle>().FirstOrDefault(r => Canvas.GetLeft(r) == ((int)p.X / TILE_SIZE) * TILE_SIZE && Canvas.GetTop(r) == ((int)p.Y / TILE_SIZE) * TILE_SIZE));
                }
                Map.TileType lastTile = map.GetTile((int)p.X / TILE_SIZE, (int)p.Y / TILE_SIZE, 0);
                currentUndoList.Add(KeyValuePair.Create(p, lastTile));
                map.SetTile((int)p.X / TILE_SIZE, (int)p.Y / TILE_SIZE, 0, t);
            }
            undoList.Add(currentUndoList);
            redoList.RemoveAt(redoList.Count - 1);
        }

        private async void Exit_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new()
            {
                XamlRoot = Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Title = "Exit",
                PrimaryButtonText = "Yes",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = "Are you sure you want to exit?"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                Close();
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker savePicker = new();

            var window = App.GetMainWindow();
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Garden of Fear Level", [".gofl"]);
            savePicker.SuggestedFileName = "New Level";

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                Map.WriteToXmlFile(map, file.Path);
            }
        }

        private async void Open_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker openPicker = new();

            var window = App.GetMainWindow();
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.FileTypeFilter.Add(".gofl");

            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                map = Map.ReadFromXmlFile(file.Path);
                MyCanvas01.Children.Clear();
                RepaintCanvas(map.Width, map.Height);
                for (int x = 0; x < map.Width; x++)
                {
                    for (int y = 0; y < map.Height; y++)
                    {
                        Map.TileType t = map.GetTile(x, y, 0);
                        if (t != Map.TileType.None)
                        {
                            Rectangle rect = new Rectangle
                            {
                                Width = TILE_SIZE,
                                Height = TILE_SIZE,
                                Fill = imageBrushes[t]
                            };
                            MyCanvas01.Children.Add(rect);

                            Canvas.SetLeft(rect, x * TILE_SIZE);
                            Canvas.SetTop(rect, y * TILE_SIZE);
                        }
                    }
                }
            }
        }

        private async void Help_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new()
            {
                XamlRoot = Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Title = "Help",
                PrimaryButtonText = "Yes",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = "Do you want to open Github Issues page?"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // open github issues page in browser
                System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/ceMigaming",
                    UseShellExecute = true
                });
            }
        }

        private void MyCanvas02_PointerPressed(object sender, PointerRoutedEventArgs e)
        {

        }

        private void MyCanvas02_PointerMoved(object sender, PointerRoutedEventArgs e)
        {

        }

        private void MyCanvas02_PointerReleased(object sender, PointerRoutedEventArgs e)
        {

        }
    }
}
