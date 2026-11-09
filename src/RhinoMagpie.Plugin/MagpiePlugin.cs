using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.PlugIns;
using Rhino.UI;
using RhinoMagpie.Agent;

[assembly: Guid("ad8574d5-1868-455f-915f-c93780651477")]
[assembly: PlugInDescription(DescriptionType.Organization, "mikidaren")]
[assembly: PlugInDescription(DescriptionType.WebSite, "https://github.com/mikidaren/Magpie-Rhino")]
[assembly: PlugInDescription(DescriptionType.UpdateUrl, "https://github.com/mikidaren/Magpie-Rhino/releases")]

namespace RhinoMagpie.Plugin;

public sealed class MagpiePlugin : PlugIn
{
    internal static MagpiePlugin? Instance { get; private set; }
    internal NativeAiIntegration? NativeAi { get; private set; }
    public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        try
        {
            MagpieGateway.EnsureConfigurationDirectory();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            errorMessage = "无法创建 Magpie 连接目录，请检查用户目录的写入权限。";
            return LoadReturnCode.ErrorShowDialog;
        }
        Panels.RegisterPanel(this, typeof(MagpiePanel), "Magpie-Rhino", null);
        Instance = this;
        NativeAi = new NativeAiIntegration();
        return LoadReturnCode.Success;
    }

    protected override void OnShutdown()
    {
        NativeAi?.Dispose();
        NativeAi = null;
        Instance = null;
        base.OnShutdown();
    }
}

public sealed class MagpieCommand : Command
{
    public override string EnglishName => "Magpie";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (MagpiePlugin.Instance?.NativeAi?.OpenMagpie() == true) return Result.Success;
        if (!Panels.IsPanelVisible(typeof(MagpiePanel)) &&
            Panels.OpenPanelAsSibling(typeof(MagpiePanel).GUID, PanelIds.Layers, true))
            return Result.Success;
        Panels.OpenPanel(typeof(MagpiePanel).GUID);
        return Result.Success;
    }
}

public sealed class MagpieStandaloneCommand : Command
{
    public override string EnglishName => "MagpieStandalone";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        Panels.OpenPanel(typeof(MagpiePanel).GUID);
        return Result.Success;
    }
}
