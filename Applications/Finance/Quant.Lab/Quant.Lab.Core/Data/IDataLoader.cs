namespace Quant.Lab.Core.Data;

public interface IDataLoader
{
    IReadOnlyList<OhlcBar> Load();
}
