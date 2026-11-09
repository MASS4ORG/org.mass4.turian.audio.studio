using Gaya.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Turian.Audio;
using Turian.Editor.Core;

namespace Turian.Editor.Audio;

/// <summary>Adds the audio mixer's live controls and metering to Studio through Gaya's public panel registry.</summary>
[Plugin("turian.audio.studio", "Audio Mixer")]
[PluginDependsOn("gaya.turian")]
public sealed class AudioMixerStudioPlugin : IPlugin
{
    /// <summary>Stable panel and command identity.</summary>
    public const string PanelId = "turian.audio.studio.mixer";

    /// <inheritdoc/>
    public void Configure(IPluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Panels.Register(new PanelDescriptor(PanelId, "Audio Mixer", PanelPlacement.Bottom,
            static services => new AudioMixerPanel(() => services.GetRequiredService<PlayModeService>()
                .Services?.GetService<IAudioMixerService>())) { OpenByDefault = false });
        context.Commands.Register(new CommandDescriptor(PanelId, "Audio Mixer",
            static services => services.GetRequiredService<IShellHost>().ShowPanel(PanelId)));
        context.Menus.Add(new MenuItemDescriptor(MenuIds.View, PanelId, "8"));
    }
}
