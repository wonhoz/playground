namespace Color.Grade.Models;

public class ImageAdjustments
{
    public double Exposure   { get; set; }  // -2.0 ~ +2.0  (EV)
    public double Contrast   { get; set; }  // -1.0 ~ +1.0
    public double Saturation { get; set; }  // -1.0 ~ +1.0
    public double Temperature { get; set; } // -1.0 ~ +1.0  (cool←0→warm)
    public double Highlights { get; set; }  // -1.0 ~ +1.0
    public double Shadows    { get; set; }  // -1.0 ~ +1.0

    public bool IsIdentity =>
        Exposure   == 0 && Contrast == 0 && Saturation == 0 &&
        Temperature == 0 && Highlights == 0 && Shadows == 0;
}
