namespace Dict.Cast.Models;

public record WordSense(
    string       PartOfSpeech,
    string       Definition,
    List<string> Synonyms,
    List<string> Antonyms,
    List<string> Examples
);
