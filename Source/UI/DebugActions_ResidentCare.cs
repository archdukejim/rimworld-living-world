using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimSynapse.LivingWorld.Residency;
using RimWorld;
using Verse;

namespace RimSynapse.LivingWorld.UI
{
    /// <summary>
    /// Debug validation for the 0.8 resident self-sufficiency layer (#78), grouped under "RimSynapse"
    /// and headlessly triggerable via the dev-tools bridge (run_debug_action). Each entry sets up its
    /// own scenario so it proves function on a bare quicktest map with no pre-existing homesteads, and
    /// emits a <c>[SYNAPSE-TEST] PASS/FAIL</c> line the log triage picks up.
    /// </summary>
    public static class DebugActions_ResidentCare
    {
        [DebugAction("RimSynapse", "LW: TEST equip resident supplies (#78)", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TestEquipResident()
        {
            Map map = Find.CurrentMap;
            if (map == null) { Log.Message("[SYNAPSE-TEST] FAIL RT_ResidentEquip | no current map"); return; }

            IntVec3 cell = CellFinder.RandomClosewalkCellNear(map.Center, map, 15);
            Pawn pawn = DwellingStructureGenerator.SpawnResidentForTesting(map, cell);
            if (pawn == null) { Log.Message("[SYNAPSE-TEST] FAIL RT_ResidentEquip | could not spawn resident"); return; }

            int meals = InventoryCount(pawn, ThingDefOf.MealSimple);
            int meds = InventoryMedicineCount(pawn);
            ThingWithComps weapon = pawn.equipment?.Primary;
            bool weaponOk = weapon != null
                            && ResidentEquipUtility.IsAllowedWeapon(weapon.def)
                            && weapon.def.BaseMarketValue <= ResidentCareSettings.weaponValueThreshold;

            bool ceActive = CombatExtendedCompat.Active;
            // In a non-CE test env there is no ammo to check; when CE is present, the ammo shows up as a
            // non-meal / non-medicine inventory stack.
            bool ammoPresent = !ceActive || pawn.inventory.innerContainer.Any(t =>
                t.def != ThingDefOf.MealSimple && !t.def.IsMedicine);

            bool pass = meals >= ResidentCareSettings.startingMeals
                        && meds >= ResidentCareSettings.startingMedicine
                        && weaponOk && ammoPresent;

            Log.Message($"[SYNAPSE-TEST] {(pass ? "PASS" : "FAIL")} RT_ResidentEquip | meals={meals}/{ResidentCareSettings.startingMeals} " +
                        $"medicine={meds}/{ResidentCareSettings.startingMedicine} weapon={weapon?.def?.defName ?? "none"} " +
                        $"value={(weapon?.def?.BaseMarketValue ?? -1f):0} nonAoe={(weapon != null && ResidentEquipUtility.IsAllowedWeapon(weapon.def))} " +
                        $"ceActive={ceActive} ammoPresent={ammoPresent}");
        }

        [DebugAction("RimSynapse", "LW: TEST resident care — self-tend (#78)", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TestResidentSelfTend()
        {
            Map map = Find.CurrentMap;
            if (map == null) { Log.Message("[SYNAPSE-TEST] FAIL RT_ResidentCare_SelfTend | no current map"); return; }

            var care = map.GetComponent<MapComponent_ResidentCare>();
            if (care == null) { Log.Message("[SYNAPSE-TEST] FAIL RT_ResidentCare_SelfTend | no MapComponent_ResidentCare"); return; }

            IntVec3 cell = CellFinder.RandomClosewalkCellNear(map.Center, map, 15);
            Pawn pawn = DwellingStructureGenerator.SpawnResidentForTesting(map, cell);
            if (pawn == null) { Log.Message("[SYNAPSE-TEST] FAIL RT_ResidentCare_SelfTend | could not spawn resident"); return; }

            // Give a fresh tendable injury, then ask the caretaker what it would do.
            pawn.TakeDamage(new DamageInfo(DamageDefOf.Cut, 12f, 999f, -1f, null));
            bool needsTend = pawn.health.HasHediffsNeedingTend();
            string assigned = care.TryAssignCare(pawn);

            bool pass = needsTend && assigned == JobDefOf.TendPatient.defName;
            Log.Message($"[SYNAPSE-TEST] {(pass ? "PASS" : "FAIL")} RT_ResidentCare_SelfTend | needsTend={needsTend} assigned={assigned ?? "none"}");
            Log.Message(care.DebugCareReport());
        }

        [DebugAction("RimSynapse", "LW: TEST trader exchange — edible crop (#78)", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TestTraderEdible()
        {
            Map map = Find.CurrentMap;
            if (map == null) { Log.Message("[SYNAPSE-TEST] FAIL RT_TraderExchange_Edible | no current map"); return; }
            var care = map.GetComponent<MapComponent_ResidentCare>();
            if (care == null) { Log.Message("[SYNAPSE-TEST] FAIL RT_TraderExchange_Edible | no MapComponent_ResidentCare"); return; }

            ThingDef rawDef = DefDatabase<ThingDef>.GetNamed("RawPotatoes", false)
                              ?? DefDatabase<ThingDef>.GetNamed("RawRice", false);
            if (rawDef == null) { Log.Message("[SYNAPSE-TEST] FAIL RT_TraderExchange_Edible | no raw-food def"); return; }

            IntVec3 center = CellFinder.RandomClosewalkCellNear(map.Center, map, 15);

            // Seed 3 surplus stacks of raw food around the homestead.
            for (int i = 0; i < 3; i++)
            {
                Thing raw = ThingMaker.MakeThing(rawDef);
                raw.stackCount = 40;
                GenPlace.TryPlaceThing(raw, center, map, ThingPlaceMode.Near);
            }

            int rawBefore = CountNear(map, center, rawDef, 12f);
            int mealsBefore = CountNear(map, center, ThingDefOf.MealSurvivalPack, 12f);

            string summary = care.RunTraderExchange(center, rawDef);

            int rawAfter = CountNear(map, center, rawDef, 12f);
            int mealsAfter = CountNear(map, center, ThingDefOf.MealSurvivalPack, 12f);
            int mealsGained = mealsAfter - mealsBefore;

            bool leftSome = rawAfter > 0 && rawAfter < rawBefore;   // took surplus, left a stack
            bool gaveMeals = mealsGained >= ResidentCareSettings.traderSurvivalMeals;
            bool pass = leftSome && gaveMeals;

            Log.Message($"[SYNAPSE-TEST] {(pass ? "PASS" : "FAIL")} RT_TraderExchange_Edible | raw {rawBefore}->{rawAfter} " +
                        $"survivalMeals +{mealsGained} (want >={ResidentCareSettings.traderSurvivalMeals}) | {summary}");
        }

        [DebugAction("RimSynapse", "LW: TEST trader exchange — non-edible crop (#78)", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TestTraderNonEdible()
        {
            Map map = Find.CurrentMap;
            if (map == null) { Log.Message("[SYNAPSE-TEST] FAIL RT_TraderExchange_NonEdible | no current map"); return; }
            var care = map.GetComponent<MapComponent_ResidentCare>();
            if (care == null) { Log.Message("[SYNAPSE-TEST] FAIL RT_TraderExchange_NonEdible | no MapComponent_ResidentCare"); return; }

            ThingDef nonEdible = DefDatabase<ThingDef>.GetNamed("Cloth", false)
                                 ?? DefDatabase<ThingDef>.GetNamed("MedicineHerbal", false);
            if (nonEdible == null) { Log.Message("[SYNAPSE-TEST] FAIL RT_TraderExchange_NonEdible | no non-edible def"); return; }

            IntVec3 center = CellFinder.RandomClosewalkCellNear(map.Center, map, 15);
            int mealsBefore = CountNear(map, center, ThingDefOf.MealSurvivalPack, 12f);

            string summary = care.RunTraderExchange(center, nonEdible);

            int mealsAfter = CountNear(map, center, ThingDefOf.MealSurvivalPack, 12f);
            int mealsGained = mealsAfter - mealsBefore;

            bool pass = mealsGained >= ResidentCareSettings.traderSurvivalMealsNonEdible;
            Log.Message($"[SYNAPSE-TEST] {(pass ? "PASS" : "FAIL")} RT_TraderExchange_NonEdible | survivalMeals +{mealsGained} " +
                        $"(want >={ResidentCareSettings.traderSurvivalMealsNonEdible}) | {summary}");
        }

        // ---- small helpers -------------------------------------------------------

        private static int InventoryCount(Pawn pawn, ThingDef def)
        {
            if (pawn?.inventory?.innerContainer == null) return 0;
            return pawn.inventory.innerContainer.Where(t => t.def == def).Sum(t => t.stackCount);
        }

        private static int InventoryMedicineCount(Pawn pawn)
        {
            if (pawn?.inventory?.innerContainer == null) return 0;
            return pawn.inventory.innerContainer.Where(t => t.def.IsMedicine).Sum(t => t.stackCount);
        }

        private static int CountNear(Map map, IntVec3 center, ThingDef def, float radius)
        {
            int count = 0;
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(center, map, radius, true))
            {
                if (t != null && t.Spawned && t.def == def) count += t.stackCount;
            }
            return count;
        }
    }
}
