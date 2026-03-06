using System.ComponentModel;

namespace IconHunt.Models;

public class IconCollection : INotifyPropertyChanged
{
    public string Prefix { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Total { get; set; }
    public string License { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
    }

    private bool _isIndexed;
    public bool IsIndexed
    {
        get => _isIndexed;
        set { _isIndexed = value; OnPropertyChanged(nameof(IsIndexed)); OnPropertyChanged(nameof(StatusText)); }
    }

    public string StatusText => IsIndexed ? $"{Total:N0}개" : "미인덱스";

    // 사전 정의된 인기 라이브러리 목록
    public static readonly IReadOnlyList<IconCollection> DefaultCollections = new[]
    {
        new IconCollection { Prefix = "mdi",              Name = "Material Design Icons",  Total = 7447, License = "Apache 2.0",  Author = "Pictogrammers",    Url = "https://materialdesignicons.com" },
        new IconCollection { Prefix = "material-symbols", Name = "Material Symbols",       Total = 2866, License = "Apache 2.0",  Author = "Google",           Url = "https://fonts.google.com/icons" },
        new IconCollection { Prefix = "heroicons",        Name = "Heroicons",              Total = 592,  License = "MIT",         Author = "Tailwind Labs",    Url = "https://heroicons.com" },
        new IconCollection { Prefix = "ph",               Name = "Phosphor Icons",         Total = 9072, License = "MIT",         Author = "Phosphor Icons",   Url = "https://phosphoricons.com" },
        new IconCollection { Prefix = "lucide",           Name = "Lucide",                 Total = 1554, License = "ISC",         Author = "Lucide Contributors", Url = "https://lucide.dev" },
        new IconCollection { Prefix = "tabler",           Name = "Tabler Icons",           Total = 5765, License = "MIT",         Author = "Paweł Kuna",       Url = "https://tabler-icons.io" },
        new IconCollection { Prefix = "bi",               Name = "Bootstrap Icons",        Total = 2060, License = "MIT",         Author = "The Bootstrap Team", Url = "https://icons.getbootstrap.com" },
        new IconCollection { Prefix = "feather",          Name = "Feather",                Total = 286,  License = "MIT",         Author = "Cole Bemis",       Url = "https://feathericons.com" },
        new IconCollection { Prefix = "ri",               Name = "Remix Icon",             Total = 3084, License = "Apache 2.0",  Author = "Remix Design",     Url = "https://remixicon.com" },
        new IconCollection { Prefix = "carbon",           Name = "Carbon",                 Total = 2259, License = "Apache 2.0",  Author = "IBM",              Url = "https://carbondesignsystem.com/guidelines/icons" },
        new IconCollection { Prefix = "ic",               Name = "Google Material Icons",  Total = 3794, License = "Apache 2.0",  Author = "Google",           Url = "https://fonts.google.com/icons" },
        new IconCollection { Prefix = "fa-solid",         Name = "Font Awesome Solid",     Total = 1390, License = "CC BY 4.0",   Author = "Font Awesome",     Url = "https://fontawesome.com" },
        new IconCollection { Prefix = "fa-regular",       Name = "Font Awesome Regular",   Total = 165,  License = "CC BY 4.0",   Author = "Font Awesome",     Url = "https://fontawesome.com" },
        new IconCollection { Prefix = "la",               Name = "Line Awesome",           Total = 1544, License = "MIT",         Author = "Icons8",           Url = "https://icons8.com/line-awesome" },
        new IconCollection { Prefix = "ion",              Name = "IonIcons",               Total = 1354, License = "MIT",         Author = "Ionic",            Url = "https://ionic.io/ionicons" },
        new IconCollection { Prefix = "octicon",          Name = "Octicons",               Total = 465,  License = "MIT",         Author = "GitHub",           Url = "https://primer.style/design/foundations/icons" },
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
