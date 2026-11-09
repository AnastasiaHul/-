using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SmartTrip.Application.Contracts;
using SmartTrip.Application.Services;

namespace Smart_tripp.WPF;

public abstract class TravelToolWindow : Window
{
    private readonly UserSession session;
    private readonly IAppLogger logger;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private bool busy;

    protected TravelToolWindow(string title, double width, double height,
        UserSession session, IAppLogger logger)
    {
        this.session = session;
        this.logger = logger;
        Title = title + " — Smart Trip";
        Width = width;
        Height = height;
        MinWidth = 540;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel { Margin = new Thickness(22) };
        Content = root;
        var heading = new TextBlock
        {
            Text = title, FontSize = 27, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(24, 59, 54)),
            Margin = new Thickness(0, 0, 0, 14)
        };
        DockPanel.SetDock(heading, Dock.Top);
        root.Children.Add(heading);

        var footer = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
        var back = new Button { Content = "Назад", Padding = new Thickness(12, 6, 12, 6), IsCancel = true };
        DockPanel.SetDock(back, Dock.Right);
        footer.Children.Add(back);
        footer.Children.Add(status);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(new ScrollViewer
        {
            Content = Form,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });

        Closing += (_, e) => e.Cancel = busy;
    }

    protected StackPanel Form { get; } = new();

    protected static void AddField(Panel panel, string label, FrameworkElement field)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 4) });
        panel.Children.Add(field);
    }

    protected static Button MakeButton(string title) =>
        new()
        {
            Content = title,
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 8, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Left
        };

    protected void ShowStatus(string message) => status.Text = message;

    protected void Reject(string action, string message)
    {
        logger.Information($"{action}.ValidationFailed: {message}", session.CurrentUser?.UserId);
        ShowStatus(message);
    }

    protected async Task RunAsync(string action, Func<Task> run)
    {
        if (busy)
        {
            return;
        }

        busy = true;
        Form.IsEnabled = false;
        try
        {
            await run();
        }
        catch (Exception exception)
        {
            logger.Error(exception, action + ".Window", session.CurrentUser?.UserId);
            ShowStatus("Не вдалося виконати дію. Деталі записано в журнал.");
        }
        finally
        {
            Form.IsEnabled = true;
            busy = false;
        }
    }
}
