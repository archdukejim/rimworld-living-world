using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimSynapse.LivingWorld.Patches
{
    [HarmonyPatch(typeof(Designator_Claim), "DesignateThing")]
    public static class Patch_Designator_Claim_DesignateThing
    {
        public static void Prefix(Thing t)
        {
            if (t == null || t.Faction == null || t.Faction.def.isPlayer) return;

            // Check if there are any resident pawns on the map belonging to this faction
            if (t.Map != null)
            {
                var residentPawns = t.Map.mapPawns.AllPawns
                    .Where(p => p.Faction == t.Faction && p.RaceProps.Humanlike);

                bool hasResident = false;
                foreach (var p in residentPawns)
                {
                    if (Residency.ResidencyUtility.IsResident(p))
                    {
                        hasResident = true;
                        break;
                    }
                }

                if (hasResident)
                {
                    // Make the faction immediately hostile
                    t.Faction.TryAffectGoodwillWith(Faction.OfPlayer, -100, true, true);
                    Messages.Message($"Claiming property inside the settlement has angered the residents! Faction {t.Faction.Name} is now hostile.", MessageTypeDefOf.ThreatBig, true);
                }
            }
        }
    }
}
