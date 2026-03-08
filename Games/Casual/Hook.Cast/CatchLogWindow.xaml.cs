namespace HookCast;

public partial class CatchLogWindow : Window
{
    public CatchLogWindow(IEnumerable<CatchRecord> records)
    {
        InitializeComponent();
        RefreshLog(records);
    }

    public void RefreshLog(IEnumerable<CatchRecord> records)
    {
        LogList.ItemsSource = records.OrderByDescending(r => r.Time).ToList();
    }
}
