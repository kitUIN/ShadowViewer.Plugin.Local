using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ShadowViewer.Plugin.Local.Controls;

/// <summary>
/// 自动翻页Icon
/// </summary>
public sealed partial class FlashIcon : UserControl
{ 
    /// <summary>
    /// 动画控制器
    /// </summary>
    private readonly Storyboard rotateStoryboard;
    /// <summary>
    /// 状态
    /// </summary>
    public bool Status
    {
        get => (bool)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }
    /// <summary>
    /// 状态依赖属性
    /// </summary>
    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(bool), typeof(FlashIcon),
            new PropertyMetadata(false, OnStatusChanged));
    /// <summary>
    /// 响应状态变化
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FlashIcon control) return;
        if (e.NewValue is true) control.StartRefresh();
        else control.StopRefresh();
    }

    /// <summary>
    /// 自动翻页图标
    /// </summary>
    public FlashIcon()
    {
        this.InitializeComponent();
        rotateStoryboard = (Storyboard)Resources["RotateStoryboard"];
        this.Loaded += AutomationIndicator_Loaded;
    }
    /// <summary>
    /// 初始化检测是否需要旋转
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AutomationIndicator_Loaded(object sender, RoutedEventArgs e)
    {
        if (Status) StartRefresh();
        else StopRefresh();
    }
    /// <summary>
    /// 开启旋转动画
    /// </summary>
    public void StartRefresh()
    {
        MainIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 250, 192, 61));
        RotatingIcon.Visibility = Visibility.Visible;
        rotateStoryboard.Begin();
    }
    /// <summary>
    /// 关闭旋转动画
    /// </summary>
    public void StopRefresh()
    {
        MainIcon.ClearValue(Control.ForegroundProperty);
        RotatingIcon.Visibility = Visibility.Collapsed;
        rotateStoryboard.Stop();
    }
}