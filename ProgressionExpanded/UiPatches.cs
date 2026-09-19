using System;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace ProgressionExpanded
{
    /// <summary>
    /// Draws as many focus pips as the settings actually allow.
    /// </summary>
    /// <remarks>
    /// <c>SkillPointsContainerListPanel</c> lights child <c>i</c> whenever <c>CurrentFocusLevel</c>
    /// reaches <c>i + 1</c>, and it loops over its own child count rather than a hardcoded five, so
    /// any number of children works. The panel is replaced rather than added to, because it is a
    /// horizontal list: anything inserted beside it flows along the row instead of stacking.
    ///
    /// Prefabs are built once when the UI loads, so the pip count is read at that moment. Changing
    /// the maximum mid-campaign changes the rules immediately but not the drawing until a restart.
    /// </remarks>
    internal static class FocusPips
    {
        /// <summary>Total width the original five pips occupied, which the new row has to match.</summary>
        internal static XmlDocument Build(int totalWidth, int height, string brush, bool splitMargin, bool bottomRight)
        {
            var count = Math.Max(1, Math.Min(10, Settings.Instance?.MaxFocusPerSkill ?? 10));
            var slot = Math.Max(4, totalWidth / count);
            var margin = count > 5 ? 2 : 4;
            var width = Math.Max(3, slot - margin);

            var document = new XmlDocument();
            var panel = document.CreateElement("SkillPointsContainerListPanel");
            panel.SetAttribute("WidthSizePolicy", "CoverChildren");
            panel.SetAttribute("HeightSizePolicy", "CoverChildren");
            panel.SetAttribute("CurrentFocusLevel", "@CurrentFocusLevel");
            panel.SetAttribute("IsEnabled", "false");
            panel.SetAttribute("DoNotAcceptEvents", "true");
            if (bottomRight)
            {
                panel.SetAttribute("HorizontalAlignment", "Right");
                panel.SetAttribute("VerticalAlignment", "Bottom");
                panel.SetAttribute("MarginBottom", "!SkillPoints.MarginBottom");
                panel.SetAttribute("MarginRight", "5");
            }
            else
            {
                panel.SetAttribute("VerticalAlignment", "Center");
            }

            var children = document.CreateElement("Children");
            for (var i = 0; i < count; i++)
            {
                var pip = document.CreateElement("BrushWidget");
                pip.SetAttribute("WidthSizePolicy", "Fixed");
                pip.SetAttribute("HeightSizePolicy", "Fixed");
                pip.SetAttribute("SuggestedWidth", width.ToString());
                pip.SetAttribute("SuggestedHeight", height.ToString());
                pip.SetAttribute("Brush", brush);
                if (splitMargin)
                {
                    pip.SetAttribute("MarginLeft", (margin / 2).ToString());
                    pip.SetAttribute("MarginRight", (margin - margin / 2).ToString());
                }
                else
                {
                    pip.SetAttribute("MarginRight", margin.ToString());
                }

                pip.SetAttribute("ForcePixelPerfectRenderPlacement", "true");
                children.AppendChild(pip);
            }

            panel.AppendChild(children);
            document.AppendChild(panel);
            return document;
        }
    }

    /// <summary>The small pips on each skill tile, in the 80px five used to take.</summary>
    [PrefabExtension("SkillGridItem", "descendant::SkillPointsContainerListPanel")]
    internal sealed class SmallFocusPipsPatch : PrefabExtensionReplacePatch
    {
        public override string Id => "ProgressionExpanded.SmallFocusPips";
        public override XmlDocument GetPrefabExtension() =>
            FocusPips.Build(80, 30, "Skill.Point.Small", false, true);
    }

    /// <summary>The large pips under the inspected skill, in the 120px five used to take.</summary>
    [PrefabExtension("CharacterDeveloper", "descendant::SkillPointsContainerListPanel")]
    internal sealed class LargeFocusPipsPatch : PrefabExtensionReplacePatch
    {
        public override string Id => "ProgressionExpanded.LargeFocusPips";
        public override XmlDocument GetPrefabExtension() =>
            FocusPips.Build(120, 64, "Skill.Point.Big", true, false);
    }
}
