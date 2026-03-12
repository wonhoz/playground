namespace Geo.Quiz.Models;

public enum QuizMode
{
    Capital,    // 수도 맞추기
    Flag,       // 국기 이모지로 나라 맞추기
    Continent,  // 대륙 맞추기
}

public enum QuizScreen { Start, Quiz, Result }

public class QuizQuestion
{
    public Country Subject { get; init; } = null!;
    public string QuestionText { get; init; } = "";
    public string FlagIsoCode  { get; init; } = "";   // Flag 모드: 2자리 ISO 코드 (KR, JP ...)
    public string CorrectAnswer { get; init; } = "";
    public List<string> Choices { get; init; } = [];
}

public class QuizResult
{
    public int Total    { get; set; }
    public int Correct  { get; set; }
    public int Wrong    { get; set; }
    public double Score => Total == 0 ? 0 : (double)Correct / Total * 100;
    public string Grade => Score switch
    {
        >= 90 => "A",
        >= 80 => "B",
        >= 70 => "C",
        >= 60 => "D",
        _     => "F",
    };
}
