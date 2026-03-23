namespace DiffPrompt.Models;

public class Experiment
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string UserMessage { get; set; } = "";
    public string PromptA { get; set; } = "";
    public string PromptB { get; set; } = "";
    public string ModelA { get; set; } = "claude-sonnet-4-6";
    public string ModelB { get; set; } = "claude-sonnet-4-6";
    public string OutputA { get; set; } = "";
    public string OutputB { get; set; } = "";
    public int TokensA { get; set; }
    public int TokensB { get; set; }
    public double CostA { get; set; }
    public double CostB { get; set; }
    public double LatencyAMs { get; set; }
    public double LatencyBMs { get; set; }
    public int? WinnerVote { get; set; }  // 1 = A 승, 2 = B 승, 0 = 무승부
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Tags { get; set; } = "";
    public string Notes { get; set; } = "";
}

public static class ClaudeModels
{
    public static readonly string[] All =
    [
        "claude-opus-4-6",
        "claude-sonnet-4-6",
        "claude-haiku-4-5-20251001",
    ];

    // 입력 토큰당 비용 ($/1M tokens)
    public static double InputCostPer1M(string model) => model switch
    {
        "claude-opus-4-6"           => 15.0,
        "claude-sonnet-4-6"         => 3.0,
        "claude-haiku-4-5-20251001" => 0.25,
        _ => 3.0
    };

    // 출력 토큰당 비용 ($/1M tokens)
    public static double OutputCostPer1M(string model) => model switch
    {
        "claude-opus-4-6"           => 75.0,
        "claude-sonnet-4-6"         => 15.0,
        "claude-haiku-4-5-20251001" => 1.25,
        _ => 15.0
    };

    public static double CalcCost(string model, int inputTokens, int outputTokens)
        => inputTokens / 1_000_000.0 * InputCostPer1M(model)
         + outputTokens / 1_000_000.0 * OutputCostPer1M(model);
}
