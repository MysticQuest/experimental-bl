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
    /// Written in the conditional on purpose. This was built for one person's own campaign, the
    /// long game has not been played through end to end, and stating any of it as fact would be
    /// promising something nobody has verified. "Theoretically" is doing honest work here.
    /// </remarks>
    internal static class Guidance
    {
        internal static readonly Action MidCampaign = Show;

        private const string Title = "Information";

        private const string Body =
            "First, the honest part. This mod was built for its author's own campaign and then " +
            "tidied up enough to share. It has not been played through a long campaign from " +
            "beginning to end, the balance is a set of opinions rather than a tested result, and " +
            "it may well crash. If a campaign starts misbehaving, the Troubleshooting group exists " +
            "precisely for that: switch things off from the bottom up until it settles.\n \n" +
            "THEORETICALLY, then, and with everything below said in that spirit:\n \n" +
            "Nothing here should be unsafe to change in an existing campaign, and no earned XP " +
            "should ever be deleted. What differs is when a change ought to take hold.\n \n" +
            "SHOULD TAKE HOLD AT ONCE  --  the bonus switches, both strength dials, whether other " +
            "heroes get attribute bonuses, and the per-skill XP rates.\n \n" +
            "SHOULD NEED THE CAMPAIGN RELOADED  --  the progression curve itself: the level 330 is " +
            "reachable at, career length, early pace, the wall at the top, and the strength of the " +
            "global skill bonuses. The curve is solved once when a campaign loads, so a save and a " +
            "load is what ought to bring a new shape into play.\n \n" +
            "SHOULD NEED THE GAME RESTARTED  --  anything marked (needs restart). Those are patches " +
            "and models the game will generally only accept while it is starting up.\n \n" +
            "WHAT YOU MAY SEE.  A skill's level is its stored XP read against the curve, so changing " +
            "the curve changes the level that XP buys. A gentler curve, or switching it off for " +
            "vanilla's, will probably push your skills up on the next load; a harsher one should " +
            "pull them down. The XP behind them is not touched either way, so in principle the move " +
            "reverses if you change your mind.\n \n" +
            "TURNING THE CURVE OFF.  Progression curve off is meant to leave every other part of the " +
            "mod running -- the attribute bonuses, the skill bonuses, the XP rates and your " +
            "children's inheritance should all carry on. That is the intended way to keep the extras " +
            "and play at vanilla's pace.";

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
