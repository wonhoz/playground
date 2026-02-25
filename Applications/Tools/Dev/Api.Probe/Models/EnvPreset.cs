using System.Collections.Generic;

namespace ApiProbe.Models;

public class EnvPreset
{
    public string                     Name      { get; set; } = "";
    public Dictionary<string, string> Variables { get; set; } = [];
}
