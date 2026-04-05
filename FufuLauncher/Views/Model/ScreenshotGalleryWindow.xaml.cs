using System.Collections.ObjectModel;
using FufuLauncher.Models;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace FufuLauncher.Views;

public sealed partial class ScreenshotGalleryWindow : Window
{
    private readonly string _screenshotDirectory;
    private ObservableCollection<ScreenshotGroup> _galleryData = new();
    private ScreenshotItem _currentDetailItem;
    private AppWindow _appWindow;

    private const string ConnectedAnimationKey = "ForwardConnectedAnimation";

    public ScreenshotGalleryWindow(string screenshotDirectory)
    {
        this.InitializeComponent();
        _screenshotDirectory = screenshotDirectory;
        
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(null); 
        CustomizeTitleBar();

        RootGrid.Loaded += (s, e) => { _ = LoadScreenshotsAsync(); };
    }

    private void CustomizeTitleBar()
    {
        IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow != null)
        {
            AppTitleBarRow.Height = new GridLength(_appWindow.TitleBar.Height);
            
            _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }
    }

    private async Task LoadScreenshotsAsync()
    {
        _galleryData.Clear();

        if (!Directory.Exists(_screenshotDirectory)) return;

        var filesInfo = await Task.Run(() =>
        {
            try
            {
                return Directory.GetFiles(_screenshotDirectory, "*.png", SearchOption.TopDirectoryOnly)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取目录失败: {ex.Message}");
                return new List<FileInfo>();
            }
        });

        if (!filesInfo.Any()) return;

        var groupedFiles = filesInfo.GroupBy(f => f.CreationTime.ToString("yyyy年MM月dd日"));

        foreach (var group in groupedFiles)
        {
            var folderGroup = new ScreenshotGroup { DateKey = group.Key };
            foreach (var file in group)
            {
                var bitmap = new BitmapImage(new Uri(file.FullName));
                folderGroup.Items.Add(new ScreenshotItem
                {
                    FilePath = file.FullName,
                    FileName = file.Name,
                    CreationTime = file.CreationTime,
                    ImageSource = bitmap
                });
            }
            _galleryData.Add(folderGroup);
        }

        GalleryViewSource.Source = _galleryData;
    }

    private void GridItem_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid && grid.RenderTransform is ScaleTransform scaleTransform)
        {
            AnimateScale(scaleTransform, 1.05);
        }
    }

    private void GridItem_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid && grid.RenderTransform is ScaleTransform scaleTransform)
        {
            AnimateScale(scaleTransform, 1.0);
        }
    }

    private void AnimateScale(ScaleTransform target, double toScale)
    {
        var storyboard = new Storyboard();
        
        var animX = new DoubleAnimation { To = toScale, Duration = new Duration(TimeSpan.FromMilliseconds(200)), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        var animY = new DoubleAnimation { To = toScale, Duration = new Duration(TimeSpan.FromMilliseconds(200)), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };

        Storyboard.SetTarget(animX, target);
        Storyboard.SetTargetProperty(animX, "ScaleX");
        
        Storyboard.SetTarget(animY, target);
        Storyboard.SetTargetProperty(animY, "ScaleY");

        storyboard.Children.Add(animX);
        storyboard.Children.Add(animY);
        storyboard.Begin();
    }

    private async void GalleryGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ScreenshotItem item)
        {
            _currentDetailItem = item;

            var gridViewItem = GalleryGridView.ContainerFromItem(item) as GridViewItem;
            if (gridViewItem != null)
            {
                var imageInGrid = (Image)FindDescendantByName(gridViewItem, "GridImage");
                
                if (imageInGrid != null)
                {
                    ConnectedAnimationService.GetForCurrentView().PrepareToAnimate(ConnectedAnimationKey, imageInGrid);
                }
            }

            DetailImageViewer.Source = item.ImageSource;
            
            DetailScrollViewer.ChangeView(null, null, 1.0f, true);

            GalleryGridView.Visibility = Visibility.Collapsed;
            DetailOverlayGrid.Visibility = Visibility.Visible;

            var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation(ConnectedAnimationKey);
            if (anim != null)
            {
                anim.Configuration = new BasicConnectedAnimationConfiguration();
                await Task.Delay(1);
                anim.TryStart(DetailImageViewer);
            }
        }
    }

    private async void CloseDetailView_Click(object sender, RoutedEventArgs e)
    {
        if (_currentDetailItem == null) return;

        ConnectedAnimationService.GetForCurrentView().PrepareToAnimate(ConnectedAnimationKey, DetailImageViewer);

        DetailOverlayGrid.Visibility = Visibility.Collapsed;
        GalleryGridView.Visibility = Visibility.Visible;

        var gridViewItem = GalleryGridView.ContainerFromItem(_currentDetailItem) as GridViewItem;
        if (gridViewItem != null)
        {
            GalleryGridView.ScrollIntoView(_currentDetailItem);
            await Task.Delay(1);

            ConnectedAnimationService.GetForCurrentView().GetAnimation(ConnectedAnimationKey).TryStart(gridViewItem);
        }

        _currentDetailItem = null;
        DetailImageViewer.Source = null;
    }

    private void DetailScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(DetailScrollViewer).Properties;
        if (properties.IsHorizontalMouseWheel) return; 

        e.Handled = true;

        double delta = properties.MouseWheelDelta;
        double scaleFactor = 1.1; 
        double currentZoom = DetailScrollViewer.ZoomFactor;
        float newZoom;

        if (delta > 0)
        {
            newZoom = (float)(currentZoom * scaleFactor);
        }
        else
        {
            newZoom = (float)(currentZoom / scaleFactor);
        }

        newZoom = Math.Max(0.1f, Math.Min(10.0f, newZoom));

        if (Math.Abs(delta) > 200) newZoom = 1.0f;

        DetailScrollViewer.ChangeView(null, null, newZoom);
    }

    private async void CopyImage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentDetailItem != null)
        {
            await SetClipboardDataAsync(DataPackageOperation.Copy);
        }
    }

    private async void CutImage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentDetailItem != null)
        {
            await SetClipboardDataAsync(DataPackageOperation.Move);
        }
    }

    private async Task SetClipboardDataAsync(DataPackageOperation operation)
    {
        try
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(_currentDetailItem.FilePath);
            var dataPackage = new DataPackage();
            dataPackage.SetStorageItems(new List<IStorageItem> { storageFile });
            dataPackage.RequestedOperation = operation;
            Clipboard.SetContent(dataPackage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"剪贴板操作失败: {ex.Message}");
        }
    }

    private async void DeleteImage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentDetailItem == null || !File.Exists(_currentDetailItem.FilePath)) return;

        try
        {
            var dialog = new ContentDialog
            {
                Title = "确认删除",
                Content = "确定要永久删除此截图吗？此操作无法撤销。",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = RootGrid.XamlRoot 
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                ExecuteDeleteLogic();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"弹窗显示失败: {ex.Message}");
        }
    }

    private void ExecuteDeleteLogic()
    {
        try
        {
            File.Delete(_currentDetailItem.FilePath);
            
            foreach (var group in _galleryData)
            {
                if (group.Items.Contains(_currentDetailItem))
                {
                    group.Items.Remove(_currentDetailItem);
                    if (group.Items.Count == 0)
                    {
                        _galleryData.Remove(group);
                    }
                    break;
                }
            }
            
            DetailOverlayGrid.Visibility = Visibility.Collapsed;
            GalleryGridView.Visibility = Visibility.Visible;
            _currentDetailItem = null;
            DetailImageViewer.Source = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"删除文件失败: {ex.Message}");
        }
    }

    private async void OpenWithSystemApp_Click(object sender, RoutedEventArgs e)
    {
        if (_currentDetailItem != null && File.Exists(_currentDetailItem.FilePath))
        {
            try
            {
                var storageFile = await StorageFile.GetFileFromPathAsync(_currentDetailItem.FilePath);
                var options = new Windows.System.LauncherOptions { DisplayApplicationPicker = true };
                await Windows.System.Launcher.LaunchFileAsync(storageFile, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"系统应用打开失败: {ex.Message}");
            }
        }
    }

    private DependencyObject FindDescendantByName(DependencyObject parent, string name)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement fe && fe.Name == name)
            {
                return child;
            }
            DependencyObject descendant = FindDescendantByName(child, name);
            if (descendant != null)
            {
                return descendant;
            }
        }
        return null;
    }
}