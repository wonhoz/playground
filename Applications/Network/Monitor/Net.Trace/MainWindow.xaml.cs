using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using Net.Trace.ViewModels;

namespace Net.Trace;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    readonly MainViewModel _vm;

    // 대륙 폴리곤 데이터 (위도, 경도 쌍)
    static readonly (string Color, double[][] Pts)[] Continents =
    [
        // North America
        ("#0F1F0F", new double[][] {
            [71,-168],[83,-100],[83,-70],[73,-60],[65,-55],[60,-64],[47,-53],
            [44,-66],[36,-75],[25,-80],[20,-87],[15,-83],[9,-83],[7,-77],
            [15,-90],[23,-106],[29,-115],[32,-117],[49,-124],
            [60,-137],[70,-141],[71,-168]
        }),
        // South America
        ("#0F1F0F", new double[][] {
            [12,-71],[10,-62],[2,-50],[-5,-35],[-15,-38],[-22,-43],
            [-33,-53],[-54,-65],[-55,-69],[-52,-75],[-42,-74],[-30,-72],
            [-18,-70],[-5,-81],[2,-80],[8,-77],[12,-71]
        }),
        // Europe
        ("#0F0F1F", new double[][] {
            [71,28],[70,20],[62,5],[51,2],[44,-9],[36,-6],[36,3],
            [37,10],[32,15],[32,25],[36,28],[38,27],[40,26],[42,28],
            [44,29],[46,30],[48,22],[52,14],[54,10],[56,8],[58,5],
            [60,5],[62,5],[65,14],[68,14],[70,20],[71,28]
        }),
        // Africa
        ("#1F0F05", new double[][] {
            [37,10],[32,15],[22,37],[11,42],[2,42],[-4,40],[-10,38],
            [-18,36],[-26,33],[-34,26],[-34,18],[-26,15],[-18,12],
            [-10,13],[-5,8],[2,10],[5,2],[4,-10],[5,-5],[10,-15],
            [15,-17],[21,-17],[26,-15],[30,-10],[32,5],[37,10]
        }),
        // Asia (simplified)
        ("#0F0F1F", new double[][] {
            [71,28],[73,60],[73,100],[68,137],[60,140],[50,142],
            [46,142],[44,133],[38,122],[30,122],[22,114],[20,110],
            [10,105],[1,104],[5,100],[13,100],[23,90],
            [23,80],[10,78],[8,77],[22,60],[22,57],[25,57],
            [30,48],[30,47],[38,40],[38,36],[36,36],[36,28],
            [40,26],[42,28],[44,29],[46,30],[48,22],[52,14],
            [54,10],[58,5],[60,5],[62,5],[65,14],[68,14],[71,28]
        }),
        // Australia
        ("#1F0F05", new double[][] {
            [-14,126],[-13,136],[-12,136],[-12,142],[-16,146],
            [-20,148],[-24,152],[-28,153],[-32,152],[-37,150],
            [-39,147],[-38,140],[-35,136],[-32,134],[-31,129],
            [-33,124],[-34,118],[-31,115],[-25,113],[-22,113],
            [-17,122],[-14,126]
        }),
    ];

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        Loaded += (_, _) =>
        {
            ApplyDarkTitleBar();
            _vm.MapRefreshRequested += () => Dispatcher.Invoke(DrawMap);
        };
    }

    void ApplyDarkTitleBar()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int val = 1;
        DwmSetWindowAttribute(hwnd, 20, ref val, sizeof(int));
    }

    // ── 입력 ────────────────────────────────────────────────────────────────
    void Target_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm.StartCmd.CanExecute(null))
            _vm.StartCmd.Execute(null);
    }

    // ── 지도 렌더링 ──────────────────────────────────────────────────────────
    void MapCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawMap();

    void DrawMap()
    {
        MapCanvas.Children.Clear();
        double w = MapCanvas.ActualWidth;
        double h = MapCanvas.ActualHeight;
        if (w < 10 || h < 10) return;

        DrawGrid(w, h);
        DrawContinents(w, h);
        DrawHops(w, h);
    }

    void DrawGrid(double w, double h)
    {
        for (int lat = -60; lat <= 60; lat += 30)
        {
            double y = LatToY(lat, h);
            bool isEquator = lat == 0;
            var line = new Line
            {
                X1 = 0, Y1 = y, X2 = w, Y2 = y,
                Stroke = isEquator
                    ? new SolidColorBrush(Color.FromArgb(50, 0, 180, 90))
                    : new SolidColorBrush(Color.FromArgb(20, 80, 80, 120)),
                StrokeThickness = isEquator ? 1.2 : 0.5,
            };
            MapCanvas.Children.Add(line);
        }

        for (int lon = -120; lon <= 120; lon += 60)
        {
            double x = LonToX(lon, w);
            var line = new Line
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = h,
                Stroke = new SolidColorBrush(Color.FromArgb(18, 80, 80, 120)),
                StrokeThickness = 0.5,
            };
            MapCanvas.Children.Add(line);
        }
    }

    void DrawContinents(double w, double h)
    {
        foreach (var (colorHex, pts) in Continents)
        {
            var poly = new Polygon
            {
                Fill   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                Stroke = new SolidColorBrush(Color.FromArgb(70, 80, 130, 80)),
                StrokeThickness = 0.8,
            };
            foreach (var p in pts)
                poly.Points.Add(new Point(LonToX(p[1], w), LatToY(p[0], h)));
            MapCanvas.Children.Add(poly);
        }
    }

    void DrawHops(double w, double h)
    {
        var geoHops = _vm.Hops
            .Where(hop => hop.Latitude.HasValue && hop.Longitude.HasValue)
            .ToList();

        if (geoHops.Count == 0) return;

        // 연결선
        for (int i = 1; i < geoHops.Count; i++)
        {
            var prev = geoHops[i - 1];
            var curr = geoHops[i];
            var p1 = LatLonToPoint(prev.Latitude!.Value, prev.Longitude!.Value, w, h);
            var p2 = LatLonToPoint(curr.Latitude!.Value, curr.Longitude!.Value, w, h);

            byte alpha = (byte)(80 + 175 * i / geoHops.Count);
            var line = new Line
            {
                X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
                Stroke = new SolidColorBrush(Color.FromArgb(alpha, 59, 130, 246)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 2 },
            };
            MapCanvas.Children.Add(line);
        }

        // 홉 핀
        for (int i = 0; i < geoHops.Count; i++)
        {
            var hop = geoHops[i];
            var pt  = LatLonToPoint(hop.Latitude!.Value, hop.Longitude!.Value, w, h);
            bool isLast = i == geoHops.Count - 1;

            double r = isLast ? 7 : 4;
            var ellipse = new Ellipse
            {
                Width  = r * 2,
                Height = r * 2,
                Fill   = isLast
                    ? new SolidColorBrush(Color.FromRgb(52, 211, 153))
                    : new SolidColorBrush(Color.FromRgb(96, 165, 250)),
                Stroke = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                StrokeThickness = 1,
            };
            Canvas.SetLeft(ellipse, pt.X - r);
            Canvas.SetTop(ellipse, pt.Y - r);
            MapCanvas.Children.Add(ellipse);

            var tb = new TextBlock
            {
                Text       = hop.HopNumber.ToString(),
                Foreground = Brushes.White,
                FontSize   = 9,
                FontWeight = FontWeights.Bold,
            };
            Canvas.SetLeft(tb, pt.X + r + 2);
            Canvas.SetTop(tb, pt.Y - 8);
            MapCanvas.Children.Add(tb);
        }
    }

    // ── 투영 헬퍼 ───────────────────────────────────────────────────────────
    static double LonToX(double lon, double w) => (lon + 180.0) / 360.0 * w;
    static double LatToY(double lat, double h) => (90.0 - lat) / 180.0 * h;
    static Point LatLonToPoint(double lat, double lon, double w, double h)
        => new(LonToX(lon, w), LatToY(lat, h));

    protected override void OnClosed(EventArgs e)
    {
        _vm.Dispose();
        base.OnClosed(e);
    }
}
