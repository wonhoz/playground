namespace Quant.Lab.Core.Strategies;

public interface IStrategy
{
    string Name { get; }
    void OnBar(StrategyContext ctx);
}
