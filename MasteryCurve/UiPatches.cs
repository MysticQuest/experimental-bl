using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace MasteryCurve
{
    /// <summary>
    /// Focus runs to 10, but the character screen only ever drew five pips.
    /// </summary>
    /// <remarks>
    /// <c>SkillPointsContainerListPanel</c> lights child <c>i</c> whenever <c>CurrentFocusLevel</c>
    /// reaches <c>i + 1</c>, and it loops over its own child count rather than a hardcoded five, so
    /// ten children work with no widget change at all. The panel itself is replaced rather than
    /// added to, because it is a horizontal list: anything inserted beside it flows along the row
    /// instead of stacking, and the pips have to be narrowed to keep the original footprint anyway.
    /// </remarks>
    internal static class FocusPips
    {
        internal static XmlDocument Build(int width, int height, string brush, int marginLeft, int marginRight,
                                          string? alignment, string? marginBottomValue)
        {
            var document = new XmlDocument();
            var panel = document.CreateElement("SkillPointsContainerListPanel");
            panel.SetAttribute("WidthSizePolicy", "CoverChildren");
            panel.SetAttribute("HeightSizePolicy", "CoverChildren");
            panel.SetAttribute("VerticalAlignment", "Center");
            panel.SetAttribute("CurrentFocusLevel", "@CurrentFocusLevel");
            panel.SetAttribute("IsEnabled", "false");
            panel.SetAttribute("DoNotAcceptEvents", "true");
            if (alignment != null) panel.SetAttribute("HorizontalAlignment", alignment);
            if (marginBottomValue != null)
            {
                panel.SetAttribute("VerticalAlignment", "Bottom");
                panel.SetAttribute("MarginBottom", marginBottomValue);
                panel.SetAttribute("MarginRight", "5");
            }

            var children = document.CreateElement("Children");
            for (var i = 0; i < 10; i++)
            {
                var pip = document.CreateElement("BrushWidget");
                pip.SetAttribute("WidthSizePolicy", "Fixed");
                pip.SetAttribute("HeightSizePolicy", "Fixed");
                pip.SetAttribute("SuggestedWidth", width.ToString());
                pip.SetAttribute("SuggestedHeight", height.ToString());
                pip.SetAttribute("Brush", brush);
                if (marginLeft > 0) pip.SetAttribute("MarginLeft", marginLeft.ToString());
                pip.SetAttribute("MarginRight", marginRight.ToString());
                pip.SetAttribute("ForcePixelPerfectRenderPlacement", "true");
                children.AppendChild(pip);
            }

            panel.AppendChild(children);
            document.AppendChild(panel);
            return document;
        }
    }

    /// <summary>Ten small pips on each skill tile, in the width five used to take.</summary>
    [PrefabExtension("SkillGridItem", "descendant::SkillPointsContainerListPanel")]
    internal sealed class SmallFocusPipsPatch : PrefabExtensionReplacePatch
    {
        public override string Id => "MasteryCurve.SmallFocusPips";

        // Five pips took 5 x (11 + 5) = 80px. Ten take 10 x (6 + 2) = the same 80px.
        public override XmlDocument GetPrefabExtension() =>
            FocusPips.Build(6, 30, "Skill.Point.Small", 0, 2, "Right", "!SkillPoints.MarginBottom");
    }

    /// <summary>Ten large pips under the inspected skill.</summary>
    [PrefabExtension("CharacterDeveloper", "descendant::SkillPointsContainerListPanel")]
    internal sealed class LargeFocusPipsPatch : PrefabExtensionReplacePatch
    {
        public override string Id => "MasteryCurve.LargeFocusPips";

        // Five pips took 5 x (20 + 2 + 2) = 120px. Ten take 10 x (10 + 1 + 1) = the same 120px.
        public override XmlDocument GetPrefabExtension() =>
            FocusPips.Build(10, 64, "Skill.Point.Big", 1, 1, null, null);
    }
}
