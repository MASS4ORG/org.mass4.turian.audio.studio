using System.Globalization;
using Gaya.Sdk;
using Guinevere;
using Turian.Audio;

namespace Turian.Editor.Audio;

/// <summary>Live faders, mute/solo, snapshot auditioning and independent stereo meters for the active Play session.</summary>
public sealed class AudioMixerPanel(Func<IAudioMixerService?> current) : IPanel
{
    float transition = 0.5f;
    string message = "";

    /// <inheritdoc/>
    public void Render(Gui gui)
    {
        ArgumentNullException.ThrowIfNull(gui);
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(6).Padding(8).Enter())
        {
            gui.ScrollY();
            if (current() is not { } mixer)
            {
                Line(gui, "Start Play Mode to audition the mix.");
                Line(gui, "Assign an Audio Mixer in Audio Settings. Edit buses and effects in its Inspector.");
                return;
            }
            if (mixer.Buses.Count == 0)
            {
                Line(gui, "The active audio backend does not expose bus controls.");
                return;
            }
            foreach (var bus in mixer.Buses) { Bus(gui, mixer, bus); }
            if (mixer.Snapshots.Count > 0)
            {
                Line(gui, "Snapshot transition (seconds)");
                gui.Slider(ref transition, 0, 5, width: 260, showValue: true, id: "audio/snapshot/duration");
                foreach (var snapshot in mixer.Snapshots)
                {
                    if (Button(gui, snapshot, "audio/snapshot/" + snapshot))
                    {
                        Accepted(mixer.TransitionTo(snapshot, transition));
                    }
                }
            }
            if (message.Length > 0) { Line(gui, message); }
        }
    }

    void Bus(Gui gui, IAudioMixerService mixer, string bus)
    {
        var controls = mixer.GetBus(bus);
        Line(gui, bus);
        using (gui.Node().Direction(Axis.Horizontal).Gap(8).Height(30).Enter())
        {
            var original = controls.Volume <= 0 ? -80 : 20 * MathF.Log10(controls.Volume);
            var db = original;
            gui.Slider(ref db, -80, 20 * MathF.Log10(16), width: 260, id: "audio/gain/" + bus);
            if (db != original) { Accepted(mixer.SetBusVolume(bus, db <= -80 ? 0 : MathF.Pow(10, db / 20))); }
            Line(gui, db <= -80 ? "Silence" : db.ToString("0.0", CultureInfo.InvariantCulture) + " dB", 80);
            var mute = gui.Checkbox(controls.Mute, "Mute", id: "audio/mute/" + bus);
            var solo = gui.Checkbox(controls.Solo, "Solo", id: "audio/solo/" + bus);
            if (mute != controls.Mute) { Accepted(mixer.SetBusMute(bus, mute)); }
            if (solo != controls.Solo) { Accepted(mixer.SetBusSolo(bus, solo)); }
        }
        var meter = mixer.GetMeter(bus);
        Meter(gui, "L", meter.LeftPeak, meter.LeftRms, "audio/meter/left/" + bus);
        Meter(gui, "R", meter.RightPeak, meter.RightRms, "audio/meter/right/" + bus);
    }

    void Accepted(bool accepted) => message = accepted ? "" : "Audio controls are busy; try again.";

    static void Meter(Gui gui, string channel, float peak, float rms, string id)
    {
        using (gui.Node(-1, 20, id).Direction(Axis.Horizontal).Gap(8).Enter())
        {
            Line(gui, channel, 20);
            using (gui.Node(260, 12).Enter())
            {
                var rect = gui.CurrentNode.Rect;
                gui.DrawRect(rect, Color.FromArgb(255, 35, 40, 45));
                var amount = Math.Clamp((Decibels(peak) + 60) / 60, 0, 1);
                gui.DrawRect(new Rect(rect.X, rect.Y, rect.W * amount, rect.H),
                    peak >= 1 ? Color.FromArgb(255, 235, 80, 60) : Color.FromArgb(255, 80, 190, 120));
            }
            Line(gui, $"Peak {Decibels(peak):0.0} dB   RMS {Decibels(rms):0.0} dB", 260);
        }
    }

    /// <summary>Converts finite linear amplitudes to a display range ending at negative eighty decibels.</summary>
    public static float Decibels(float amplitude) => amplitude > 0 ? Math.Max(-80, 20 * MathF.Log10(amplitude)) : -80;

    static void Line(Gui gui, string text, float width = -1)
    {
        var theme = ThemeTokens.Current;
        using (gui.Node(width, 24).Enter()) { gui.DrawText(text, theme.Text(14), theme.Ink); }
    }

    static bool Button(Gui gui, string text, string id)
    {
        using (gui.Node(-1, 28, id).Enter())
        {
            var theme = ThemeTokens.Current;
            gui.DrawBackgroundRect(gui.GetInteractable().OnHover() ? theme.Hover : theme.Field, 3);
            gui.DrawText(text, theme.Text(14), theme.Ink);
            return gui.Pass == Pass.Pass2Render && gui.GetInteractable().OnClick();
        }
    }
}
