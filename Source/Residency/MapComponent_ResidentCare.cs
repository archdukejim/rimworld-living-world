using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimSynapse.LivingWorld.Residency
{
    /// <summary>
    /// Keeps dwelling residents alive. Non-player pawns don't run the colony work think-tree, so left
    /// to the vanilla AI these settlers never harvest, cook, or self-tend and starve within a month.
    /// This caretaker steps in on a throttled tick and hands each idle resident the single most useful
    /// job — self-tend, cook, harvest, or (in winter) forage — reusing the real vanilla
    /// <see cref="WorkGiver"/>s so the jobs are wired exactly as a colonist's would be, just dispatched
    /// past the faction gate that would otherwise skip them.
    ///
    /// <para>It also runs the harvest → passing-trader exchange (an abstract swap, no walking pawn):
    /// see <see cref="NotifyResidentHarvest"/> and <see cref="RunTraderExchange"/>.</para>
    ///
    /// <para>MapComponents are auto-instantiated on every map, so this needs no registration; it early-
    /// outs cheaply when the feature is off or the map holds no residents.</para>
    /// </summary>
    public class MapComponent_ResidentCare : MapComponent
    {
        private struct HarvestPing
        {
            public IntVec3 center;
            public ThingDef harvestedDef;
        }

        // Latest un-serviced harvest per homestead bucket, and when each bucket last saw a trader.
        private readonly Dictionary<int, HarvestPing> pendingByArea = new Dictionary<int, HarvestPing>();
        private readonly Dictionary<int, int> lastTraderTickByArea = new Dictionary<int, int>();

        // Cached def-bound WorkGiver instances (resolved once).
        private WorkGiver_GrowerHarvest harvestWorker;
        private WorkGiver_DoBill campfireCookWorker;
        private bool workersResolved;

        public MapComponent_ResidentCare(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (!ResidentCareSettings.Active) return;

            int interval = Mathf.Max(60, ResidentCareSettings.careTickInterval);
            if (Find.TickManager.TicksGame % interval != 0) return;

            CareTick();
            ServicePendingTraders();
        }

        // -------------------------------------------------------------------------------------
        // Caretaker AI
        // -------------------------------------------------------------------------------------

        private void CareTick()
        {
            List<Pawn> residents = ResidentsOnMap();
            for (int i = 0; i < residents.Count; i++)
            {
                Pawn p = residents[i];
                if (!IsAvailableForWork(p)) continue;
                TryAssignCare(p);
            }
        }

        private List<Pawn> ResidentsOnMap()
        {
            var list = new List<Pawn>();
            IReadOnlyList<Pawn> all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn p = all[i];
                if (p != null && p.RaceProps != null && p.RaceProps.Humanlike && ResidencyUtility.IsResident(p))
                {
                    list.Add(p);
                }
            }
            return list;
        }

        /// <summary>Only step in when the pawn is free — asleep, downed, drafted, in combat, or already busy is left alone.</summary>
        private static bool IsAvailableForWork(Pawn p)
        {
            if (p == null || !p.Spawned || p.Dead || p.Downed) return false;
            if (p.InMentalState || p.Drafted) return false;
            if (p.CurJob == null) return true;
            // A sleeping pawn's job is LayDown, which is not in the whitelist below — so it's left alone.

            JobDef d = p.CurJobDef;
            return d == JobDefOf.Wait || d == JobDefOf.Wait_Wander || d == JobDefOf.GotoWander
                || d == JobDefOf.Goto || d == JobDefOf.Wait_MaintainPosture;
        }

        /// <summary>Assign the highest-priority survival job; returns the job def name assigned (or null).</summary>
        public string TryAssignCare(Pawn p)
        {
            Job job = SelfTendJob(p) ?? CookJob(p) ?? HarvestJob(p) ?? ForageJob(p);
            if (job == null) return null;

            p.jobs.StartJob(job, JobCondition.InterruptForced);
            return job.def?.defName;
        }

        private Job SelfTendJob(Pawn p)
        {
            if (p.health == null || !p.health.HasHediffsNeedingTend()) return null;
            if (p.health.capacities != null && !p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return null;
            if (!WorkGiver_Tend.GoodLayingStatusForTend(p, p)) return null;

            // Tend self using medicine carried in inventory if any (else a bare tend that still stops bleeding).
            Thing medicine = FindHeldMedicine(p);
            Job job = JobMaker.MakeJob(JobDefOf.TendPatient, p, medicine);
            job.count = 1;
            return job;
        }

        private static Thing FindHeldMedicine(Pawn p)
        {
            if (p.inventory?.innerContainer == null) return null;
            foreach (Thing t in p.inventory.innerContainer)
            {
                if (t?.def != null && t.def.IsMedicine) return t;
            }
            return null;
        }

        private Job CookJob(Pawn p)
        {
            if (LocalSimpleMealCount(p) >= ResidentCareSettings.mealStockTarget) return null;

            Building campfire = NearestUsableCampfire(p);
            if (campfire == null) return null;

            EnsureCookBill(campfire);
            ResolveWorkers();
            if (campfireCookWorker == null) return null;

            try { return campfireCookWorker.JobOnThing(p, campfire, false); }
            catch { return null; }
        }

        private int LocalSimpleMealCount(Pawn p)
        {
            int count = 0;
            if (p.inventory?.innerContainer != null)
            {
                foreach (Thing t in p.inventory.innerContainer)
                {
                    if (t?.def == ThingDefOf.MealSimple) count += t.stackCount;
                }
            }
            // Meals lying around the homestead (NPCs don't haul to stockpiles).
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(p.Position, map, 12f, true))
            {
                if (t?.def == ThingDefOf.MealSimple && t.Spawned) count += t.stackCount;
            }
            return count;
        }

        private Building NearestUsableCampfire(Pawn p)
        {
            List<Thing> fires = map.listerThings.ThingsOfDef(ThingDefOf.Campfire);
            Building best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < fires.Count; i++)
            {
                if (!(fires[i] is Building b) || !(b is IBillGiver)) continue;
                if (b.IsForbidden(p) || !p.CanReserveAndReach(b, PathEndMode.InteractionCell, Danger.Deadly)) continue;
                float d = p.Position.DistanceToSquared(b.Position);
                if (d < bestDist) { bestDist = d; best = b; }
            }
            return best;
        }

        private void EnsureCookBill(Building campfire)
        {
            if (!(campfire is IBillGiver giver)) return;
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamed("CookMealSimple", false);
            if (recipe == null) return;

            foreach (Bill b in giver.BillStack.Bills)
            {
                if (b?.recipe == recipe) return; // already present
            }

            var bill = (Bill_Production)recipe.MakeNewBill();
            // Forever + a caretaker-side stock gate: we only issue the cook job when the homestead is short.
            bill.repeatMode = BillRepeatModeDefOf.Forever;
            bill.ingredientSearchRadius = 30f;
            giver.BillStack.AddBill(bill);
        }

        private Job HarvestJob(Pawn p)
        {
            ResolveWorkers();
            if (harvestWorker == null) return null;

            IntVec3 cell = FindHarvestableZoneCell(p);
            if (cell == IntVec3.Invalid) return null;

            try
            {
                if (!harvestWorker.HasJobOnCell(p, cell, false)) return null;
                return harvestWorker.JobOnCell(p, cell, false);
            }
            catch { return null; }
        }

        private IntVec3 FindHarvestableZoneCell(Pawn p)
        {
            IntVec3 best = IntVec3.Invalid;
            float bestDist = float.MaxValue;
            List<Zone> zones = map.zoneManager.AllZones;
            for (int i = 0; i < zones.Count; i++)
            {
                if (!(zones[i] is Zone_Growing zg)) continue;
                List<IntVec3> cells = zg.Cells;
                for (int c = 0; c < cells.Count; c++)
                {
                    IntVec3 cell = cells[c];
                    Plant plant = cell.GetPlant(map);
                    if (plant == null || !plant.HarvestableNow) continue;
                    float d = p.Position.DistanceToSquared(cell);
                    if (d < bestDist) { bestDist = d; best = cell; }
                    break; // one harvestable cell per zone is enough to seed the job
                }
            }
            return best;
        }

        private Job ForageJob(Pawn p)
        {
            // Only when crops can't sustain them: deep in the cold season and genuinely hungry.
            if (GenLocalDate.Season(map) != Season.Winter && GenLocalDate.Season(map) != Season.PermanentWinter) return null;
            if (p.needs?.food == null || p.needs.food.CurLevelPercentage > 0.4f) return null;

            Plant target = null;
            float bestDist = float.MaxValue;
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(p.Position, map, 40f, true))
            {
                if (!(t is Plant plant) || plant.def?.plant == null) continue;
                if (!plant.HarvestableNow) continue;
                ThingDef yield = plant.def.plant.harvestedThingDef;
                if (yield == null || !yield.IsNutritionGivingIngestible) continue;
                if (!p.CanReserveAndReach(plant, PathEndMode.Touch, Danger.Deadly)) continue;
                float d = p.Position.DistanceToSquared(plant.Position);
                if (d < bestDist) { bestDist = d; target = plant; }
            }
            if (target == null) return null;

            Job job = JobMaker.MakeJob(JobDefOf.Harvest, target);
            job.ignoreDesignations = true; // wild plants carry no player harvest designation
            return job;
        }

        private void ResolveWorkers()
        {
            if (workersResolved) return;
            workersResolved = true;

            var harvestDef = DefDatabase<WorkGiverDef>.GetNamed("GrowerHarvest", false);
            harvestWorker = harvestDef?.Worker as WorkGiver_GrowerHarvest;

            var cookDef = DefDatabase<WorkGiverDef>.GetNamed("DoBillsCookCampfire", false);
            campfireCookWorker = cookDef?.Worker as WorkGiver_DoBill;
        }

        // -------------------------------------------------------------------------------------
        // Harvest → passing-trader exchange
        // -------------------------------------------------------------------------------------

        /// <summary>Called from the <see cref="Patches.Patch_Plant_PlantCollected"/> postfix when a resident harvests.</summary>
        public void NotifyResidentHarvest(Pawn by, Plant plant)
        {
            if (!ResidentCareSettings.Active || by == null || plant?.def?.plant == null) return;

            int key = AreaKey(by.Position);
            pendingByArea[key] = new HarvestPing
            {
                center = by.Position,
                harvestedDef = plant.def.plant.harvestedThingDef
            };
        }

        private void ServicePendingTraders()
        {
            if (pendingByArea.Count == 0) return;

            int now = Find.TickManager.TicksGame;
            int cooldown = Mathf.Max(2500, ResidentCareSettings.traderCooldownTicks);
            List<int> serviced = null;

            foreach (KeyValuePair<int, HarvestPing> kv in pendingByArea)
            {
                lastTraderTickByArea.TryGetValue(kv.Key, out int last);
                if (last != 0 && now - last < cooldown) continue;

                RunTraderExchange(kv.Value.center, kv.Value.harvestedDef);
                lastTraderTickByArea[kv.Key] = now;
                (serviced ?? (serviced = new List<int>())).Add(kv.Key);
            }

            if (serviced != null)
            {
                foreach (int k in serviced) pendingByArea.Remove(k);
            }
        }

        /// <summary>
        /// The abstract passing-trader swap around <paramref name="center"/>. Edible harvest: take the
        /// surplus raw food (leave a configured number of stacks), give survival meals + medicine.
        /// Non-edible harvest yields no food, so instead give the larger survival-meal count and leave
        /// nothing to collect. Returns a short human-readable summary for the debug path.
        /// </summary>
        public string RunTraderExchange(IntVec3 center, ThingDef harvestedDef)
        {
            bool edible = harvestedDef != null && harvestedDef.IsNutritionGivingIngestible;
            var sb = new StringBuilder();
            sb.Append(edible ? "edible" : "non-edible").Append(" crop; ");

            if (edible)
            {
                int removed = CollectSurplusRawFood(center, harvestedDef, ResidentCareSettings.traderRawFoodStacksLeft);
                int meals = GivePlace(ThingDefOf.MealSurvivalPack, ResidentCareSettings.traderSurvivalMeals, center);
                int meds = GivePlace(ResolveTraderMedicine(), ResidentCareSettings.traderMedicine, center);
                sb.Append($"collected {removed} surplus {harvestedDef.label}, left {ResidentCareSettings.traderRawFoodStacksLeft} stack(s); gave {meals} survival meals + {meds} medicine");
            }
            else
            {
                int meals = GivePlace(ThingDefOf.MealSurvivalPack, ResidentCareSettings.traderSurvivalMealsNonEdible, center);
                sb.Append($"gave {meals} survival meals (no raw food)");
            }

            if (ResidentCareSettings.traderVisitLetter && Find.LetterStack != null)
            {
                Find.LetterStack.ReceiveLetter(
                    "Passing trader",
                    "A trader passing through stopped at a nearby homestead, buying up the surplus harvest and leaving behind preserved food and supplies.",
                    LetterDefOf.PositiveEvent,
                    new TargetInfo(center, map));
            }

            return sb.ToString();
        }

        /// <summary>Remove matching raw-food stacks near center, keeping the largest <paramref name="stacksToLeave"/>. Returns items removed.</summary>
        private int CollectSurplusRawFood(IntVec3 center, ThingDef rawDef, int stacksToLeave)
        {
            var stacks = GenRadial.RadialDistinctThingsAround(center, map, 12f, true)
                .Where(t => t != null && t.Spawned && t.def == rawDef)
                .OrderByDescending(t => t.stackCount)
                .ToList();

            int removed = 0;
            for (int i = Mathf.Max(0, stacksToLeave); i < stacks.Count; i++)
            {
                removed += stacks[i].stackCount;
                stacks[i].Destroy();
            }
            return removed;
        }

        private int GivePlace(ThingDef def, int count, IntVec3 center)
        {
            if (def == null || count <= 0) return 0;
            int placed = 0;
            int remaining = count;
            while (remaining > 0)
            {
                Thing t = ThingMaker.MakeThing(def);
                int give = Mathf.Min(remaining, def.stackLimit > 0 ? def.stackLimit : remaining);
                t.stackCount = give;
                if (GenPlace.TryPlaceThing(t, center, map, ThingPlaceMode.Near))
                {
                    placed += give;
                }
                remaining -= give;
            }
            return placed;
        }

        private static ThingDef ResolveTraderMedicine()
        {
            return DefDatabase<ThingDef>.GetNamed("MedicineIndustrial", false)
                ?? DefDatabase<ThingDef>.GetNamed("MedicineHerbal", false)
                ?? DefDatabase<ThingDef>.GetNamed("MedicineUltratech", false);
        }

        private static int AreaKey(IntVec3 c)
        {
            // 16-cell buckets; homesteads are spaced >25 cells apart, so each falls in its own bucket.
            return (c.x >> 4) * 100000 + (c.z >> 4);
        }

        // -------------------------------------------------------------------------------------
        // Debug helpers (exercised headlessly via the DebugActions)
        // -------------------------------------------------------------------------------------

        public string DebugCareReport()
        {
            var sb = new StringBuilder();
            List<Pawn> residents = ResidentsOnMap();
            sb.AppendLine($"[R&T resident care] {residents.Count} resident(s) on map '{map}'.");
            foreach (Pawn p in residents)
            {
                if (!IsAvailableForWork(p))
                {
                    sb.AppendLine($"  {p.LabelShortCap}: busy/unavailable (job={p.CurJobDef?.defName ?? "none"})");
                    continue;
                }
                string assigned = TryAssignCare(p);
                sb.AppendLine($"  {p.LabelShortCap}: assigned {(assigned ?? "nothing (all needs met / no work)")}");
            }
            return sb.ToString();
        }

        public List<Pawn> DebugResidents() => ResidentsOnMap();
    }
}
