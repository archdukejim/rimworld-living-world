using Verse;

namespace RimSynapse.LivingWorld.Residency
{
    /// <summary>
    /// Whether a pawn lives in a dwelling this mod generated, rather than merely standing in one.
    ///
    /// <para>This mod generates the dwellings and the pawns that occupy them, and is the only writer
    /// of residency anywhere in the org — every other mod only reads. Under the rule that a
    /// mechanic's state and logic belong to the mod that introduces it, the flag lives here and is
    /// persisted here.</para>
    ///
    /// <para>It used to be <c>isResident</c> on Core's <c>SynapseCorePawnComp</c>, which meant this
    /// mod could not load without Core installed — it held a hard type reference to a comp in an
    /// assembly it does not declare a dependency on. <see cref="ResidencyUtility"/> publishes the
    /// answer back to Core for the mods that consume it.</para>
    /// </summary>
    public class ResidentPawnComp : ThingComp
    {
        public bool isResident;

        /// <summary>
        /// True once this pawn has been considered for adoption of Core's legacy <c>isResident</c>.
        ///
        /// <para>Per-pawn rather than global because pawns arrive from several places — world pawns
        /// loaded up front, map pawns on their map's load, and pawns that were never in a save at
        /// all. A single "migration done" flag would skip whichever group happened to load after it
        /// was set.</para>
        /// </summary>
        public bool legacyChecked;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isResident, "isResident", false);
            Scribe_Values.Look(ref legacyChecked, "legacyChecked", false);
        }
    }
}
