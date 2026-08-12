using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimSynapse.LivingWorld
{
    public static class DwellingStructureGenerator
    {
        public static void Generate(Map map, int count)
        {
            if (map == null || count <= 0) return;

            // Fetch faction safely for all structures and residents
            Faction faction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.OutlanderCivil);
            if (faction == null)
            {
                faction = Find.FactionManager.AllFactions.FirstOrDefault(f => !f.HostileTo(Faction.OfPlayer) && !f.def.isPlayer);
            }
            if (faction == null)
            {
                faction = Find.FactionManager.OfAncients;
            }

            // Cap individual dwellings at 5; if population is larger, allocate the rest to Barracks (max 3 barracks)
            int targetDwellings = Math.Min(count, 5);
            int remainingPop = count - targetDwellings;
            int targetBarracks = 0;
            if (remainingPop > 0)
            {
                targetBarracks = Math.Min((remainingPop + 3) / 4, 3);
            }

            List<IntVec3> spawnedBaseLocs = new List<IntVec3>();

            // Fetch defs safely
            ThingDef wallDef = ThingDefOf.Wall;
            ThingDef doorDef = ThingDefOf.Door;
            ThingDef woodLog = ThingDefOf.WoodLog;
            ThingDef campfireDef = ThingDefOf.Campfire;
            ThingDef bedDef = ThingDefOf.Bed;
            ThingDef chairDef = DefDatabase<ThingDef>.GetNamed("DiningChair", false) ?? DefDatabase<ThingDef>.GetNamed("Stool", false) ?? ThingDefOf.Wall;
            
            ThingDef fenceDef = DefDatabase<ThingDef>.GetNamed("Fence", false) ?? ThingDefOf.Wall;
            ThingDef fenceGateDef = DefDatabase<ThingDef>.GetNamed("FenceGate", false) ?? ThingDefOf.Door;
            ThingDef penMarkerDef = DefDatabase<ThingDef>.GetNamed("PenMarker", false);

            TerrainDef woodFloor = DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor", false) ?? TerrainDefOf.Concrete;
            TerrainDef soilTerrain = TerrainDefOf.Soil;

            // 1. Generate Individual Dwellings (5x5 exterior, 3x3 interior)
            for (int i = 0; i < targetDwellings; i++)
            {
                IntVec3 startSpot = FindValidSpot(map, spawnedBaseLocs, 25);
                if (startSpot == IntVec3.Invalid) continue;

                IntVec3 baseLoc = startSpot - new IntVec3(8, 0, 8);
                spawnedBaseLocs.Add(baseLoc);

                // Wall footprint is 5x5
                for (int x = baseLoc.x; x <= baseLoc.x + 4; x++)
                {
                    for (int z = baseLoc.z; z <= baseLoc.z + 4; z++)
                    {
                        IntVec3 c = new IntVec3(x, 0, z);
                        if (!c.InBounds(map)) continue;

                        ClearBlockers(c, map);

                        bool isBorderX = (x == baseLoc.x || x == baseLoc.x + 4);
                        bool isBorderZ = (z == baseLoc.z || z == baseLoc.z + 4);

                        if (isBorderX || isBorderZ)
                        {
                            // Door in center of the top wall
                            if (x == baseLoc.x + 2 && z == baseLoc.z + 4)
                            {
                                SpawnThing(doorDef, woodLog, c, map, faction);
                            }
                            else
                            {
                                SpawnThing(wallDef, woodLog, c, map, faction);
                            }
                        }
                        else
                        {
                            map.terrainGrid.SetTerrain(c, woodFloor);
                        }

                        map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
                    }
                }

                // Spawn wooden bed inside (facing north)
                IntVec3 bedLoc = baseLoc + new IntVec3(1, 0, 1);
                ClearBlockers(bedLoc, map);
                Thing bedThing = ThingMaker.MakeThing(bedDef, woodLog);
                if (faction != null)
                {
                    bedThing.SetFaction(faction);
                }
                SetQualityAndCondition(bedThing);
                GenSpawn.Spawn(bedThing, bedLoc, map, Rot4.North);
                Building_Bed bed = bedThing as Building_Bed;

                // Spawn a chair inside
                IntVec3 chairLoc = baseLoc + new IntVec3(3, 0, 1);
                ClearBlockers(chairLoc, map);
                Thing chairThing = ThingMaker.MakeThing(chairDef, woodLog);
                if (faction != null)
                {
                    chairThing.SetFaction(faction);
                }
                SetQualityAndCondition(chairThing);
                GenSpawn.Spawn(chairThing, chairLoc, map, Rot4.North);

                // Campfire outside (near the door)
                IntVec3 campfireLoc = new IntVec3(baseLoc.x + 2, 0, baseLoc.z + 6);
                ClearBlockers(campfireLoc, map);
                SpawnThing(campfireDef, null, campfireLoc, map, faction);

                System.Random random = new System.Random(baseLoc.x ^ baseLoc.z);
                
                // Crop field (people always have a growing field, no pasture here)
                Zone_Growing zone = new Zone_Growing(map.zoneManager);
                map.zoneManager.RegisterZone(zone);

                ThingDef selectedCrop;
                if (Rand.Value < 0.80f)
                {
                    // 80% chance: Edible crop (Potato, Corn, Rice)
                    var edibleCrops = new List<ThingDef> { ThingDefOf.Plant_Potato };
                    var corn = DefDatabase<ThingDef>.GetNamed("Plant_Corn", false);
                    if (corn != null) edibleCrops.Add(corn);
                    var rice = DefDatabase<ThingDef>.GetNamed("Plant_Rice", false);
                    if (rice != null) edibleCrops.Add(rice);
                    
                    selectedCrop = edibleCrops[random.Next(edibleCrops.Count)];
                }
                else
                {
                    // 20% chance: Non-edible crop (Cotton, Healroot)
                    var nonEdibleCrops = new List<ThingDef>();
                    var cotton = DefDatabase<ThingDef>.GetNamed("Plant_Cotton", false);
                    if (cotton != null) nonEdibleCrops.Add(cotton);
                    var healroot = DefDatabase<ThingDef>.GetNamed("Plant_Healroot", false);
                    if (healroot != null) nonEdibleCrops.Add(healroot);
                    
                    if (nonEdibleCrops.Count > 0)
                        selectedCrop = nonEdibleCrops[random.Next(nonEdibleCrops.Count)];
                    else
                        selectedCrop = ThingDefOf.Plant_Potato;
                }

                for (int x = baseLoc.x + 6; x <= baseLoc.x + 13; x++)
                {
                    for (int z = baseLoc.z; z <= baseLoc.z + 7; z++)
                    {
                        IntVec3 c = new IntVec3(x, 0, z);
                        if (!c.InBounds(map)) continue;

                        ClearBlockers(c, map);
                        map.terrainGrid.SetTerrain(c, soilTerrain);
                        zone.AddCell(c);

                        Plant plant = (Plant)ThingMaker.MakeThing(selectedCrop);
                        plant.Growth = Rand.Range(0.15f, 0.85f);
                        GenSpawn.Spawn(plant, c, map);
                    }
                }
                zone.SetPlantDefToGrow(selectedCrop);

                // Spawn resident pawn
                if (Rand.Value < 0.66f)
                {
                    SpawnResident(map, baseLoc + new IntVec3(2, 0, 2), bed);
                }
            }

            // 2. Generate Barracks (5x9 exterior, 3x7 interior) - Spacing is 50x50
            for (int i = 0; i < targetBarracks; i++)
            {
                IntVec3 startSpot = FindValidSpot(map, spawnedBaseLocs, 50);
                if (startSpot == IntVec3.Invalid) continue;

                // Spacing base is 50x50; offset barracks inside it to make room for the 9x9 pasture
                IntVec3 baseLoc = startSpot - new IntVec3(16, 0, 16);
                spawnedBaseLocs.Add(baseLoc);

                // Barracks building is placed at offset (5, 5) inside the 50x50 clearance zone
                IntVec3 buildBase = baseLoc + new IntVec3(5, 0, 5);

                // Footprint is 5x9 (width 9 on X, height 5 on Z)
                for (int x = buildBase.x; x <= buildBase.x + 8; x++)
                {
                    for (int z = buildBase.z; z <= buildBase.z + 4; z++)
                    {
                        IntVec3 c = new IntVec3(x, 0, z);
                        if (!c.InBounds(map)) continue;

                        ClearBlockers(c, map);

                        bool isBorderX = (x == buildBase.x || x == buildBase.x + 8);
                        bool isBorderZ = (z == buildBase.z || z == buildBase.z + 4);

                        if (isBorderX || isBorderZ)
                        {
                            // Door in the center of the 9-length wall (at bottom, z = buildBase.z)
                            if (x == buildBase.x + 4 && z == buildBase.z)
                            {
                                SpawnThing(doorDef, woodLog, c, map, faction);
                            }
                            else
                            {
                                SpawnThing(wallDef, woodLog, c, map, faction);
                            }
                        }
                        else
                        {
                            map.terrainGrid.SetTerrain(c, woodFloor);
                        }

                        map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
                    }
                }

                // Spawn 4 beds lengthwise towards the door (North rotation, headboard against far top wall)
                // Bed X spots: buildBase.x + 1, buildBase.x + 3, buildBase.x + 5, buildBase.x + 7
                List<Building_Bed> barracksBeds = new List<Building_Bed>();
                for (int bedIndex = 0; bedIndex < 4; bedIndex++)
                {
                    int bx = buildBase.x + 1 + (bedIndex * 2);
                    IntVec3 bLoc = new IntVec3(bx, 0, buildBase.z + 2);
                    ClearBlockers(bLoc, map);
                    ClearBlockers(bLoc + new IntVec3(0, 0, 1), map);

                    Thing bedThing = ThingMaker.MakeThing(bedDef, woodLog);
                    if (faction != null)
                    {
                        bedThing.SetFaction(faction);
                    }
                    SetQualityAndCondition(bedThing);
                    GenSpawn.Spawn(bedThing, bLoc, map, Rot4.North);
                    
                    if (bedThing is Building_Bed bed)
                    {
                        barracksBeds.Add(bed);
                    }
                }

                // Campfire outside (near the door)
                IntVec3 campfireLoc = new IntVec3(buildBase.x + 4, 0, buildBase.z - 2);
                ClearBlockers(campfireLoc, map);
                SpawnThing(campfireDef, null, campfireLoc, map, faction);

                // Generate a 9x9 pasture pen at offset (20, 20) inside the 50x50 clearance zone
                IntVec3 pBase = baseLoc + new IntVec3(20, 0, 20);
                for (int x = pBase.x; x <= pBase.x + 8; x++)
                {
                    for (int z = pBase.z; z <= pBase.z + 8; z++)
                    {
                        IntVec3 c = new IntVec3(x, 0, z);
                        if (!c.InBounds(map)) continue;

                        ClearBlockers(c, map);

                        bool isBorderX = (x == pBase.x || x == pBase.x + 8);
                        bool isBorderZ = (z == pBase.z || z == pBase.z + 8);

                        if (isBorderX || isBorderZ)
                        {
                            if (x == pBase.x + 4 && z == pBase.z)
                            {
                                SpawnThing(fenceGateDef, woodLog, c, map, faction);
                            }
                            else
                            {
                                SpawnThing(fenceDef, woodLog, c, map, faction);
                            }
                        }
                        else
                        {
                            // Spawn biome-specific grazing plants
                            if (Rand.Value < 0.7f)
                            {
                                ThingDef plantDef = FindGrazingPlant(map);
                                Plant plant = (Plant)ThingMaker.MakeThing(plantDef);
                                plant.Growth = Rand.Range(0.4f, 0.9f);
                                GenSpawn.Spawn(plant, c, map);
                            }
                        }
                    }
                }

                if (penMarkerDef != null)
                {
                    IntVec3 markerLoc = pBase + new IntVec3(4, 0, 4);
                    ClearBlockers(markerLoc, map);
                    SpawnThing(penMarkerDef, woodLog, markerLoc, map, faction);
                }

                // Spawn a male and a female grazing animal of a biome-suitable kind inside the pasture
                PawnKindDef animalKind = FindGrazingAnimalKind(map);
                if (animalKind != null)
                {
                    IntVec3 animalLoc1 = pBase + new IntVec3(2, 0, 2);
                    IntVec3 animalLoc2 = pBase + new IntVec3(6, 0, 6);
                    ClearBlockers(animalLoc1, map);
                    ClearBlockers(animalLoc2, map);

                    PawnGenerationRequest maleRequest = new PawnGenerationRequest(
                        animalKind,
                        faction: faction,
                        fixedGender: Gender.Male,
                        context: PawnGenerationContext.NonPlayer
                    );
                    Pawn maleAnimal = PawnGenerator.GeneratePawn(maleRequest);
                    GenSpawn.Spawn(maleAnimal, animalLoc1, map);

                    PawnGenerationRequest femaleRequest = new PawnGenerationRequest(
                        animalKind,
                        faction: faction,
                        fixedGender: Gender.Female,
                        context: PawnGenerationContext.NonPlayer
                    );
                    Pawn femaleAnimal = PawnGenerator.GeneratePawn(femaleRequest);
                    GenSpawn.Spawn(femaleAnimal, animalLoc2, map);
                }

                // Spawn residents for barracks (each gets assigned to one of the 4 beds)
                for (int bIndex = 0; bIndex < barracksBeds.Count; bIndex++)
                {
                    if (Rand.Value < 0.75f)
                    {
                        IntVec3 pLoc = new IntVec3(buildBase.x + 1 + (bIndex * 2), 0, buildBase.z + 1);
                        SpawnResident(map, pLoc, barracksBeds[bIndex]);
                    }
                }
            }
        }

        private static void SetQualityAndCondition(Thing thing)
        {
            if (thing == null) return;

            // Maximum quality is normal
            var compQuality = thing.TryGetComp<CompQuality>();
            if (compQuality != null)
            {
                QualityCategory qc = (QualityCategory)Rand.RangeInclusive(0, (int)QualityCategory.Normal);
                compQuality.SetQuality(qc, ArtGenerationContext.Outsider);
            }

            // Good condition (100% hitpoints)
            if (thing.def.useHitPoints)
            {
                thing.HitPoints = thing.MaxHitPoints;
            }
        }

        /// <summary>
        /// Test seam (#29): spawn exactly one dwelling resident at a caller-chosen cell, bypassing
        /// the random placement search and the per-dwelling spawn-chance that made the
        /// Regions_DwellingOccupantsAreResidents integration case flaky (~4% of runs spawned nobody,
        /// and constrained maps could place no dwellings at all). It runs the same SetResident write
        /// path Generate uses, so the case still proves occupants are marked resident and that Core's
        /// provider agrees — just deterministically.
        /// </summary>
        public static Pawn SpawnResidentForTesting(Map map, IntVec3 cell)
        {
            return SpawnResident(map, cell, null);
        }

        private static Pawn SpawnResident(Map map, IntVec3 spawnLoc, Building_Bed bed)
        {
            if (!spawnLoc.InBounds(map)) return null;

            Faction faction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.OutlanderCivil);
            if (faction == null)
            {
                faction = Find.FactionManager.AllFactions.FirstOrDefault(f => !f.HostileTo(Faction.OfPlayer) && !f.def.isPlayer);
            }
            if (faction == null)
            {
                faction = Find.FactionManager.OfAncients;
            }

            PawnKindDef kind = PawnKindDefOf.Colonist;
            if (faction.def.defName.Contains("Tribal"))
            {
                kind = DefDatabase<PawnKindDef>.GetNamed("Tribal_Warrior", false) ?? PawnKindDefOf.Colonist;
            }

            PawnGenerationRequest request = new PawnGenerationRequest(
                kind,
                faction: faction,
                context: PawnGenerationContext.NonPlayer
            );

            // Attempt to generate a pawn capable of growing and cooking
            Pawn pawn = null;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                pawn = PawnGenerator.GeneratePawn(request);
                if (pawn != null && pawn.story != null &&
                    !pawn.WorkTypeIsDisabled(DefDatabase<WorkTypeDef>.GetNamed("Growing")) &&
                    !pawn.WorkTypeIsDisabled(DefDatabase<WorkTypeDef>.GetNamed("Cooking")))
                {
                    break;
                }
            }
            if (pawn == null)
            {
                pawn = PawnGenerator.GeneratePawn(request);
            }

            // Customize skills and passions
            if (pawn.skills != null)
            {
                var plantsSkill = pawn.skills.GetSkill(SkillDefOf.Plants);
                if (plantsSkill != null)
                {
                    plantsSkill.Level = Math.Max(plantsSkill.Level, Rand.RangeInclusive(6, 12));
                    plantsSkill.passion = Passion.Major;
                }

                var cookingSkill = pawn.skills.GetSkill(SkillDefOf.Cooking);
                if (cookingSkill != null)
                {
                    cookingSkill.Level = Math.Max(cookingSkill.Level, Rand.RangeInclusive(6, 12));
                    cookingSkill.passion = Passion.Major;
                }

                var animalsSkill = pawn.skills.GetSkill(SkillDefOf.Animals);
                if (animalsSkill != null)
                {
                    animalsSkill.Level = Math.Max(animalsSkill.Level, Rand.RangeInclusive(6, 12));
                    animalsSkill.passion = Passion.Major;
                }
            }

            // Starting supplies: medicine, meals, and a modest non-AOE self-defence weapon (plus CE
            // ammo when CE is installed). Self-sufficiency (0.8, #78) — without these, and without the
            // caretaker AI below, these occupants starved inside the first month.
            Residency.ResidentEquipUtility.EquipStartingSupplies(pawn);

            // This pawn lives here. Residency is this mod's own state as of 0.7 — it used to be a
            // flag on Core's pawn comp, which meant generating a dwelling required Core to be
            // installed.
            Residency.ResidencyUtility.SetResident(pawn, true);

            GenSpawn.Spawn(pawn, spawnLoc, map);

            if (bed != null)
            {
                pawn.ownership.ClaimBedIfNonMedical(bed);
            }

            LordMaker.MakeNewLord(
                faction,
                new LordJob_DefendPoint(spawnLoc),
                map,
                new List<Pawn> { pawn }
            );

            return pawn;
        }

        private static PawnKindDef FindGrazingAnimalKind(Map map)
        {
            try
            {
                if (map?.Biome?.AllWildAnimals != null)
                {
                    var eligible = map.Biome.AllWildAnimals
                        .Where(k => k.RaceProps != null && 
                                    !k.RaceProps.ToolUser && 
                                    k.RaceProps.Eats(FoodTypeFlags.Plant) && 
                                    !k.RaceProps.predator)
                        .ToList();
                    
                    if (eligible.Count > 0)
                    {
                        return eligible[Rand.Range(0, eligible.Count)];
                    }
                }
            }
            catch (Exception)
            {
                // Fallback
            }

            return DefDatabase<PawnKindDef>.GetNamed("Muffalo", false) 
                ?? DefDatabase<PawnKindDef>.GetNamed("Sheep", false) 
                ?? DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(k => k.RaceProps != null && k.RaceProps.Eats(FoodTypeFlags.Plant) && !k.RaceProps.predator);
        }

        private static ThingDef FindGrazingPlant(Map map)
        {
            try
            {
                if (map?.Biome?.wildPlants != null)
                {
                    var eligible = map.Biome.wildPlants
                        .Where(p => p.plant != null && 
                                    p.plant.plant != null && 
                                    !p.plant.plant.IsTree && 
                                    (p.plant.defName.Contains("Grass") || p.plant.defName.Contains("Dandelion")))
                        .Select(p => p.plant)
                        .ToList();
                    
                    if (eligible.Count > 0)
                    {
                        return eligible[Rand.Range(0, eligible.Count)];
                    }
                }
            }
            catch (Exception)
            {
                // Fallback
            }

            return ThingDefOf.Plant_Grass;
        }

        private static IntVec3 FindValidSpot(Map map, List<IntVec3> spawnedBaseLocs, int size)
        {
            int offset = size / 3;
            for (int i = 0; i < 3000; i++)
            {
                IntVec3 cell = CellFinder.RandomCell(map);
                if (cell.Walkable(map) && 
                    !cell.Fogged(map) && 
                    cell.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy) && 
                    cell.x > size && cell.x < map.Size.x - size && cell.z > size && cell.z < map.Size.z - size)
                {
                    IntVec3 baseLoc = cell - new IntVec3(offset, 0, offset);
                    
                    bool tooClose = false;
                    foreach (var other in spawnedBaseLocs)
                    {
                        if (baseLoc.DistanceToSquared(other) < size * size)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;

                    bool clear = true;
                    for (int x = -2; x <= size - 3; x++)
                    {
                        for (int z = -2; z <= size - 3; z++)
                        {
                            IntVec3 c = baseLoc + new IntVec3(x, 0, z);
                            if (!c.InBounds(map))
                            {
                                clear = false;
                                break;
                            }
                            var terrain = c.GetTerrain(map);
                            if (terrain.IsWater || c.GetEdifice(map) != null)
                            {
                                clear = false;
                                break;
                            }
                        }
                        if (!clear) break;
                    }
                    if (clear) return cell;
                }
            }

            return IntVec3.Invalid;
        }

        private static void ClearBlockers(IntVec3 cell, Map map)
        {
            var blockers = cell.GetThingList(map).ToList();
            foreach (var b in blockers)
            {
                if (b.def.destroyable && b.def.category != ThingCategory.Pawn)
                {
                    b.Destroy();
                }
            }
        }

        private static void SpawnThing(ThingDef def, ThingDef stuff, IntVec3 cell, Map map, Faction faction = null)
        {
            if (def == null) return;
            Thing thing = ThingMaker.MakeThing(def, stuff);
            if (faction != null)
            {
                thing.SetFaction(faction);
            }
            GenSpawn.Spawn(thing, cell, map);
        }
    }
}
