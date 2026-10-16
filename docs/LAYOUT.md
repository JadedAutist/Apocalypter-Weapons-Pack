# Folder layout

| Folder | Purpose |
|---|---|
| src/Core | Shared plugin lifecycle, combat presentation and interaction helpers |
| src/Chainsaw | Chainsaw holding, attacks, fuel, physics and persistence |
| src/Flamethrower | Flame stream, effects, fuel, damage, sights and persistence |
| src/World | World loot, merchant stock/requests and maintenance |
| src/Integration | Bundled profiles and optional editor/settings integration |
| assets/runtime | Exactly the data shipped beside the plugin DLL |
| tests | Factory regression, binary release audit and isolated game integration fixture |
| scripts | Build, verify, integration test and package entry points |
| packaging | Runtime file/hash manifest |
| docs | Build, testing, publishing and folder guidance |
| artifacts, dist | Generated local output, ignored by Git |

The installed mod needs one DLL and its bundled images, models, effects and profiles. Several asset folders are normal for a content pack. Source files, Blender projects, game assemblies, test hosts, captures, caches and backups do not belong in the player installation or download.

The author workspace contains historical authoring files and unreleased weapons. It stays separate and its existing paths are retained so editor and import workflows continue to work. The public checkout is the organized, reviewable release project.
