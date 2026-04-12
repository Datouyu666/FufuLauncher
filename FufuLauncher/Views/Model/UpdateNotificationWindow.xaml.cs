using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace FufuLauncher.Views;

public sealed partial class UpdateNotificationWindow : WindowEx
{
    public UpdateNotificationWindow(string updateInfoUrl)
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        UpdateWebView.Source = new Uri(updateInfoUrl);

        this.CenterOnScreen();
        SystemBackdrop = new DesktopAcrylicBackdrop();
        IsShownInSwitchers = true;
    }
    
    private void OnUpdateBtnClicked(object sender, RoutedEventArgs e)
    {
        var updateWindow = new UpdateWindow();
        updateWindow.Activate();
        
        Close();
    }
}