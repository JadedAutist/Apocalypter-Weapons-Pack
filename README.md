# Apocalypter Weapons Pack 1.6.4

![Apocalypter Weapons Pack](docs/images/header.png)


Adds a gasoline-powered chainsaw and a refillable flamethrower with their own models, inventory images, Codex entries, fuel tanks and saved item identities.

## Installation

Requires BepInEx 5 for the game. Close Apocalypter, then extract this archive into the game folder so that `BepInEx/plugins/Apocalypter Weapons Pack` contains the DLL and its asset folders.

Keep your existing `BepInEx/config` files when updating. Install only one copy of Apocalypter Weapons Pack.

The pack includes its approved holding and attack profiles. Inspector and Apocasetter are optional separate mods. Without Apocasetter, edit `BepInEx/config/apocalypter.weaponspack.cfg` while the game is closed.

## Finding the weapons

- Chainsaw: 25% chance in each Buzzgut and Black Juggernaut boss area.
- Flamethrower: 25% chance at each Professor location and underground Laboratory.
- Slumtown weapons merchant: an independent 10% chance of stocking each weapon, at a random price of 3,000–4,000 nuts each. Use the normal Buy interaction. The same merchant can request either weapon for 2,000–3,000 nuts, using the normal delivery/sale confirmation. Requests roll independently at 10% each when its native request board refreshes.

Each location/shop rolls once for its generated stock. Visiting again, purchasing an item or reloading does not reroll it. Both successful and unsuccessful rolls are saved. Existing saves can receive rolls at locations without a previous pack record. Previously generated test loot is retained.

## Controls and fuel

- Chainsaw: LMB swings; hold RMB for continuous sawing. Both attacks consume gasoline, including misses, and use the last fraction of fuel.
- Chainsaw hits on metal, including car bodies and doors, use a small native spark impact. Wild Wasteland's spark size and enable setting are respected when available. Both chainsaw attacks use BloodFX blood effects when supported and enabled, or the current shared standalone blood impact otherwise. BloodFX is optional.
- Ignite distributes flames over the enemy's posed body shape, scaling coverage for larger and wider models, with width and height changing independently over time. Rigid helmet trophies and held equipment do not add fire origins when an animated body surface is available. Enemies killed by flames receive a charred corpse appearance with broad charcoal patches, muted red gaps, shallow surface detail and a slight sheen on supported materials. The burn adapts to later custom corpse skin material swaps while preserving the custom mesh and shader. Damage and burn settings remain adjustable.
- Flamethrower: hold LMB to fire flames. Hold RMB to spray unlit fuel on the ground; LMB ignites it, and fire spreads through connected patches.
- Refill the dropped weapon by pouring an appropriate can into its tank using normal game pouring. Both tanks hold 10 L.
- The chainsaw accepts gasoline. The flamethrower accepts gasoline, diesel or engine oil, one type at a time. Empty the tank before changing fuels.

Gasoline provides the shortest flame range and lowest damage, diesel is intermediate, and engine oil has the longest range and highest damage. The current fuel, damage and range appear on the weapon tooltip. Fuel counters and empty-tank audio show when a refill is needed.

The flame stream arcs down slightly and can damage several enemies across its width. Solid cover stops it. Direct flame contact heats native explosive fuel containers before ignition. Scorch marks fade within five seconds; brief surface fire shrinks and fades. Muzzle glow, smoke, light, recoil and stove ignition/burner sounds accompany firing.

Flame contact also ignites living enemies. Burning starts at 10 damage per second and gains another 10 for each second of flame contact, up to 50 damage per second after four seconds. It lasts ten seconds after the last contact. The attached fire shrinks and fades when the burn expires. On death, its ending flames follow the captured corpse during the fade, followed by brief smoke. Switching weapons does not cancel an existing burn.

