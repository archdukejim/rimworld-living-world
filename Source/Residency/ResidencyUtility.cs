using System;
using System.Reflection;
using Verse;

namespace RimSynapse.LivingWorld.Residency
{
    /// <summary>
    /// The one place residency is read and written. Everything in this mod goes through here rather
    /// than touching <see cref="ResidentPawnComp"/> directly, and Core is handed
    /// <see cref="IsResident"/> as a provider so Conversations and Psychology can ask without
    /// either side taking a dependency on the other.
    /// </summary>
    public static class ResidencyUtility
    {
        /// <summary>Whether this pawn lives in a generated dwelling. Safe on null and on pawns with no comp.</summary>
        public static bool IsResident(Pawn pawn)
        {
            if (pawn == null) return false;

            var comp = pawn.TryGetComp<ResidentPawnComp>();
            if (comp == null) return false;

            EnsureLegacyAdopted(pawn, comp);
            return comp.isResident;
        }

        /// <summary>Mark or unmark a pawn as a resident.</summary>
        public static void SetResident(Pawn pawn, bool value)
        {
            if (pawn == null) return;

            var comp = pawn.TryGetComp<ResidentPawnComp>();
            if (comp == null)
            {
                // A humanlike without the comp means the injector did not see its ThingDef, which is
                // worth knowing about rather than silently dropping the write.
                Log.WarningOnce(
                    $"[RimSynapse-LivingWorld] Cannot set residency on {pawn.LabelShortCap}: no ResidentPawnComp. " +
                    "Its ThingDef was not caught by ResidencyInjector.", 0x5265_7331);
                return;
            }

            comp.isResident = value;
            comp.legacyChecked = true; // an explicit write beats anything a legacy save had to say
        }

        // -------------------------------------------------------------------------------------
        // Migration off Core's comp
        // -------------------------------------------------------------------------------------

        private static bool legacyFieldResolved;
        private static FieldInfo legacyField;
        private static Type legacyCompType;

        /// <summary>
        /// Adopt <c>isResident</c> from Core's <c>SynapseCorePawnComp</c> the first time a pawn from
        /// a pre-migration save is asked about.
        ///
        /// <para>This matters more than most migrations: dwelling generation runs <b>only at map
        /// generation</b>, so a residency flag that is dropped can never be re-derived. The pawns
        /// would be permanently ordinary, and the settlement they live in would stop reacting to
        /// being looted.</para>
        ///
        /// <para>Done lazily per pawn rather than in a load hook because comp load order between two
        /// mods' comps on the same pawn is not something either mod controls. By the time anybody
        /// asks the question, both comps have finished loading.</para>
        ///
        /// <para>Entirely reflective: this mod must build and run with Core absent, so it cannot
        /// name Core's type. With no Core, or after the field is eventually removed there, this
        /// resolves to nothing and every pawn simply keeps whatever this mod already recorded.</para>
        /// </summary>
        private static void EnsureLegacyAdopted(Pawn pawn, ResidentPawnComp comp)
        {
            if (comp.legacyChecked) return;
            comp.legacyChecked = true;

            try
            {
                if (!legacyFieldResolved)
                {
                    legacyFieldResolved = true;
                    legacyCompType = GenTypes.GetTypeInAnyAssembly("RimSynapse.Comps.SynapseCorePawnComp");
                    if (legacyCompType != null)
                    {
                        legacyField = legacyCompType.GetField("isResident", BindingFlags.Public | BindingFlags.Instance);
                    }
                }

                if (legacyCompType == null || legacyField == null) return;

                foreach (var other in pawn.AllComps)
                {
                    if (other == null || !legacyCompType.IsInstanceOfType(other)) continue;
                    if (legacyField.GetValue(other) is bool wasResident && wasResident)
                    {
                        comp.isResident = true;
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.WarningOnce(
                    $"[RimSynapse-LivingWorld] Could not read legacy residency from Core: {ex.Message}", 0x5265_7332);
            }
        }

        // -------------------------------------------------------------------------------------
        // Publishing to Core
        // -------------------------------------------------------------------------------------

        /// <summary>
        /// Hand <see cref="IsResident"/> to Core so Conversations, Psychology and Core's own
        /// resident-aware paths can ask without anybody holding a hard reference.
        ///
        /// <para>Reflection only, and every branch logs — a provider that silently failed to
        /// register is indistinguishable from one answering "nobody is a resident", which is the
        /// same failure class as an unbound Harmony patch.</para>
        /// </summary>
        public static void RegisterWithCore()
        {
            try
            {
                var providers = GenTypes.GetTypeInAnyAssembly("RimSynapse.SynapseCoreProviders");
                if (providers == null)
                {
                    Log.Message("[RimSynapse-LivingWorld] RimSynapse Core not detected. Skipping residency provider registration.");
                    return;
                }

                var slot = providers.GetProperty("Residency", BindingFlags.Public | BindingFlags.Static);
                if (slot == null || !slot.CanWrite)
                {
                    Log.Warning("[RimSynapse-LivingWorld] SynapseCoreProviders has no writable 'Residency' slot; residency will not be visible to other mods.");
                    return;
                }

                slot.SetValue(null, (Func<Pawn, bool>)IsResident);
                Log.Message("[RimSynapse-LivingWorld] Registered residency provider to RimSynapse Core successfully.");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimSynapse-LivingWorld] Error registering residency provider: {ex}");
            }
        }
    }
}
