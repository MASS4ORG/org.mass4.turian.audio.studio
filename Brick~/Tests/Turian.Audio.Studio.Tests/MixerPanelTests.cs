using System.Numerics;
using Gaya.Sdk;
using Guinevere;
using NSubstitute;
using SkiaSharp;
using Turian.Editor.Audio;
using Xunit;

namespace Turian.Audio.Studio.Tests;

/// <summary>Exercises the optional Studio plugin and live controls through actual headless GUI frames.</summary>
public sealed class MixerPanelTests
{
    /// <summary>Idle panel instructions remain readable against Studio's dark surface.</summary>
    [Fact]
    public void IdlePanelUsesReadableHostThemeText()
    {
        using var frame = new Frame(new AudioMixerPanel(static () => null));
        frame.Draw();
        Assert.True(frame.BrightPixels() > 50);
    }

    /// <summary>The plugin contributes an optional panel, command and View menu through the public SDK.</summary>
    [Fact]
    public void PluginRegistersItsPanelAndCommand()
    {
        var context = Substitute.For<IPluginContext>();
        context.Panels.Returns(Substitute.For<IPanelRegistry>());
        context.Commands.Returns(Substitute.For<ICommandRegistry>());
        context.Menus.Returns(Substitute.For<IMenuRegistry>());
        new AudioMixerStudioPlugin().Configure(context);
        context.Panels.Received().Register(Arg.Is<PanelDescriptor>(static p =>
            p.Id == AudioMixerStudioPlugin.PanelId && !p.OpenByDefault));
        context.Commands.Received().Register(Arg.Is<CommandDescriptor>(static c => c.Id == AudioMixerStudioPlugin.PanelId));
        context.Menus.Received().Add(Arg.Any<MenuItemDescriptor>());
    }

    /// <summary>Mute, solo and snapshots reach the callback, and stopping/replacing a session removes old controls.</summary>
    [Fact]
    public void PanelControlsTheCurrentSession()
    {
        IAudioMixerService? current = null;
        using var frame = new Frame(new AudioMixerPanel(() => current));
        frame.Draw();
        var settings = new AudioSettings { SampleRate = 8000, BufferFrames = 64 };
        var model = new AudioMixer();
        model.Snapshots.Add(new AudioSnapshot { Name = "Pause", Values = [new() { Bus = "Music", Volume = 0.2f }] });
        using var device = new SilentAudioDevice();
        using var audio = new AudioService(device, new AudioMixerGraph(settings, new SimpleSpatializer()),
            settings, mixerModel: model);
        current = audio;
        audio.Prepare(new AudioEvent { EventName = "music", Bus = "Music", Loop = true },
            [AudioClip.FromPcm([0.2f], new(1, 8000))]);
        audio.Post("music", Guid.Empty, new AudioParameterSet());
        var output = new float[8192];
        device.Render(output);
        frame.Draw();
        frame.Click("audio/mute/Music");
        Assert.True(audio.GetBus("Music").Mute);
        device.Render(output);
        Assert.Equal(0, output[^1]);
        frame.Click("audio/mute/Music");
        frame.Click("audio/solo/Music");
        Assert.True(audio.GetBus("Music").Solo);
        frame.Click("audio/snapshot/Pause");
        Assert.Equal(0.2f, audio.GetBus("Music").Volume);
        device.Render(output);
        Assert.Equal(0.04f, output[^1], 5);
        current = null;
        frame.Draw();
        current = new SilentAudioMixerService();
        frame.Draw();
    }

    /// <summary>Meter labels handle silence and gain in a consistent decibel range.</summary>
    [Theory]
    [InlineData(0, -80)]
    [InlineData(1, 0)]
    [InlineData(0.1f, -20)]
    public void MeterDecibels(float amplitude, float expected) => Assert.Equal(expected, AudioMixerPanel.Decibels(amplitude), 4);

    sealed class Frame(IPanel panel) : IDisposable
    {
        readonly SKSurface surface = SKSurface.Create(new SKImageInfo(1000, 1600));
        readonly Font font = Font.FromFamilyName("sans-serif", 14);
        readonly IInputHandler input = Substitute.For<IInputHandler>();
        readonly Gui gui = new();

        internal void Draw()
        {
            surface.Canvas.Clear(new SKColor(34, 34, 34));
            gui.Input = input;
            gui.SetStage(Pass.Pass1Build);
            gui.BeginFrame(surface.Canvas, font, font);
            panel.Render(gui);
            gui.CalculateLayout();
            gui.SetStage(Pass.Pass2Render);
            panel.Render(gui);
            gui.Render();
            gui.EndFrame();
        }

        internal int BrightPixels()
        {
            using var snapshot = surface.Snapshot();
            using var bitmap = SKBitmap.FromImage(snapshot);
            var count = 0;
            foreach (var pixel in bitmap.Pixels)
            {
                if (pixel.Red > 100 && pixel.Green > 100 && pixel.Blue > 100) { count++; }
            }
            return count;
        }

        internal void Click(string id)
        {
            var node = Find(gui.RootNode!, id) ?? throw new InvalidOperationException($"Missing control {id}");
            input.MousePosition.Returns(node.Center);
            input.PrevMousePosition.Returns(node.Center);
            input.IsMouseButtonPressed(MouseButton.Left).Returns(true);
            Draw();
            input.IsMouseButtonPressed(MouseButton.Left).Returns(false);
            input.MousePosition.Returns(new Vector2(-1, -1));
        }

        internal void Type(string id, string text)
        {
            Click(id);
            input.IsKeyDown(KeyboardKey.LeftControl).Returns(true);
            input.IsKeyPressed(KeyboardKey.A).Returns(true);
            Draw();
            input.IsKeyDown(KeyboardKey.LeftControl).Returns(false);
            input.IsKeyPressed(KeyboardKey.A).Returns(false);
            input.GetTypedCharacters().Returns(text);
            Draw();
            input.GetTypedCharacters().Returns(string.Empty);
        }

        static LayoutNode? Find(LayoutNode root, string id)
        {
            if (root.Id == id) { return root; }
            foreach (var child in root.Children)
            {
                if (Find(child, id) is { } found) { return found; }
            }
            return null;
        }

        public void Dispose()
        {
            (panel as IDisposable)?.Dispose();
            surface.Dispose();
        }
    }
}
