// The comp surface the residency code actually meets, plus stand-ins for the two Core types it
// reaches for by name. Both fakes are declared here on purpose: RimWorldStubs' GenTypes does a real
// assembly scan, so declaring them in the test assembly is what makes the reflection paths in
// ResidencyUtility genuinely exercisable rather than merely compiled.
using System;
using System.Collections.Generic;

namespace Verse
{
    public class ThingComp
    {
        public virtual void PostExposeData() { }
    }

    public partial class Pawn
    {
        public string LabelShortCap = "Pawn";
        public readonly List<ThingComp> AllComps = new List<ThingComp>();

        public T TryGetComp<T>() where T : ThingComp
        {
            foreach (var c in AllComps) if (c is T typed) return typed;
            return null;
        }
    }
}

namespace RimSynapse.Comps
{
    /// <summary>
    /// Stands in for Core's pawn comp as it exists before the migration — only the one field
    /// residency ever read. Resolved by name through GenTypes, exactly as the real one is.
    /// </summary>
    public class SynapseCorePawnComp : Verse.ThingComp
    {
        public bool isResident = false;
    }
}

namespace RimSynapse
{
    /// <summary>
    /// Stands in for Core's provider registry. Only the slot residency registers into; the real
    /// contract is pinned by Core's own ProviderTests.
    /// </summary>
    public static class SynapseCoreProviders
    {
        public static Func<Verse.Pawn, bool> Residency { get; set; }
        public static Func<int, int> PopulationDensity { get; set; }
    }
}
