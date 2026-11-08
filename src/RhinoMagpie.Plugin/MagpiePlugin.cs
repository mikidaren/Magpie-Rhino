using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.PlugIns;
using Rhino.UI;

[assembly: Guid("ad8574d5-1868-455f-915f-c93780651477")]
[assembly: PlugInDescription(DescriptionType.Organization, "mikidaren")]
[assembly: PlugInDescription(DescriptionType.WebSite, "https://github.com/mikidaren/rhino9-magpie")]
[assembly: PlugInDescription(DescriptionType.UpdateUrl, "https://github.com/mikidaren/rhino9-magpie/releases")]

namespace RhinoMagpie.Plugin;

public sealed class MagpiePlugin : PlugIn
{
    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        Panels.RegisterPanel(this, typeof(MagpiePanel), "Magpie", null);
        return LoadReturnCode.Success;
    }
}

public sealed class MagpieCommand : Command
{
    public override string EnglishName => "Magpie";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (!Panels.IsPanelVisible(typeof(MagpiePanel)) &&
            Panels.OpenPanelAsSibling(typeof(MagpiePanel).GUID, PanelIds.Layers, true))
            return Result.Success;
        Panels.OpenPanel(typeof(MagpiePanel).GUID);
        return Result.Success;
    }
}
