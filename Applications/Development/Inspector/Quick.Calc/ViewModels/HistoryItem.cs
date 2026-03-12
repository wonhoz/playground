namespace QuickCalc.ViewModels;

public class HistoryItem
{
    public ulong Value { get; init; }
    public string Dec => Value.ToString();
    public string Hex => $"0x{Value:X}";
    public string Bin
    {
        get
        {
            if (Value == 0) return "0b0";
            var bits = Convert.ToString((long)Value, 2);
            return "0b" + bits;
        }
    }
    public string Display => $"{Hex}  ({Dec})";
}
