using GolfCast.Models;

namespace GolfCast.Views;

public record HoleRow(int HoleNum, string HoleName, int Par, int Score, bool IsHoleIn1)
{
    public bool IsUnderPar => Score < Par;
    public bool IsOverPar  => Score > Par;
    public string ToPar    => (Score - Par) switch { < 0 => (Score-Par).ToString(), 0 => "E", _ => $"+{Score-Par}" };
}

public partial class ScoreCardWindow : Window
{
    public bool Replay { get; private set; }

    public ScoreCardWindow(ScoreCard card)
    {
        InitializeComponent();
        TxtCourse.Text = $"{card.Course.Name} ({card.Course.Level switch { Difficulty.Easy => "쉬움", Difficulty.Normal => "보통", _ => "어려움" }})";

        var rows = card.Course.Holes.Select((h, i) =>
            new HoleRow(h.Number, h.Name, h.Par,
                i < card.Scores.Count ? card.Scores[i] : 0,
                i < card.HoleIn1.Count && card.HoleIn1[i])).ToList();

        HoleList.ItemsSource = rows;

        TxtTotal.Text = $"{card.Total}타";
        TxtToPar.Text = card.ToPar switch { < 0 => $"{card.ToPar}", 0 => "이븐 파", _ => $"+{card.ToPar}" };

        int hio = card.HoleIn1.Count(x => x);
        TxtResult.Text = $"총 {card.Total}타 / 파 {card.TotalPar}" + (hio > 0 ? $" 🎯 홀인원 {hio}회" : "");
    }

    private void OnClose(object sender, RoutedEventArgs e) { Replay = false; Close(); }
    private void OnReplay(object sender, RoutedEventArgs e) { Replay = true; Close(); }
}
