using System.Collections.ObjectModel;

namespace ApiProbe.Models;

public class ApiCollection
{
    public Guid                          Id       { get; set; } = Guid.NewGuid();
    public string                        Name     { get; set; } = "New Collection";
    public ObservableCollection<ApiRequest> Requests { get; set; } = [];
}
