namespace QrForge.Models;

public class VCardData
{
    public string Name    { get; set; } = string.Empty;
    public string Phone   { get; set; } = string.Empty;
    public string Email   { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Url     { get; set; } = string.Empty;

    public string ToVCardString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("BEGIN:VCARD");
        sb.AppendLine("VERSION:3.0");
        if (!string.IsNullOrWhiteSpace(Name))    sb.AppendLine($"FN:{Name}");
        if (!string.IsNullOrWhiteSpace(Phone))   sb.AppendLine($"TEL:{Phone}");
        if (!string.IsNullOrWhiteSpace(Email))   sb.AppendLine($"EMAIL:{Email}");
        if (!string.IsNullOrWhiteSpace(Company)) sb.AppendLine($"ORG:{Company}");
        if (!string.IsNullOrWhiteSpace(Url))     sb.AppendLine($"URL:{Url}");
        sb.AppendLine("END:VCARD");
        return sb.ToString().TrimEnd();
    }
}
