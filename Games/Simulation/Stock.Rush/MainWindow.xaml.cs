using System.Globalization;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using StockRush.Controls;

namespace StockRush;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private MarketEngine _engine = null!;
    private Account _account = null!;
    private NewsEngine _news = null!;
    private readonly TutorialManager _tutorial = new();
    private readonly SaveData _save;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer _bannerTimer = new() { Interval = TimeSpan.FromSeconds(4.5) };
    private readonly ObservableCollection<NewsItem> _newsItems = new();
    private readonly List<BookRow> _bookRows = new();
    private Stock? _selected;
    private bool _muted;
    private bool _inTutorial;
    private int _uiTick;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            var dark = 1;
            DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        };

        // 작은 해상도에서 우측 패널이 잘리지 않도록 작업영역에 맞춤
        var wa = SystemParameters.WorkArea;
        if (Width > wa.Width) { Width = wa.Width; Left = wa.Left; }
        if (Height > wa.Height) { Height = wa.Height; Top = wa.Top; }

        _save = SaveService.Load();

        for (var i = 0; i < 10; i++) _bookRows.Add(new BookRow { IsAsk = true });
        for (var i = 0; i < 10; i++) _bookRows.Add(new BookRow { IsAsk = false });
        BookList.ItemsSource = _bookRows;
        NewsList.ItemsSource = _newsItems;

        _timer.Tick += Timer_Tick;
        _bannerTimer.Tick += (_, _) => { BreakingBanner.Visibility = Visibility.Collapsed; _bannerTimer.Stop(); };

        _tutorial.StepChanged += OnTutorialStep;
        _tutorial.Completed += OnTutorialCompleted;

        // 토글 라디오 동작 보강 (체크 해제 방지)
        BtnSideBuy.Unchecked += (_, _) => { if (BtnSideSell.IsChecked != true) BtnSideBuy.IsChecked = true; };
        BtnSideSell.Unchecked += (_, _) => { if (BtnSideBuy.IsChecked != true) BtnSideSell.IsChecked = true; };
        BtnTypeMarket.Unchecked += (_, _) => { if (BtnTypeLimit.IsChecked != true) BtnTypeMarket.IsChecked = true; };
        BtnTypeLimit.Unchecked += (_, _) => { if (BtnTypeMarket.IsChecked != true) BtnTypeLimit.IsChecked = true; };
        TabPos.Unchecked += (_, _) => EnsureTab();
        TabOrd.Unchecked += (_, _) => EnsureTab();
        TabTrd.Unchecked += (_, _) => EnsureTab();

        Closing += (_, _) => PersistRecord();

        InitGame();
        UpdateRecordText();
    }

    // ── 게임 초기화/모드 ──────────────────────────────────────────
    private void InitGame()
    {
        _engine = new MarketEngine();
        _account = new Account();
        _news = new NewsEngine(_engine);

        _engine.ViTriggered += OnVi;
        _engine.DayClosed += OnDayClosed;
        _news.NewsPublished += OnNews;
        _account.TradeExecuted += OnTrade;

        _newsItems.Clear();
        WatchList.ItemsSource = _engine.Stocks;
        PosList.ItemsSource = _account.Positions;
        OrdList.ItemsSource = _account.OpenOrders;
        TrdList.ItemsSource = _account.Trades;

        _selected = null;
        SelectStock(_engine.Stocks[0]);
        _uiTick = 0;
        UpdateUi(force: true);
    }

    private void BtnStartReal_Click(object sender, RoutedEventArgs e)
    {
        InitGame();
        _inTutorial = false;
        TutorialBar.Visibility = Visibility.Collapsed;
        StartOverlay.Visibility = Visibility.Collapsed;
        _engine.OpenSession();
        _timer.Start();
        PushSystemNews("장 시작! 행운을 빕니다. 초기 자본 1,000만 원.");
    }

    private void BtnTutorial_Click(object sender, RoutedEventArgs e)
    {
        var id = (string)((Button)sender).Tag;
        InitGame();
        _inTutorial = true;

        var target = id == "basic"
            ? _engine.Stocks[0]
            : _engine.Stocks[_engine.Rng.Next(_engine.Stocks.Count)];

        var ctx = new TutorialContext
        {
            Engine = _engine,
            Account = _account,
            News = _news,
            Target = target,
            GetSelected = () => _selected
        };

        StartOverlay.Visibility = Visibility.Collapsed;
        TutorialBar.Visibility = Visibility.Visible;
        _engine.OpenSession();
        _tutorial.Start(id, ctx);
        _timer.Start();
    }

    private void BtnMenu_Click(object sender, RoutedEventArgs e) => GoMenu();

    private void GoMenu()
    {
        _timer.Stop();
        _tutorial.Stop();
        _inTutorial = false;
        PersistRecord();
        TutorialBar.Visibility = Visibility.Collapsed;
        DayEndOverlay.Visibility = Visibility.Collapsed;
        ResultOverlay.Visibility = Visibility.Collapsed;
        BreakingBanner.Visibility = Visibility.Collapsed;
        UpdateRecordText();
        StartOverlay.Visibility = Visibility.Visible;
    }

    private void PersistRecord()
    {
        if (_account == null || _inTutorial) { SaveService.Save(_save); return; }
        var equity = _account.Equity(_engine.Stocks);
        if (equity != Account.InitialCash || _account.Trades.Count > 0)
        {
            if (equity > _save.BestEquity)
            {
                _save.BestEquity = equity;
                _save.BestReturnRate = (double)(equity - Account.InitialCash) / Account.InitialCash * 100.0;
            }
        }
        SaveService.Save(_save);
    }

    private void UpdateRecordText()
    {
        TxtRecord.Text = _save.BestEquity > 0
            ? $"최고 기록: 총자산 {_save.BestEquity:N0}원 ({(_save.BestReturnRate >= 0 ? "+" : "")}{_save.BestReturnRate:F2}%) · 누적 {_save.TotalDaysPlayed}일 거래"
            : "아직 기록이 없습니다. 첫 거래를 시작해 보세요!";
    }

    // ── 메인 루프 ────────────────────────────────────────────────
    private void Timer_Tick(object? sender, EventArgs e)
    {
        _engine.Tick();
        if (_inTutorial) _tutorial.Tick();
        else _news.Tick();

        var filled = _account.MatchLimitOrders(_engine);
        foreach (var o in filled)
            ShowOrderMsg($"지정가 체결: {o.Name} {o.SideText} {o.Qty:N0}주 @ {o.LimitPrice:N0}원",
                o.Side == OrderSide.매수 ? Ui.UpBrush : Ui.DownBrush);

        _uiTick++;
        UpdateUi(force: false);
    }

    private void UpdateUi(bool force)
    {
        var t = _engine.MarketTime;
        TxtClock.Text = $"Day {_engine.Day} · {t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";

        var idxChange = (_engine.IndexValue - _engine.IndexPrevClose) / _engine.IndexPrevClose * 100.0;
        TxtIndex.Text = $"KOSPI {_engine.IndexValue:N2} ({(idxChange >= 0 ? "+" : "")}{idxChange:F2}%)";
        TxtIndex.Foreground = Ui.ForChange(idxChange);

        // 보유 종목 현재가 갱신
        long unrealized = 0;
        foreach (var p in _account.Positions)
        {
            var s = _engine.Find(p.Code);
            if (s != null) p.CurrentPrice = s.Price;
            unrealized += p.Pnl;
        }

        var equity = _account.Equity(_engine.Stocks);
        var totalReturn = (double)(equity - Account.InitialCash) / Account.InitialCash * 100.0;
        TxtEquity.Text = equity.ToString("N0");
        TxtEquity.Foreground = Ui.ForChange(equity - Account.InitialCash);
        TxtUnrealized.Text = $"{(unrealized >= 0 ? "+" : "")}{unrealized:N0}";
        TxtUnrealized.Foreground = Ui.ForChange(unrealized);
        TxtRealized.Text = $"{(_account.RealizedPnlToday >= 0 ? "+" : "")}{_account.RealizedPnlToday:N0}";
        TxtRealized.Foreground = Ui.ForChange(_account.RealizedPnlToday);
        TxtReturn.Text = $"{(totalReturn >= 0 ? "+" : "")}{totalReturn:F2}%";
        TxtReturn.Foreground = Ui.ForChange(totalReturn);
        TxtCash.Text = _account.Cash.ToString("N0");

        if (_selected != null)
        {
            TxtBigPrice.Text = _selected.PriceText;
            TxtBigPrice.Foreground = _selected.TrendBrush;
            TxtBigChange.Text = $"{_selected.ChangeText} ({_selected.ChangeRateText})";
            TxtBigChange.Foreground = _selected.TrendBrush;
            TxtOhlv.Text = $"시 {_selected.DayOpen:N0} · 고 {_selected.DayHigh:N0} · 저 {_selected.DayLow:N0} · 량 {_selected.VolumeText}";

            if (_selected.HaltTicks > 0)
            {
                StateBadge.Visibility = Visibility.Visible;
                TxtStockState.Text = "⚡ VI 발동 — 거래 일시정지";
            }
            else if (_selected.IsUpperLimit)
            {
                StateBadge.Visibility = Visibility.Visible;
                TxtStockState.Text = "🔥 상한가";
            }
            else if (_selected.IsLowerLimit)
            {
                StateBadge.Visibility = Visibility.Visible;
                TxtStockState.Text = "💧 하한가";
            }
            else StateBadge.Visibility = Visibility.Collapsed;

            Chart.AvgPrice = _account.GetPosition(_selected.Code)?.AvgPrice ?? 0;
            Chart.Stock = _selected;
            Chart.InvalidateVisual();

            if (force || _uiTick % 2 == 0) _engine.FillOrderBook(_selected, _bookRows);
        }

        if (force || _uiTick % 5 == 0) UpdateOrderInfo();
    }

    // ── 이벤트 핸들러 (엔진) ─────────────────────────────────────
    private void OnNews(NewsItem item)
    {
        _newsItems.Insert(0, item);
        while (_newsItems.Count > 60) _newsItems.RemoveAt(_newsItems.Count - 1);

        if (item.IsBreaking)
        {
            TxtBreaking.Text = item.Headline;
            BreakingBanner.Visibility = Visibility.Visible;
            _bannerTimer.Stop();
            _bannerTimer.Start();
            FlashScreen(item.Kind == NewsKind.속보호재 ? Color.FromRgb(0xF0, 0x44, 0x52) : Color.FromRgb(0x31, 0x82, 0xF6));
            if (!_muted) SystemSounds.Exclamation.Play();
        }
    }

    private void OnVi(Stock stock)
    {
        PushSystemNews($"[VI] {stock.Name} 변동성 완화장치 발동 — 거래 일시정지");
        FlashScreen(Color.FromRgb(0xFF, 0xB0, 0x00));
        if (!_muted) SystemSounds.Hand.Play();
    }

    private void OnTrade(TradeRecord rec)
    {
        ShowOrderMsg($"체결: {rec.Name} {rec.SideText} {rec.Qty:N0}주 @ {rec.Price:N0}원" +
                     (rec.Side == OrderSide.매도 ? $" (손익 {(rec.RealizedPnl >= 0 ? "+" : "")}{rec.RealizedPnl:N0}원)" : ""),
            rec.Side == OrderSide.매수 ? Ui.UpBrush : Ui.DownBrush);
        if (!_muted) SystemSounds.Asterisk.Play();
    }

    private void OnDayClosed()
    {
        if (_inTutorial)
        {
            _engine.NextDay();
            return;
        }

        _timer.Stop();
        var equity = _account.Equity(_engine.Stocks);
        var unrealized = _account.Positions.Sum(p => p.Pnl);
        var totalReturn = (double)(equity - Account.InitialCash) / Account.InitialCash * 100.0;

        _save.TotalDaysPlayed++;
        PersistRecord();

        TxtDayEndTitle.Text = $"Day {_engine.Day} 장 마감";
        TxtDayEndStats.Text =
            $"총자산  {equity:N0}원\n" +
            $"일 실현손익  {(_account.RealizedPnlToday >= 0 ? "+" : "")}{_account.RealizedPnlToday:N0}원\n" +
            $"평가손익  {(unrealized >= 0 ? "+" : "")}{unrealized:N0}원\n" +
            $"누적 수익률  {(totalReturn >= 0 ? "+" : "")}{totalReturn:F2}%";
        DayEndOverlay.Visibility = Visibility.Visible;
    }

    private void BtnNextDay_Click(object sender, RoutedEventArgs e)
    {
        DayEndOverlay.Visibility = Visibility.Collapsed;
        _account.RealizedPnlToday = 0;
        _engine.NextDay();
        _timer.Start();
        PushSystemNews($"Day {_engine.Day} 장 시작! 오버나이트 갭에 주의하세요.");
    }

    // ── 튜토리얼 ─────────────────────────────────────────────────
    private void OnTutorialStep(string text)
    {
        TxtTutorialStep.Text = $"{_tutorial.StepNumber}/{_tutorial.StepTotal}";
        TxtTutorialText.Text = text;
    }

    private void OnTutorialCompleted(string message)
    {
        _timer.Stop();
        _inTutorial = false;
        TutorialBar.Visibility = Visibility.Collapsed;
        TxtResult.Text = message;
        ResultOverlay.Visibility = Visibility.Visible;
        _save.TutorialCompleted = true;
        SaveService.Save(_save);
    }

    private void BtnTutorialQuit_Click(object sender, RoutedEventArgs e) => GoMenu();

    // ── 주문 UI ──────────────────────────────────────────────────
    private void OrderSide_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (ReferenceEquals(sender, BtnSideBuy)) BtnSideSell.IsChecked = false;
        else BtnSideBuy.IsChecked = false;

        var isBuy = BtnSideBuy.IsChecked == true;
        BtnOrder.Style = (Style)FindResource(isBuy ? "BuyButton" : "SellButton");
        BtnOrder.Content = isBuy ? "매수" : "매도";
        UpdateOrderInfo();
    }

    private void OrderType_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (ReferenceEquals(sender, BtnTypeMarket)) BtnTypeLimit.IsChecked = false;
        else BtnTypeMarket.IsChecked = false;

        var isLimit = BtnTypeLimit.IsChecked == true;
        TxtPrice.IsEnabled = isLimit;
        if (isLimit && _selected != null) TxtPrice.Text = _selected.Price.ToString();
        UpdateOrderInfo();
    }

    private void TxtQty_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateOrderInfo();
    }

    private void QtyPct_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var pct = double.Parse((string)((Button)sender).Tag, CultureInfo.InvariantCulture);

        long qty;
        if (BtnSideBuy.IsChecked == true)
        {
            var price = GetOrderPrice();
            qty = (long)(_account.MaxBuyQty(price) * pct);
        }
        else
        {
            var held = _account.GetPosition(_selected.Code)?.Qty ?? 0;
            qty = (long)(held * pct);
            if (pct >= 1.0) qty = held;
        }
        TxtQty.Text = Math.Max(0, qty).ToString();
    }

    private long GetOrderPrice()
    {
        if (_selected == null) return 0;
        if (BtnTypeLimit.IsChecked == true &&
            long.TryParse(TxtPrice.Text.Replace(",", ""), out var limit) && limit > 0)
            return limit;
        return _selected.Price + MarketEngine.TickSize(_selected.Price); // 시장가 예상 체결가
    }

    private void UpdateOrderInfo()
    {
        if (_selected == null) return;
        var price = GetOrderPrice();
        long.TryParse(TxtQty.Text.Replace(",", ""), out var qty);
        var est = price * Math.Max(0, qty);

        if (BtnSideBuy.IsChecked == true)
            TxtOrderInfo.Text = $"매수가능 {_account.MaxBuyQty(price):N0}주 · 예상금액 {est:N0}원";
        else
        {
            var held = _account.GetPosition(_selected.Code)?.Qty ?? 0;
            TxtOrderInfo.Text = $"보유 {held:N0}주 · 예상금액 {est:N0}원";
        }
    }

    private void BtnOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        if (!_engine.SessionOpen)
        {
            ShowOrderMsg("장이 열려 있지 않습니다.", Ui.WarnBrush);
            return;
        }
        if (_selected.HaltTicks > 0)
        {
            ShowOrderMsg("VI 발동 중 — 거래가 일시 정지된 종목입니다.", Ui.WarnBrush);
            if (!_muted) SystemSounds.Hand.Play();
            return;
        }
        if (!long.TryParse(TxtQty.Text.Replace(",", ""), out var qty) || qty <= 0)
        {
            ShowOrderMsg("수량을 확인하세요.", Ui.WarnBrush);
            return;
        }

        var side = BtnSideBuy.IsChecked == true ? OrderSide.매수 : OrderSide.매도;

        if (BtnTypeMarket.IsChecked == true)
        {
            var price = _engine.FillPrice(_selected, side, qty);
            var err = side == OrderSide.매수
                ? _account.ExecuteBuy(_selected, price, qty, _engine.Day, _engine.MarketTime)
                : _account.ExecuteSell(_selected, price, qty, _engine.Day, _engine.MarketTime);
            if (err != null) ShowOrderMsg(err, Ui.WarnBrush);
        }
        else
        {
            if (!long.TryParse(TxtPrice.Text.Replace(",", ""), out var rawLimit) || rawLimit <= 0)
            {
                ShowOrderMsg("지정가 가격을 확인하세요.", Ui.WarnBrush);
                return;
            }
            var limit = MarketEngine.RoundToTick(rawLimit, false);
            var err = _account.PlaceLimitOrder(_selected, side, limit, qty);
            ShowOrderMsg(err ?? $"지정가 주문 접수: {side} {qty:N0}주 @ {limit:N0}원 (호가단위 적용)",
                err == null ? Ui.FlatBrush : Ui.WarnBrush);
        }
        UpdateOrderInfo();
    }

    private void CancelOrder_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is Order order)
        {
            _account.CancelOrder(order);
            ShowOrderMsg($"주문 취소: {order.Name} {order.SideText} {order.Qty:N0}주", Ui.DimBrush);
        }
    }

    // ── 선택/탭 ──────────────────────────────────────────────────
    private void WatchRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is Stock stock) SelectStock(stock);
    }

    private void PosRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is Position pos)
        {
            var s = _engine.Find(pos.Code);
            if (s != null) SelectStock(s);
        }
    }

    private void BookRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is BookRow row && row.Price > 0)
        {
            if (BtnTypeLimit.IsChecked != true) BtnTypeLimit.IsChecked = true;
            TxtPrice.Text = row.Price.ToString();
            UpdateOrderInfo();
        }
    }

    private void SelectStock(Stock stock)
    {
        if (_selected != null) _selected.IsSelected = false;
        _selected = stock;
        stock.IsSelected = true;

        TxtStockName.Text = stock.Name;
        TxtStockCode.Text = $"{stock.Code} · {stock.Sector}";
        if (BtnTypeLimit.IsChecked == true) TxtPrice.Text = stock.Price.ToString();
        Chart.Stock = stock;
        Chart.AvgPrice = _account.GetPosition(stock.Code)?.AvgPrice ?? 0;
        Chart.InvalidateVisual();
        _engine.FillOrderBook(stock, _bookRows);
        UpdateOrderInfo();
    }

    private void Tab_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (ReferenceEquals(sender, TabPos)) { TabOrd.IsChecked = false; TabTrd.IsChecked = false; }
        else if (ReferenceEquals(sender, TabOrd)) { TabPos.IsChecked = false; TabTrd.IsChecked = false; }
        else { TabPos.IsChecked = false; TabOrd.IsChecked = false; }
        ApplyTab();
    }

    private void EnsureTab()
    {
        if (TabPos.IsChecked != true && TabOrd.IsChecked != true && TabTrd.IsChecked != true)
            TabPos.IsChecked = true;
        ApplyTab();
    }

    private void ApplyTab()
    {
        PosList.Visibility = TabPos.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        OrdList.Visibility = TabOrd.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        TrdList.Visibility = TabTrd.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnMute_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _muted = BtnMute.IsChecked == true;
        BtnMute.Content = _muted ? "🔕" : "🔔";
    }

    // ── 연출 효과 ────────────────────────────────────────────────
    private void FlashScreen(Color color)
    {
        FlashBorder.BorderBrush = new SolidColorBrush(color);
        var anim = new DoubleAnimation(0.9, 0.0, TimeSpan.FromMilliseconds(900))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        FlashBorder.BeginAnimation(OpacityProperty, anim);
    }

    private void ShowOrderMsg(string text, Brush brush)
    {
        TxtOrderMsg.Text = text;
        TxtOrderMsg.Foreground = brush;
        TxtOrderMsg.BeginAnimation(OpacityProperty, null);
        TxtOrderMsg.Opacity = 1;
        var anim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromSeconds(2.5)) { BeginTime = TimeSpan.FromSeconds(3) };
        TxtOrderMsg.BeginAnimation(OpacityProperty, anim);
    }

    private void PushSystemNews(string headline)
    {
        _newsItems.Insert(0, new NewsItem
        {
            Day = _engine.Day,
            Time = _engine.MarketTime,
            Headline = headline,
            Kind = NewsKind.시장
        });
    }
}
