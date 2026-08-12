// Behaviour tests for residency (R&T #21) — the mechanic this mod took ownership of in 0.7.
//
// The load-bearing case is migration. Dwelling generation runs only at map generation, so a
// residency flag dropped on the way from Core's comp to this mod's can never be re-derived: those
// pawns would be permanently ordinary and their settlement would stop reacting to being looted.
// Most of what follows is about that one-way door.
using System;
using RimSynapse.LivingWorld.Residency;
using Verse;

namespace ResidencyTests
{
    public static class Program
    {
        private static int failures;

        public static int Main()
        {
            Section("the basics");
            Check("a null pawn is not a resident", ResidencyUtility.IsResident(null) == false);
            Check("setting residency on null does not throw", SafeSet(null, true));

            var plain = NewPawn();
            Check("a pawn with the comp starts non-resident", !ResidencyUtility.IsResident(plain));
            ResidencyUtility.SetResident(plain, true);
            Check("...and is a resident once marked", ResidencyUtility.IsResident(plain));
            ResidencyUtility.SetResident(plain, false);
            Check("...and can be unmarked", !ResidencyUtility.IsResident(plain));

            Section("a pawn without the comp is answered, not thrown at");
            // A humanlike def the injector never saw, or a non-humanlike being asked about.
            var compless = new Pawn();
            Check("reads as not a resident", ResidencyUtility.IsResident(compless) == false);
            Check("writing to it does not throw", SafeSet(compless, true));

            Section("migration: residency survives the move off Core's comp");
            var migrated = NewPawn();
            migrated.AllComps.Add(new RimSynapse.Comps.SynapseCorePawnComp { isResident = true });
            Check("a legacy resident is still a resident", ResidencyUtility.IsResident(migrated));

            var legacyNonResident = NewPawn();
            legacyNonResident.AllComps.Add(new RimSynapse.Comps.SynapseCorePawnComp { isResident = false });
            Check("a legacy non-resident stays non-resident", !ResidencyUtility.IsResident(legacyNonResident));

            Section("migration happens once and never overwrites a real answer");
            // Adoption must not fight a later write, or unmarking a legacy resident would silently
            // undo itself the next time anybody asked.
            var flipped = NewPawn();
            flipped.AllComps.Add(new RimSynapse.Comps.SynapseCorePawnComp { isResident = true });
            Check("adopted on first read", ResidencyUtility.IsResident(flipped));
            ResidencyUtility.SetResident(flipped, false);
            Check("an explicit unmark sticks", !ResidencyUtility.IsResident(flipped));
            Check("...and stays stuck on repeated reads", !ResidencyUtility.IsResident(flipped));

            // An explicit write before anybody reads must also win: the write marks the pawn as
            // already considered, so a stale legacy true cannot resurrect itself.
            var writtenFirst = NewPawn();
            writtenFirst.AllComps.Add(new RimSynapse.Comps.SynapseCorePawnComp { isResident = true });
            ResidencyUtility.SetResident(writtenFirst, false);
            Check("a write before the first read is not overwritten by legacy", !ResidencyUtility.IsResident(writtenFirst));

            Section("a pawn with no legacy comp is unaffected");
            var fresh = NewPawn();
            Check("reads false", !ResidencyUtility.IsResident(fresh));
            ResidencyUtility.SetResident(fresh, true);
            Check("and behaves normally after", ResidencyUtility.IsResident(fresh));

            Section("persistence round-trip");
            // Scribe_Values is a no-op in the stubs, so this pins the shape rather than the file
            // format: both fields are exposed, so neither residency nor the migration marker is
            // silently left out of the save.
            var comp = new ResidentPawnComp { isResident = true, legacyChecked = true };
            bool exposed = true;
            try { comp.PostExposeData(); } catch { exposed = false; }
            Check("PostExposeData runs without throwing", exposed);
            Check("residency survives it", comp.isResident);

            Section("registering the provider with Core");
            RimSynapse.SynapseCoreProviders.Residency = null;
            ResidencyUtility.RegisterWithCore();
            Check("Core's residency slot is filled", RimSynapse.SynapseCoreProviders.Residency != null);

            var published = NewPawn();
            ResidencyUtility.SetResident(published, true);
            Check("and it answers for a real resident",
                RimSynapse.SynapseCoreProviders.Residency(published));
            Check("...and denies a non-resident",
                !RimSynapse.SynapseCoreProviders.Residency(NewPawn()));
            Check("...and tolerates null", !RimSynapse.SynapseCoreProviders.Residency(null));

            Console.WriteLine();
            Console.WriteLine(failures == 0 ? "ALL RESIDENCY TESTS PASSED" : failures + " RESIDENCY TEST(S) FAILED");
            return failures == 0 ? 0 : 1;
        }

        /// <summary>A pawn as the injector leaves it: humanlike, comp attached, nothing set.</summary>
        private static Pawn NewPawn()
        {
            var p = new Pawn();
            p.AllComps.Add(new ResidentPawnComp());
            return p;
        }

        private static bool SafeSet(Pawn p, bool value)
        {
            try { ResidencyUtility.SetResident(p, value); return true; }
            catch { return false; }
        }

        private static void Section(string name)
        {
            Console.WriteLine();
            Console.WriteLine("-- " + name);
        }

        private static void Check(string label, bool ok)
        {
            if (!ok) failures++;
            Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + label);
        }
    }
}
