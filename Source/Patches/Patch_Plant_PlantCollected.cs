using HarmonyLib;
using RimSynapse.LivingWorld.Residency;
using RimWorld;
using Verse;

namespace RimSynapse.LivingWorld.Patches
{
    /// <summary>
    /// When a dwelling resident harvests a crop, flag their homestead for a passing-trader visit
    /// (an abstract swap performed by <see cref="MapComponent_ResidentCare"/> — no walking pawn).
    /// A no-op for every other harvester, so the vanilla harvest path is untouched.
    /// </summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]
    internal static class Patch_Plant_PlantCollected
    {
        [HarmonyPostfix]
        static void Postfix(Plant __instance, Pawn by)
        {
            if (by == null || __instance == null) return;
            if (!ResidentCareSettings.Active) return;
            if (!ResidencyUtility.IsResident(by)) return;

            Map map = by.MapHeld ?? __instance.MapHeld;
            var care = map?.GetComponent<MapComponent_ResidentCare>();
            care?.NotifyResidentHarvest(by, __instance);
        }
    }
}
