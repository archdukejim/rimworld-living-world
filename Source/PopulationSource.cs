using System;
using System.Reflection;
using RimWorld.Planet;
using Verse;

namespace RimSynapse.LivingWorld
{
    /// <summary>
    /// Decides how many inhabitants a freshly generated map's homesteads should hold, and where the
    /// number comes from.
    ///
    /// <para><b>Soft dependency, not a hard one.</b> If Regions &amp; Territories is installed, Living
    /// World defers entirely to R&amp;T's per-tile population endpoint
    /// (<c>PopulationDensityUtility.GetSourcePopulationAtTile</c>), resolved by reflection with no
    /// assembly reference — so Living World needs neither R&amp;T nor Map Mode Framework to build or
    /// run. When that endpoint is absent, Living World falls back to seeding its own modest number of
    /// homesteads (0..<see cref="standaloneMaxSettlements"/>) on habitable tiles, deterministically
    /// from the world seed so a given world always populates the same way.</para>
    /// </summary>
    public static class PopulationSource
    {
        /// <summary>When R&amp;T is absent, still place homesteads from the world seed. Off = only R&amp;T-reported maps get inhabitants.</summary>
        public static bool standaloneSeedingEnabled = true;

        /// <summary>Upper bound on standalone (no-R&amp;T) homesteads per habitable map.</summary>
        public static int standaloneMaxSettlements = 4;

        private static bool resolved;
        private static Func<int, int> rtEndpoint;

        /// <summary>True when the Regions &amp; Territories population endpoint was found.</summary>
        public static bool RegionsAndTerritoriesActive
        {
            get { Resolve(); return rtEndpoint != null; }
        }

        /// <summary>Inhabitant count for the map's tile: R&amp;T's number when present, else the seeded fallback.</summary>
        public static int GetPopulationForTile(Map map)
        {
            if (map == null) return 0;
            int tileId = map.Tile.tileId;

            Resolve();
            if (rtEndpoint != null)
            {
                // R&T is authoritative — including a legitimate zero (no settlement on this tile).
                try { return rtEndpoint(tileId); }
                catch (Exception ex)
                {
                    Log.WarningOnce($"[RimSynapse-LivingWorld] R&T population endpoint threw: {ex.Message}", 0x4C57_0001);
                    return 0;
                }
            }

            return StandaloneCount(tileId);
        }

        /// <summary>Deterministic 0..max from the world seed and tile, only on habitable tiles.</summary>
        private static int StandaloneCount(int tileId)
        {
            if (!standaloneSeedingEnabled || standaloneMaxSettlements <= 0) return 0;
            if (!IsHabitable(tileId)) return 0;

            int seed = Gen.HashCombineInt(SeedHash(), tileId);
            Rand.PushState(seed);
            int count = Rand.RangeInclusive(0, standaloneMaxSettlements);
            Rand.PopState();
            return count;
        }

        private static int SeedHash()
        {
            string s = Find.World?.info?.seedString;
            return string.IsNullOrEmpty(s) ? 0 : GenText.StableStringHash(s);
        }

        private static bool IsHabitable(int tileId)
        {
            WorldGrid grid = Find.WorldGrid;
            if (grid == null) return false;
            Tile tile = grid[tileId];
            if (tile == null || tile.WaterCovered) return false;
            if (tile.hilliness == Hilliness.Impassable) return false;
            if (tile.PrimaryBiome != null && tile.PrimaryBiome.impassable) return false;
            return true;
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;

            try
            {
                Type t = GenTypes.GetTypeInAnyAssembly("RimSynapse.RegionsAndTerritories.PopulationDensityUtility");
                MethodInfo m = t?.GetMethod("GetSourcePopulationAtTile",
                    BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(int) }, null);
                if (m != null)
                {
                    rtEndpoint = (Func<int, int>)Delegate.CreateDelegate(typeof(Func<int, int>), m);
                    Log.Message("[RimSynapse-LivingWorld] Regions & Territories population endpoint found; deferring inhabitant counts to it.");
                }
                else
                {
                    Log.Message("[RimSynapse-LivingWorld] Regions & Territories not detected; using standalone world-seed homestead seeding.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimSynapse-LivingWorld] Could not resolve R&T population endpoint, using standalone seeding: {ex.Message}");
            }
        }

        public static void ExposeData()
        {
            Scribe_Values.Look(ref standaloneSeedingEnabled, "lw_standaloneSeedingEnabled", true);
            Scribe_Values.Look(ref standaloneMaxSettlements, "lw_standaloneMaxSettlements", 4);
        }
    }
}
