namespace CipherQuest.Models;

public enum CipherType { Caesar, Vigenere, Substitution, RailFence, Enigma }

public class CipherPuzzle
{
    public int    Number     { get; init; }
    public string Title      { get; init; } = "";
    public string CipherText { get; init; } = "";
    public string PlainText  { get; init; } = "";
    public string Key        { get; init; } = "";  // Caesar:"3" / Vigenere:"KEY" / Sub:"QWERTY..." / Rail:"3" / Enigma:"ACE"
    public string Hint       { get; init; } = "";
    public string History    { get; init; } = "";
}

public class Chapter
{
    public int                Number  { get; init; }
    public string             Name    { get; init; } = "";
    public string             Era     { get; init; } = "";
    public CipherType         Type    { get; init; }
    public string             Desc    { get; init; } = "";
    public List<CipherPuzzle> Puzzles { get; init; } = [];
}
