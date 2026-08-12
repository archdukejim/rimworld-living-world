using HarmonyLib;
using Verse;

namespace RimSynapse.LivingWorld.Patches
{
    /// <summary>
    /// On player-home map generation, populate the map with inhabited homesteads. The inhabitant
    /// count comes from <see cref="PopulationSource"/> — Regions &amp; Territories' per-tile figure
    /// when that mod is present, otherwise a world-seed-deterministic fallback — so this hook works
    /// whether or not R&amp;T (and Map Mode Framework) are installed.
    /// </summary>
    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateMap))]
    internal static class Patch_MapGenerator_GenerateMap
    {
        [HarmonyPostfix]
        static void Postfix(Map __result)
        {
            if (__result == null || !__result.IsPlayerHome) return;

            int pop = PopulationSource.GetPopulationForTile(__result);
            if (pop > 0)
            {
                DwellingStructureGenerator.Generate(__result, pop);
            }
        }
    }
}
