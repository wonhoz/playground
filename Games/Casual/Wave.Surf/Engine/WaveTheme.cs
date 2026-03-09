using System.Windows.Media;

namespace WaveSurf.Engine;

/// <summary>파도 색상 테마 (열대 / 폭풍 / 일몰 / 오로라)</summary>
public record WaveTheme(
    string Name,
    Color SkyTop,
    Color SkyBottom,
    Color WaveLight,
    Color WaveDark,
    Color FoamColor,
    Color SurferColor,
    Color HudColor
)
{
    public static readonly WaveTheme[] All =
    [
        new("🌴 열대",
            SkyTop:     Color.FromRgb(0x4A, 0xC8, 0xFF),
            SkyBottom:  Color.FromRgb(0x00, 0x90, 0xD0),
            WaveLight:  Color.FromRgb(0x00, 0x80, 0xC8),
            WaveDark:   Color.FromRgb(0x00, 0x3A, 0x70),
            FoamColor:  Color.FromRgb(0xFF, 0xFF, 0xFF),
            SurferColor: Color.FromRgb(0xFF, 0xD0, 0x40),
            HudColor:   Color.FromRgb(0xFF, 0xFF, 0xFF)),

        new("⛈ 폭풍",
            SkyTop:     Color.FromRgb(0x20, 0x28, 0x38),
            SkyBottom:  Color.FromRgb(0x10, 0x18, 0x28),
            WaveLight:  Color.FromRgb(0x18, 0x30, 0x50),
            WaveDark:   Color.FromRgb(0x08, 0x10, 0x20),
            FoamColor:  Color.FromRgb(0x90, 0xA8, 0xC0),
            SurferColor: Color.FromRgb(0xE0, 0xE0, 0xE0),
            HudColor:   Color.FromRgb(0xC0, 0xD8, 0xFF)),

        new("🌅 일몰",
            SkyTop:     Color.FromRgb(0xFF, 0x70, 0x20),
            SkyBottom:  Color.FromRgb(0xC0, 0x30, 0x10),
            WaveLight:  Color.FromRgb(0x80, 0x18, 0x30),
            WaveDark:   Color.FromRgb(0x30, 0x08, 0x18),
            FoamColor:  Color.FromRgb(0xFF, 0xD0, 0x80),
            SurferColor: Color.FromRgb(0xFF, 0xF0, 0xA0),
            HudColor:   Color.FromRgb(0xFF, 0xE0, 0x80)),

        new("🌌 오로라",
            SkyTop:     Color.FromRgb(0x04, 0x12, 0x20),
            SkyBottom:  Color.FromRgb(0x00, 0x20, 0x18),
            WaveLight:  Color.FromRgb(0x00, 0x50, 0x38),
            WaveDark:   Color.FromRgb(0x00, 0x18, 0x14),
            FoamColor:  Color.FromRgb(0x60, 0xFF, 0xA0),
            SurferColor: Color.FromRgb(0x80, 0xFF, 0xC0),
            HudColor:   Color.FromRgb(0x60, 0xFF, 0xA0)),
    ];
}
