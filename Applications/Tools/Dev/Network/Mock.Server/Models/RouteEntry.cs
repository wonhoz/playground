namespace MockServer.Models;

public class RouteEntry
{
    public string Method      { get; set; } = "GET";
    public string Path        { get; set; } = "/";
    public int    Status      { get; set; } = 200;
    public int    Delay       { get; set; } = 0;
    public string Response    { get; set; } = "{}";
    public string Description { get; set; } = "";
}
