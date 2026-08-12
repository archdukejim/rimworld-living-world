using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimSynapse.LivingWorld.Residency
{
    /// <summary>
    /// Gives a freshly generated dwelling resident the supplies they need to survive their first
    /// month: medicine and meals to fall back on, and a modest self-defence weapon (plus Combat
    /// Extended ammo for it when CE is installed). Every step is guarded and a no-op when
    /// <see cref="ResidentCareSettings"/> is switched off.
    /// </summary>
    public static class ResidentEquipUtility
    {
        // The set of "ordinary" weapons that are neither AOE nor throwable, built once. Value-threshold
        // filtering happens at pick time because the threshold is a live setting.
        private static List<ThingDef> nonAoeWeaponsCache;

        public static void EquipStartingSupplies(Pawn pawn)
        {
            if (pawn == null || !ResidentCareSettings.Active) return;

            GiveInventory(pawn, ResolveMedicineDef(), ResidentCareSettings.startingMedicine);
            GiveInventory(pawn, ThingDefOf.MealSimple, ResidentCareSettings.startingMeals);
            GiveSelfDefenceWeapon(pawn);
            EnableSelfTend(pawn);
        }

        private static void GiveInventory(Pawn pawn, ThingDef def, int count)
        {
            if (pawn?.inventory == null || def == null || count <= 0) return;
            Thing thing = ThingMaker.MakeThing(def);
            thing.stackCount = count;
            pawn.inventory.innerContainer.TryAdd(thing);
        }

        /// <summary>Give the pawn exactly one non-AOE, under-threshold weapon (replacing any stock weapon), and CE ammo for it.</summary>
        public static ThingDef GiveSelfDefenceWeapon(Pawn pawn)
        {
            if (pawn?.equipment == null) return null;

            ThingDef weaponDef = PickWeaponDef();
            if (weaponDef == null) return null;

            ThingDef stuff = weaponDef.MadeFromStuff
                ? (GenStuff.RandomStuffByCommonalityFor(weaponDef) ?? GenStuff.DefaultStuffFor(weaponDef))
                : null;

            Thing weapon = ThingMaker.MakeThing(weaponDef, stuff);
            var quality = weapon.TryGetComp<CompQuality>();
            quality?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Outsider);
            if (weapon.def.useHitPoints) weapon.HitPoints = weapon.MaxHitPoints;

            // Exactly one weapon: drop whatever the pawn kind happened to spawn holding.
            pawn.equipment.DestroyAllEquipment();
            pawn.equipment.AddEquipment((ThingWithComps)weapon);

            // Combat Extended: a gun without ammo can't fire, so hand it the ammo it declares.
            CombatExtendedCompat.TryGiveMatchingAmmo(pawn, weapon);

            return weaponDef;
        }

        private static ThingDef PickWeaponDef()
        {
            float threshold = ResidentCareSettings.weaponValueThreshold;
            List<ThingDef> pool = NonAoeWeapons()
                .Where(d => d.BaseMarketValue > 0f && d.BaseMarketValue <= threshold)
                .ToList();

            if (pool.Count == 0)
            {
                // Threshold too tight — fall back to the cheapest non-AOE weapon so residents are never unarmed.
                return NonAoeWeapons().OrderBy(d => d.BaseMarketValue).FirstOrDefault();
            }
            return pool.RandomElement();
        }

        private static List<ThingDef> NonAoeWeapons()
        {
            if (nonAoeWeaponsCache != null) return nonAoeWeaponsCache;

            nonAoeWeaponsCache = DefDatabase<ThingDef>.AllDefs
                .Where(d => d.IsWeapon
                            && d.equipmentType == EquipmentType.Primary
                            && !d.destroyOnDrop
                            && d.BaseMarketValue > 0f
                            && !IsAoeOrThrowable(d))
                .ToList();
            return nonAoeWeaponsCache;
        }

        /// <summary>True when this weapon def is a legal resident self-defence weapon (not AOE/throwable). Public for the debug validator.</summary>
        public static bool IsAllowedWeapon(ThingDef d)
        {
            return d != null && d.IsWeapon && !IsAoeOrThrowable(d);
        }

        /// <summary>
        /// Excludes grenades/throwables, flamers, and anything that fires an explosive or fire
        /// projectile or is flagged a building-destroyer — inspected from the weapon's own verbs and
        /// tags so it holds for modded weapons too.
        /// </summary>
        private static bool IsAoeOrThrowable(ThingDef d)
        {
            if (d.weaponTags != null)
            {
                foreach (string tag in d.weaponTags)
                {
                    if (tag == null) continue;
                    if (tag.IndexOf("Grenade", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (tag.IndexOf("Flame", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (tag.IndexOf("Molotov", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }

            if (d.Verbs != null)
            {
                foreach (VerbProperties v in d.Verbs)
                {
                    if (v == null) continue;
                    if (v.ai_IsBuildingDestroyer) return true;

                    ProjectileProperties proj = v.defaultProjectile?.projectile;
                    if (proj == null) continue;
                    if (proj.explosionRadius > 0f) return true;
                    if (proj.damageDef == DamageDefOf.Flame || proj.damageDef == DamageDefOf.Bomb) return true;
                }
            }
            return false;
        }

        private static ThingDef ResolveMedicineDef()
        {
            return DefDatabase<ThingDef>.GetNamed("MedicineIndustrial", false)
                ?? DefDatabase<ThingDef>.GetNamed("MedicineHerbal", false)
                ?? DefDatabase<ThingDef>.GetNamed("MedicineUltratech", false);
        }

        private static void EnableSelfTend(Pawn pawn)
        {
            if (pawn?.playerSettings == null) return; // don't fabricate player settings on a neutral pawn
            pawn.playerSettings.selfTend = true;
            if (pawn.playerSettings.medCare == MedicalCareCategory.NoCare)
            {
                pawn.playerSettings.medCare = MedicalCareCategory.HerbalOrWorse;
            }
        }
    }
}
