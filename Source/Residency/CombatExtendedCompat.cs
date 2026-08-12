using System;
using System.Collections;
using System.Reflection;
using Verse;

namespace RimSynapse.LivingWorld.Residency
{
    /// <summary>
    /// Soft, reflection-only bridge to Combat Extended. This mod holds no reference to CE and must
    /// build and run whether or not CE is installed — so every CE type is resolved by name and every
    /// step is guarded. When CE is absent, <see cref="Active"/> is false and
    /// <see cref="TryGiveMatchingAmmo"/> is a no-op.
    ///
    /// <para>CE gates a gun behind a <c>CompAmmoUser</c>; without the right ammo in inventory the
    /// weapon can't fire. When we hand a resident a self-defence weapon we also hand it the ammo that
    /// weapon's <c>CompProperties_AmmoUser.ammoSet</c> declares, so the weapon is actually usable.</para>
    /// </summary>
    public static class CombatExtendedCompat
    {
        private const int AmmoStackCap = 200;

        private static bool resolved;
        private static Type compAmmoUserType;      // CombatExtended.CompAmmoUser
        private static Type compPropsAmmoUserType; // CombatExtended.CompProperties_AmmoUser
        private static FieldInfo ammoSetField;     // CompProperties_AmmoUser.ammoSet  -> AmmoSetDef
        private static FieldInfo ammoTypesField;   // AmmoSetDef.ammoTypes            -> IList of AmmoLink
        private static FieldInfo ammoLinkAmmoField;// AmmoLink.ammo                   -> AmmoDef : ThingDef

        /// <summary>True when Combat Extended is loaded and its ammo types were resolved.</summary>
        public static bool Active
        {
            get
            {
                Resolve();
                return compAmmoUserType != null && compPropsAmmoUserType != null
                    && ammoSetField != null && ammoTypesField != null && ammoLinkAmmoField != null;
            }
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;

            try
            {
                compAmmoUserType = GenTypes.GetTypeInAnyAssembly("CombatExtended.CompAmmoUser");
                compPropsAmmoUserType = GenTypes.GetTypeInAnyAssembly("CombatExtended.CompProperties_AmmoUser");
                if (compPropsAmmoUserType != null)
                {
                    ammoSetField = compPropsAmmoUserType.GetField("ammoSet", BindingFlags.Public | BindingFlags.Instance);
                }

                Type ammoSetDefType = GenTypes.GetTypeInAnyAssembly("CombatExtended.AmmoSetDef");
                if (ammoSetDefType != null)
                {
                    ammoTypesField = ammoSetDefType.GetField("ammoTypes", BindingFlags.Public | BindingFlags.Instance);
                }

                Type ammoLinkType = GenTypes.GetTypeInAnyAssembly("CombatExtended.AmmoLink");
                if (ammoLinkType != null)
                {
                    ammoLinkAmmoField = ammoLinkType.GetField("ammo", BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch (Exception ex)
            {
                Log.WarningOnce($"[RimSynapse-LivingWorld] Combat Extended detected but its ammo types could not be resolved: {ex.Message}", 0x4345_0001);
            }
        }

        /// <summary>
        /// If CE is active and <paramref name="weapon"/> uses ammo, add a stack of its first declared
        /// ammo type to <paramref name="pawn"/>'s inventory. Returns the ammo def given, or null.
        /// </summary>
        public static ThingDef TryGiveMatchingAmmo(Pawn pawn, Thing weapon)
        {
            if (!Active || pawn?.inventory == null || weapon == null) return null;

            try
            {
                ThingComp ammoUser = FindAmmoUserComp(weapon);
                if (ammoUser == null) return null;

                // comp.props is CompProperties_AmmoUser; read its ammoSet, then the first ammo link.
                object props = ammoUser.props;
                if (props == null || !compPropsAmmoUserType.IsInstanceOfType(props)) return null;

                object ammoSet = ammoSetField.GetValue(props);
                if (ammoSet == null) return null;

                if (!(ammoTypesField.GetValue(ammoSet) is IList links) || links.Count == 0) return null;

                object firstLink = links[0];
                if (firstLink == null) return null;

                if (!(ammoLinkAmmoField.GetValue(firstLink) is ThingDef ammoDef)) return null;

                Thing ammo = ThingMaker.MakeThing(ammoDef);
                int limit = ammoDef.stackLimit > 0 ? ammoDef.stackLimit : AmmoStackCap;
                ammo.stackCount = Math.Min(limit, AmmoStackCap);
                pawn.inventory.innerContainer.TryAdd(ammo);
                return ammoDef;
            }
            catch (Exception ex)
            {
                Log.WarningOnce($"[RimSynapse-LivingWorld] Could not give CE ammo to {pawn?.LabelShortCap}: {ex.Message}", 0x4345_0002);
                return null;
            }
        }

        private static ThingComp FindAmmoUserComp(Thing weapon)
        {
            if (!(weapon is ThingWithComps twc) || twc.AllComps == null) return null;
            foreach (ThingComp c in twc.AllComps)
            {
                if (c != null && compAmmoUserType.IsInstanceOfType(c)) return c;
            }
            return null;
        }
    }
}
