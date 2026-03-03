namespace ServeCast.Models;

/// <summary>서버 설정 모델</summary>
public class ServerConfig
{
    public string   FolderPath    { get; set; } = "";
    public int      Port          { get; set; } = 8080;
    public bool     UseHttps      { get; set; } = false;
    public bool     EnableCors    { get; set; } = false;
    public string   CorsOrigins   { get; set; } = "*";
    public bool     EnableAuth    { get; set; } = false;
    public string   AuthUsername  { get; set; } = "admin";
    public string   AuthPassword  { get; set; } = "";
    public bool     SpaMode       { get; set; } = false;
    public bool     ShowDirectory { get; set; } = true;

    public string Scheme => UseHttps ? "https" : "http";
    public string LocalUrl  => $"{Scheme}://localhost:{Port}";
}
