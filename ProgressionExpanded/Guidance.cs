using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ProgressionExpanded
{
    /// <summary>
    /// The one thing a settings page cannot say in a tooltip: what happens if you change
    /// something while a campaign is already running.
    /// </summary>
    /// <remarks>
    /// It matters here more than in most mods, because the three groups behave differently and
    /// the difference is invisible. Nothing in this mod edits stored XP -- what changes is what
    /// that XP is worth, which is why a skill can read differently after a reload without a
    /// single point having been lost.
    /// </remarks>
    internal static class Guidance
    {
        internal static readonly Action MidCampaign = Show;

        private const string Title = "Changing settings mid-campaign";

        private const string Body =
            "Nothing here is unsafe to change in an existing campaign, and no earned XP is ever " +
            "deleted. What differs is when a change takes hold.\n \n" +
            "TAKES HOLD AT ONCE  --  the bonus switches, both strength dials, whether other heroes " +
            "get attribute bonuses, and the per-skill XP rates. Alt-tab back in and they are live.\n \n" +
            "NEEDS THE CAMPAIGN RELOADED  --  the progression curve itself: the level 330 is " +
            "reachable at, career length, early pace and the wall at the top, and the strength of " +
            "the global skill bonuses. The curve is solved once when a campaign loads, so a save " +
            "and a load is what brings a new shape into play.\n \n" +
            "NEEDS THE GAME RESTARTED  --  anything marked (needs restart). Those are patches and " +
            "models the game only accepts while it is starting up.\n \n" +
            "WHAT YOU WILL SEE.  A skill's level is its stored XP read against the curve, so " +
            "changing the curve changes the level that XP buys. Making the curve gentler, or " +
            "switching it off for vanilla's, will push your skills up on the next load; making it " +
            "harsher will pull them down. The XP behind them is untouched either way, so the move " +
            "reverses exactly if you change your mind.\n \n" +
            "TURNING THE CURVE OFF.  Progression curve off leaves every other part of the mod " +
            "running -- the attribute bonuses, the skill bonuses, the XP rates and your children's " +
            "inheritance all carry on. It is the supported way to keep the extras and play vanilla " +
            "pacing, and it is what the Closer to vanilla preset does not do: that one reshapes the " +
            "curve instead of removing it.";

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
