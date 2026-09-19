using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ProgressionExpanded
{
    /// <summary>
    /// What the settings page cannot fit in a tooltip: what this mod is, and what happens if you
    /// change something while a campaign is already running.
    /// </summary>
    /// <remarks>
    /// Written the way you would say it to someone, and hedged on purpose. This was built for one
    /// person's own campaign, the long game has not been played end to end, and stating any of it
    /// flatly would be promising something nobody has checked.
    /// </remarks>
    internal static class Guidance
    {
        internal static readonly Action MidCampaign = Show;

        private const string Title = "Information";

        private const string Body =
            "Fair warning first. I made this for my own campaign and then cleaned it up enough to " +
            "put online. I have not played a full lifetime through with it. The balance is what " +
            "felt right to me rather than anything properly tested, and it may well crash. If a " +
            "campaign starts acting up, open Troubleshooting and start switching things off from " +
            "the bottom -- that is what it is there for.\n \n" +
            "So, theoretically:\n \n" +
            "You should be able to change anything here mid-campaign without hurting your save, and " +
            "nothing deletes XP you have earned. The only real question is when a change kicks in.\n \n" +
            "Most of it takes effect straight away -- the bonus switches, the two strength sliders, " +
            "the other-heroes toggle, and the XP rate for each skill.\n \n" +
            "The curve settings need a reload. That is the level 330 is reachable at, career length, " +
            "early pace, the wall at the top, and the strength of the skill bonuses. The curve gets " +
            "worked out once when a campaign loads, so save and load again and it should pick up the " +
            "new shape.\n \n" +
            "Anything marked (needs restart) wants the game closed and opened again. Those are " +
            "patches the game will only take while it is starting up.\n \n" +
            "One thing that catches people out: your skill levels can move after a reload. A skill " +
            "level is really just your stored XP measured against the curve, so if the curve changes, " +
            "the same XP buys a different level. Make things easier and your skills jump up; make " +
            "them harder and they drop. Nothing is lost either way -- put the setting back and they " +
            "should come back with it.\n \n" +
            "And turning the curve off does not turn the mod off. The attribute bonuses, the skill " +
            "bonuses, the XP rates and everything your children inherit all keep running. That is " +
            "the whole reason it is its own switch: vanilla pacing, with the rest still there.";

        private static void Show()
        {
            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    Title, Body, true, false, "Close", null, null, null));
            }
            catch (Exception exception)
            {
                Guard.Report("Guidance", exception);
            }
        }
    }
}
