namespace Dict.Cast.Services;

using System.Speech.Synthesis;

public class TtsService : IDisposable
{
    SpeechSynthesizer? _synth;

    public TtsService()
    {
        try { _synth = new SpeechSynthesizer(); }
        catch { /* SAPI 사용 불가 환경 무시 */ }
    }

    public void Speak(string text)
    {
        if (_synth == null || string.IsNullOrWhiteSpace(text)) return;
        _synth.SpeakAsyncCancelAll();
        _synth.Rate   = 0;
        _synth.Volume = 100;
        _synth.SpeakAsync(text);
    }

    public void Dispose() => _synth?.Dispose();
}
