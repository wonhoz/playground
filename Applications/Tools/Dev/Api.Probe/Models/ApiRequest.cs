using System.Collections.ObjectModel;

namespace ApiProbe.Models;

public class ApiRequest
{
    public Guid                      Id          { get; set; } = Guid.NewGuid();
    public string                    Name        { get; set; } = "New Request";
    public string                    Method      { get; set; } = "GET";
    public string                    Url         { get; set; } = "";
    public ObservableCollection<HeaderItem> Headers { get; set; } = [];
    public string                    Body        { get; set; } = "";
    public string                    ContentType { get; set; } = "application/json";
}
