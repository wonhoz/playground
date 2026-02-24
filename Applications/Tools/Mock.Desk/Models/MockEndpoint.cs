namespace MockDesk.Models;

public class MockEndpoint
{
    public Guid   Id           { get; set; } = Guid.NewGuid();
    public bool   Enabled      { get; set; } = true;
    public string Method       { get; set; } = "GET";
    public string Path         { get; set; } = "/api/example";
    public int    StatusCode   { get; set; } = 200;
    public string ResponseBody { get; set; } = "{\n  \"message\": \"OK\"\n}";
    public int    DelayMs      { get; set; } = 0;
    public string Description  { get; set; } = "";
}
