namespace Dict.Cast.ViewModels;

using Dict.Cast.Models;

/// <summary>
/// WordSense 표시용 뷰모델 — XAML 바인딩에서 문자열 가공 처리
/// </summary>
public class SenseViewModel
{
    readonly WordSense _sense;
    readonly int       _index;

    public SenseViewModel(WordSense sense, int index)
    {
        _sense = sense;
        _index = index;
    }

    public string Pos => _sense.PartOfSpeech;

    public string PosDisplay => _sense.PartOfSpeech switch
    {
        "noun"      => "n.",
        "verb"      => "v.",
        "adjective" => "adj.",
        "adverb"    => "adv.",
        _           => _sense.PartOfSpeech
    };

    public string PosColor => _sense.PartOfSpeech switch
    {
        "noun"      => "#60A5FA",
        "verb"      => "#4ADE80",
        "adjective" => "#FB923C",
        "adverb"    => "#C084FC",
        _           => "#9CA3AF"
    };

    public string NumberedDefinition => $"{_index}. {_sense.Definition}";

    public string Definition => _sense.Definition;

    public bool HasSynonyms => _sense.Synonyms.Count > 0;
    public bool HasAntonyms => _sense.Antonyms.Count > 0;
    public bool HasExamples => _sense.Examples.Count > 0;

    public string SynonymsText
        => "유의어: " + string.Join("  ·  ", _sense.Synonyms.Take(7));

    public string AntonymsText
        => "반의어: " + string.Join("  ·  ", _sense.Antonyms.Take(4));

    public string ExamplesText
        => string.Join("   ", _sense.Examples.Take(2).Select(e => $"\"{e}\""));
}
