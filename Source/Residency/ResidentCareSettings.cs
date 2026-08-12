using Verse;

namespace RimSynapse.LivingWorld.Residency
{
    /// <summary>
    /// Tunables for the 0.8 resident self-sufficiency layer (dwelling occupants that farm, cook,
    /// self-tend, and trade their surplus so they stop starving in the first month).
    ///
    /// <para>All fields are plain statics with sensible defaults; persisted through
    /// <see cref="FactionPlacementSettings.ExposeData"/> alongside the other 0.7/0.8 switches. Every
    /// behaviour is a no-op when <see cref="selfSufficiencyEnabled"/> is off, and each individual
    /// piece degrades gracefully when the relevant content (a DLC, Combat Extended) is absent.</para>
    /// </summary>
    public static class ResidentCareSettings
    {
        /// <summary>Master switch for the whole layer. Off means residents behave as they did before 0.8.</summary>
        public static bool selfSufficiencyEnabled = true;

        // --- Starting supplies (per resident pawn) --------------------------------
        /// <summary>Medicine ("med packs") each resident spawns holding.</summary>
        public static int startingMedicine = 5;

        /// <summary>Simple meals each resident spawns holding.</summary>
        public static int startingMeals = 5;

        /// <summary>Upper market-value bound (silver) for the random self-defence weapon each resident carries.</summary>
        public static float weaponValueThreshold = 300f;

        // --- Caretaker AI ---------------------------------------------------------
        /// <summary>Ticks between caretaker evaluation passes. ~250 ≈ 4 in-game seconds.</summary>
        public static int careTickInterval = 250;

        /// <summary>Target stock of simple meals a resident cooks toward (cooks more when below this).</summary>
        public static int mealStockTarget = 5;

        // --- Harvest → passing-trader exchange ------------------------------------
        /// <summary>Survival meals a passing trader leaves after an edible-crop harvest.</summary>
        public static int traderSurvivalMeals = 10;

        /// <summary>Survival meals a passing trader leaves after a NON-edible-crop harvest (in lieu of taking raw food).</summary>
        public static int traderSurvivalMealsNonEdible = 20;

        /// <summary>Medicine a passing trader leaves after an edible-crop harvest.</summary>
        public static int traderMedicine = 1;

        /// <summary>Full stacks of raw food the trader leaves behind (0 for non-edible crops, which yield no food).</summary>
        public static int traderRawFoodStacksLeft = 1;

        /// <summary>Minimum ticks between trader visits to the same homestead, so continuous harvesting can't spam it. 60000 = one day.</summary>
        public static int traderCooldownTicks = 60000;

        /// <summary>Whether a passing-trader visit raises a small letter. Off keeps it silent.</summary>
        public static bool traderVisitLetter = true;

        public static bool Active => selfSufficiencyEnabled;

        public static void ExposeData()
        {
            Scribe_Values.Look(ref selfSufficiencyEnabled, "resident_selfSufficiencyEnabled", true);
            Scribe_Values.Look(ref startingMedicine, "resident_startingMedicine", 5);
            Scribe_Values.Look(ref startingMeals, "resident_startingMeals", 5);
            Scribe_Values.Look(ref weaponValueThreshold, "resident_weaponValueThreshold", 300f);
            Scribe_Values.Look(ref careTickInterval, "resident_careTickInterval", 250);
            Scribe_Values.Look(ref mealStockTarget, "resident_mealStockTarget", 5);
            Scribe_Values.Look(ref traderSurvivalMeals, "resident_traderSurvivalMeals", 10);
            Scribe_Values.Look(ref traderSurvivalMealsNonEdible, "resident_traderSurvivalMealsNonEdible", 20);
            Scribe_Values.Look(ref traderMedicine, "resident_traderMedicine", 1);
            Scribe_Values.Look(ref traderRawFoodStacksLeft, "resident_traderRawFoodStacksLeft", 1);
            Scribe_Values.Look(ref traderCooldownTicks, "resident_traderCooldownTicks", 60000);
            Scribe_Values.Look(ref traderVisitLetter, "resident_traderVisitLetter", true);
        }
    }
}
