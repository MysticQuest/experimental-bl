using System;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Library;

namespace MasteryCurve
{
    /// <summary>
    /// Focus runs to 10, but the character screen only draws five pips. This adds a second row.
    /// </summary>
    /// <remarks>
    /// <c>SkillPointsContainerListPanel</c> lights child <c>i</c> whenever <c>CurrentFocusLevel</c>
    /// reaches <c>i + 1</c>, and it loops over its own child count rather than a hardcoded five.
    /// So a second panel works as-is, except that it would light its first pip at focus 1. The
    /// mixin below exposes the upper half of the value so the second row starts counting at six.
    /// </remarks>
    [ViewModelMixin("RefreshWithCurrentValues")]
    internal sealed class SkillFocusRowMixin : BaseViewModelMixin<SkillVM>
    {
        private const int PipsPerRow = 5;
        private int _upper;

        public SkillFocusRowMixin(SkillVM vm) : base(vm) => Recalculate();

        [DataSourceProperty]
        public int CurrentFocusLevelUpper
        {
            get => _upper;
            set
            {
                if (_upper == value) return;
                _upper = value;
                OnPropertyChangedWithValue(value, nameof(CurrentFocusLevelUpper));
            }
        }

        public override void OnRefresh() => Recalculate();

        private void Recalculate() =>
            CurrentFocusLevelUpper = Math.Max(0, (ViewModel?.CurrentFocusLevel ?? 0) - PipsPerRow);
    }

    /// <summary>Shared builder for a second pip row bound to the upper half of the focus value.</summary>
    internal static class FocusRow
    {
        internal static XmlDocument Build(int pipWidth, int pipHeight, string brush, int marginRight, int marginBottom)
        {
            var document = new XmlDocument();
            var panel = document.CreateElement("SkillPointsContainerListPanel");
            Set(panel, "WidthSizePolicy", "CoverChildren");
            Set(panel, "HeightSizePolicy", "CoverChildren");
            Set(panel, "HorizontalAlignment", "Right");
            Set(panel, "VerticalAlignment", "Bottom");
            Set(panel, "MarginRight", marginRight.ToString());
            Set(panel, "MarginBottom", marginBottom.ToString());
            Set(panel, "CurrentFocusLevel", "@CurrentFocusLevelUpper");
            Set(panel, "IsEnabled", "false");
            Set(panel, "DoNotAcceptEvents", "true");

            var children = document.CreateElement("Children");
            for (var i = 0; i < 5; i++)
            {
                var pip = document.CreateElement("BrushWidget");
                Set(pip, "WidthSizePolicy", "Fixed");
                Set(pip, "HeightSizePolicy", "Fixed");
                Set(pip, "SuggestedWidth", pipWidth.ToString());
                Set(pip, "SuggestedHeight", pipHeight.ToString());
                Set(pip, "Brush", brush);
                Set(pip, "MarginRight", i == 4 ? "2" : "5");
                Set(pip, "ForcePixelPerfectRenderPlacement", "true");
                children.AppendChild(pip);
            }

            panel.AppendChild(children);
            document.AppendChild(panel);
            return document;
        }

        private static void Set(XmlElement element, string name, string value) => element.SetAttribute(name, value);
    }

    // ---- the small pips on every skill tile ----

    [PrefabExtension("SkillGridItem", "descendant::SkillPointsContainerListPanel/Children/BrushWidget")]
    internal sealed class SmallPipHeightPatch : PrefabExtensionSetAttributePatch
    {
        public override string Id => "MasteryCurve.SmallPipHeight";
        public override string Attribute => "SuggestedHeight";
        public override string Value => "12";          // 0.4 of the original 30, so two rows fit the slot
    }

    [PrefabExtension("SkillGridItem", "descendant::SkillPointsContainerListPanel")]
    internal sealed class SmallPipRowOffsetPatch : PrefabExtensionSetAttributePatch
    {
        public override string Id => "MasteryCurve.SmallPipRowOffset";
        public override string Attribute => "MarginBottom";
        public override string Value => "8";           // the lower row, pinned so the upper one lands cleanly
    }

    // The Prefabs2 insert patch carries no content member -- it only describes where to insert --
    // so the original API is still the one that can supply an XmlDocument. Obsolete, not broken.
#pragma warning disable CS0618
    [PrefabExtension("SkillGridItem", "descendant::SkillGridItemButtonWidget/Children")]
    internal sealed class SmallUpperRowPatch : PrefabExtensionInsertPatch
    {
        public override string Id => "MasteryCurve.SmallUpperRow";
        public override int Position => 0;
        public override XmlDocument GetPrefabExtension() => FocusRow.Build(11, 12, "Skill.Point.Small", 5, 22);
    }

    // ---- the large pips on the inspected skill ----

    [PrefabExtension("CharacterDeveloper", "descendant::SkillPointsContainerListPanel/Children/BrushWidget")]
    internal sealed class LargePipHeightPatch : PrefabExtensionSetAttributePatch
    {
        public override string Id => "MasteryCurve.LargePipHeight";
        public override string Attribute => "SuggestedHeight";
        public override string Value => "26";          // 0.4 of the original 64
    }

    [PrefabExtension("CharacterDeveloper", "descendant::SkillPointsContainerListPanel")]
    internal sealed class LargePipRowOffsetPatch : PrefabExtensionSetAttributePatch
    {
        public override string Id => "MasteryCurve.LargePipRowOffset";
        public override string Attribute => "VerticalAlignment";
        public override string Value => "Bottom";
    }

    [PrefabExtension("CharacterDeveloper", "descendant::SkillPointsContainerListPanel/..")]
    internal sealed class LargeUpperRowPatch : PrefabExtensionInsertPatch
    {
        public override string Id => "MasteryCurve.LargeUpperRow";
        public override int Position => 0;
        public override XmlDocument GetPrefabExtension() => FocusRow.Build(20, 26, "Skill.Point.Big", 2, 30);
    }
#pragma warning restore CS0618
}
