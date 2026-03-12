namespace MockServer.Models;

/// <summary>GUI + 서버 통합 라우트 모델 (Id, Enabled는 GUI 전용)</summary>
public class MockRoute
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public bool   Enabled     { get; set; } = true;
    public string Method      { get; set; } = "GET";
    public string Path        { get; set; } = "/api/example";
    public int    Status      { get; set; } = 200;
    public int    Delay       { get; set; } = 0;
    public string Response    { get; set; } = "{\n  \"message\": \"OK\"\n}";
    public string Description { get; set; } = "";

    public static MockRoute FromEntry(RouteEntry e) => new()
    {
        Method = e.Method, Path = e.Path, Status = e.Status,
        Delay = e.Delay, Response = e.Response, Description = e.Description
    };

    public RouteEntry ToEntry() => new()
    {
        Method = Method, Path = Path, Status = Status,
        Delay = Delay, Response = Response, Description = Description
    };
}
