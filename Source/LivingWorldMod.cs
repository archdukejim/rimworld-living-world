using System;
using HarmonyLib;
using RimSynapse.LivingWorld.Residency;
using UnityEngine;
using Verse;

namespace RimSynapse.LivingWorld
{
    /// <summary>
    /// Living World brings inhabited maps to life: it seeds homesteads and dwellings with residents
    /// who farm, cook, self-tend, forage, and trade their surplus, so the settlers you find on a map
    /// plausibly survived long enough to have built it.
    ///
    /// <para>Standalone by design. When Regions &amp; Territories is present, Living World reads its
    /// per-tile population endpoint (by reflection, see <see cref="PopulationSource"/>) to decide how
    /// densely to place inhabitants; without it, it seeds a modest number of homesteads from the world
    /// seed. It holds no assembly reference to R&amp;T, Map Mode Framework, Core, or Combat Extended —
    /// every cross-mod link is reflection, so each stays optional in fact.</para>
    /// </summary>
    public class LivingWorldMod : Mod
    {
        public static LivingWorldSettings Settings;

        public LivingWorldMod(ModContentPack content) : base(content)
        {
            Log.Message("[RimSynapse-LivingWorld] Initializing Living World...");
            Settings = GetSettings<LivingWorldSettings>();

            var harmony = new Harmony("rimsynapse.livingworld");
            harmony.PatchAll();

            // Publish residency to RimSynapse Core (reflection; a no-op when Core is absent), deferred
            // so Core has registered its own provider surface first.
            LongEventHandler.ExecuteWhenFinished(ResidencyUtility.RegisterWithCore);
        }

        public override string SettingsCategory() => "RimSynapse Living World";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var l = new Listing_Standard();
            l.Begin(inRect);

            l.CheckboxLabeled("Enable resident self-sufficiency", ref ResidentCareSettings.selfSufficiencyEnabled,
                "Master switch. Off means dwelling residents are spawned but not equipped or kept alive by the caretaker.");

            l.GapLine();
            l.Label("Starting supplies (per resident)");
            ResidentCareSettings.startingMedicine = Mathf.RoundToInt(l.SliderLabeled(
                $"Medicine: {ResidentCareSettings.startingMedicine}", ResidentCareSettings.startingMedicine, 0f, 20f));
            ResidentCareSettings.startingMeals = Mathf.RoundToInt(l.SliderLabeled(
                $"Simple meals: {ResidentCareSettings.startingMeals}", ResidentCareSettings.startingMeals, 0f, 20f));
            ResidentCareSettings.weaponValueThreshold = l.SliderLabeled(
                $"Self-defence weapon value cap: {ResidentCareSettings.weaponValueThreshold:0} silver",
                ResidentCareSettings.weaponValueThreshold, 50f, 1000f);

            l.GapLine();
            l.Label("Homestead upkeep");
            ResidentCareSettings.mealStockTarget = Mathf.RoundToInt(l.SliderLabeled(
                $"Cook toward N meals in stock: {ResidentCareSettings.mealStockTarget}", ResidentCareSettings.mealStockTarget, 0f, 20f));

            l.GapLine();
            l.Label("Passing-trader exchange (on harvest)");
            ResidentCareSettings.traderSurvivalMeals = Mathf.RoundToInt(l.SliderLabeled(
                $"Survival meals for an edible harvest: {ResidentCareSettings.traderSurvivalMeals}", ResidentCareSettings.traderSurvivalMeals, 0f, 40f));
            ResidentCareSettings.traderSurvivalMealsNonEdible = Mathf.RoundToInt(l.SliderLabeled(
                $"Survival meals for a non-edible harvest: {ResidentCareSettings.traderSurvivalMealsNonEdible}", ResidentCareSettings.traderSurvivalMealsNonEdible, 0f, 40f));
            l.CheckboxLabeled("Announce trader visits with a letter", ref ResidentCareSettings.traderVisitLetter);

            l.GapLine();
            l.CheckboxLabeled("Seed homesteads even without Regions & Territories", ref PopulationSource.standaloneSeedingEnabled,
                "When R&T is not installed, place a modest 0-N homesteads on habitable tiles from the world seed. Off means Living World only populates maps whose population R&T reports.");
            PopulationSource.standaloneMaxSettlements = Mathf.RoundToInt(l.SliderLabeled(
                $"   Standalone homesteads per map (max): {PopulationSource.standaloneMaxSettlements}",
                PopulationSource.standaloneMaxSettlements, 0f, 8f));

            l.End();
        }
    }

    /// <summary>Persists Living World's tunables. Owns the mod's single <see cref="ModSettings"/> instance.</summary>
    public class LivingWorldSettings : ModSettings
    {
        public override void ExposeData()
        {
            base.ExposeData();
            ResidentCareSettings.ExposeData();
            PopulationSource.ExposeData();
        }
    }
}
