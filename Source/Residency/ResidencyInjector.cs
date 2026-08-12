using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimSynapse.LivingWorld.Residency
{
    /// <summary>
    /// Attaches <see cref="ResidentPawnComp"/> to every humanlike ThingDef at startup.
    ///
    /// <para>Done in code rather than by XML patch so it covers humanlike races added by other mods
    /// without this mod having to know their defNames — the same approach Core uses for its own
    /// pawn comp.</para>
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ResidencyInjector
    {
        static ResidencyInjector()
        {
            int injected = 0;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs.Where(d => d.race != null && d.race.Humanlike))
            {
                if (def.comps == null)
                {
                    def.comps = new List<CompProperties>();
                }

                if (def.comps.Any(c => c.compClass == typeof(ResidentPawnComp))) continue;

                def.comps.Add(new CompProperties { compClass = typeof(ResidentPawnComp) });
                injected++;
            }

            Log.Message($"[RimSynapse-LivingWorld] Residency comp injected into {injected} humanlike def(s).");
        }
    }
}