Harvestable enemies killed by flames yield the corresponding cooked dog, insect or mutant meat in their usual quantities. Other drops and enemies killed by other means remain unchanged. Burnt corpse appearance and cooked harvesting are restored when native saved corpses reload.

Burning fuel patches deal their fuel type's damage per second and apply ignite while enemies stand in them. Their damage adds to the flame stream; overlapping patches count as one ground-fire contact. Solid cover and gaps stop spreading. Burning patches last at least 15 seconds by default, with more fuel extending the burn. Unlit fuel evaporates after 60 seconds by default. Patches are temporary and do not persist through saving or scene changes.

## Settings

Wild Wasteland's third-person view shows each weapon's own model. The chainsaw uses two-handed holding and its own swing and continuous-cutting poses. RMB on either weapon retains the normal field of view.

When Wild Wasteland's Smooth weapon switch setting is enabled, the chainsaw and flamethrower follow its first-person lowering and raising movement with their arms and models together. Wild Wasteland remains optional; disabling its feature retains the normal pack holding poses.

Damage, range, fuel cost and attack rate are adjustable. Flamethrower settings are separate for each fuel. Fuel consumption per second equals fuel cost per attack multiplied by attack rate. Flame damage per second stays independent of attack rate. Spawn chances are fixed. Fire smoke can be disabled separately from flames and damage.

In Apocasetter, settings are grouped under Weapons, Fuel and Smoke tabs. The Chainsaw and Flamethrower headings can be collapsed. Searching reveals matching settings. Ignite has separate controls for damage per stack, maximum stacks, contact interval and burn duration. Under Flamethrower - Smoke, Enabled controls all pack fire smoke, with separate Burning enemies, Ground fire and Burnt corpses switches. Turning a switch off removes its active smoke; flames and damage continue. Corpse smoke spreads across the body for two seconds after death, then the remaining wisps fade.

Player-facing balance settings use one decimal place, including fuel costs (0.1/0.2/0.3 L; minimum 0.1 L). Actual tank contents and continuous draining retain full precision. Existing saved values and new edits round to that precision. Flamethrower range defaults are exactly 5 m for gasoline, 7 m for diesel and 10 m for engine oil; chainsaw LMB defaults to 0.8 swings per second. Authored holding and animation profiles are unaffected.

## Release changes

1.6.4 adds third-person weapon models and chainsaw holding and attack animations, prevents RMB zoom, aligns the chainsaw crosshair with the normal HUD, and groups settings into Weapons, Fuel and Smoke tabs.

1.6.3 adds RMB fuel spraying, connected ground fires, stacking ignite damage, body-shaped flames, burnt corpses and cooked harvesting. Both tanks now hold 10 L. It improves chainsaw blood and metal impacts, adds collapsible settings, fixes flamethrower hits through vehicle panes, and supports Wild Wasteland's weapon icons and smooth equip movement. See CHANGELOG.md for the full changes.

1.6.2 fixes severe weapon lag when the optional player-body mod is absent, steadies running sway and hides held weapons correctly on death.

1.6.1 fixes merchant requests that could interrupt saving or cause errors in other mods, and restores Apocasetter's standard scaled settings and detail tabs.


1.6.0 removes the spawn testing controls and new test-site creation, sets flamethrower world rarity to 25%, adds Slumtown merchant stock, and uses the branded runtime folder. Saves made with old test sites retain the minimum geometry restoration needed to support their saved items. Existing holding profiles, authored chainsaw animations, fuel settings and stock M16 assets are retained.

The download contains runtime files and required profiles. Personal backups, source, editors, diagnostic captures, caches and debug symbols are excluded.


Download the installable ZIP from this repository's Releases page.

## Source and development

The public source builds the chainsaw and flamethrower. See [building](docs/BUILDING.md), [testing](docs/TESTING.md), [publishing](docs/PUBLISHING.md), [folder layout](docs/LAYOUT.md), and [credits](CREDITS.md). Unreleased local weapon work is kept outside this repository.
