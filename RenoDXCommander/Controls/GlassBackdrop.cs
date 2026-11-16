// GlassBackdrop.cs -- BONG DLSS5's window backdrop: Windows' desktop acrylic that stays see-through when the window
// isn't focused. The stock DesktopAcrylicBackdrop follows window activation and turns into a flat grey while the
// window is inactive (user: "ugly when unfocused"); this one keeps IsInputActive on, so it looks the same either way.

using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace RenoDXCommander.Controls;

internal sealed class GlassBackdrop : SystemBackdrop
{
    private DesktopAcrylicController? _controller;
    private SystemBackdropConfiguration? _config;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(target, xamlRoot);
        _config = new SystemBackdropConfiguration { IsInputActive = true, Theme = SystemBackdropTheme.Dark };
        _controller = new DesktopAcrylicController
        {
            Kind = DesktopAcrylicKind.Base,
            TintColor = Windows.UI.Color.FromArgb(0xFF, 0x12, 0x14, 0x1C),
            TintOpacity = 0.35f,
            LuminosityOpacity = 0.55f,
            FallbackColor = Windows.UI.Color.FromArgb(0xFF, 0x16, 0x18, 0x22),
        };
        _controller.AddSystemBackdropTarget(target);
        _controller.SetSystemBackdropConfiguration(_config);
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        base.OnTargetDisconnected(target);
        if (_controller == null) return;
        _controller.RemoveSystemBackdropTarget(target);
        _controller.Dispose();
        _controller = null;
        _config = null;
    }
}
