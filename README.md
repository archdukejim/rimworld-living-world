# RimSynapse — Living World

Brings inhabited maps to life. Where other mods count how many people a place *should*
hold, Living World is what actually puts them there: homesteads and dwellings with
residents who farm, cook, self-tend, forage in winter, and trade their surplus to passing
traders — so the settlers you find on a map plausibly survived long enough to have built it.

## Standalone by design

Living World holds **no assembly reference** to any other mod and requires **neither
Regions & Territories nor Map Mode Framework**.

- **With [Regions & Territories](https://github.com/RimSynapse/Regions-and-Territories)**:
  Living World reads R&T's per-tile population endpoint (`PopulationDensityUtility`) by
  reflection and defers to it for where and how densely to place inhabitants.
- **Without it**: Living World seeds its own modest 0–N homesteads on habitable tiles,
  deterministically from the world seed.
- **With RimSynapse Core**: residency is published to Core (reflection) so Conversations
  and Psychology can tell a resident from a passer-by.
- **With Combat Extended**: residents are given the correct ammunition for their weapon.

Each of these is optional; every cross-mod link is reflection, so nothing is required and
nothing breaks when a partner mod is absent.

## What it does

- **Homestead generation** — dwellings, fields, and residents at map generation.
- **Resident self-sufficiency** — a caretaker keeps residents alive: self-tend, cook toward
  a meal stock, harvest their crops, and forage wild plants in winter.
- **Starting supplies** — each resident spawns with medicine, meals, and a modest non-AOE
  self-defence weapon (plus CE ammo when CE is present).
- **Harvest → passing trader** — harvesting draws a passing trader who buys the surplus and
  leaves preserved food and supplies.

All counts and thresholds are tunable in the mod settings.

## Building

`Source/` compiles to `Assemblies/RimSynapseLivingWorld.dll` (net48). Set your paths in
`Source/GamePath.props`. Offline behaviour suites: `Tests/run-tests.sh` (needs mono).
