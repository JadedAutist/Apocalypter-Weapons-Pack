# Changes

## 1.6.4

- Show the chainsaw and flamethrower models in Wild Wasteland's third-person view.
- Give the third-person chainsaw two-handed holding, melee movement, both LMB swings and continuous RMB cutting animations.
- Prevent RMB zoom while using either weapon.
- Align the chainsaw crosshair with the game's normal HUD placement and scaling.
- Group settings under Weapons, Fuel and Smoke tabs, keeping collapsible weapon sections.

## 1.6.3

- Replace flamethrower aiming with RMB fuel spraying. LMB ignites connected fuel patches; burning ground damages enemies and applies ignite independently of the flame stream.
- Add adjustable ignite damage: 10 damage per second initially, gaining 10 per second of flame contact up to 50. Burning expires ten seconds after the last contact.
- Give ground fire varied animated flames and a configurable minimum burn time of 15 seconds.
- Spread moving ignite flames across enemy bodies, including larger monsters and bosses, while avoiding helmet trophies and held equipment.
- Let ending flames follow fallen corpses as they fade. Add optional smoke to enemies and ground fire, plus two seconds of smoke across freshly burnt corpses.
- Give flame-killed corpses a charred appearance with muted red cracks and subtle surface detail, including corpses with later custom skin changes.
- Make flame-killed harvestable corpses yield their corresponding cooked meat in the usual quantities. Preserve burnt appearance and cooked harvesting across saving.
- Allow flamethrower hits through recognised vehicle window panes and on mounted enemy hitboxes. Metal frames, bodywork and windscreen grids remain solid cover.
- Use the current shared blood effect for chainsaw hits on living enemies and corpses. Support optional BloodFX for both attacks, with standalone fallback; flame damage does not trigger its hit spray.
- Add slight vibration during continuous chainsaw cutting and small metal sparks on metal surfaces, including car bodies and doors.
- Increase both weapon tanks to 10 L. Preserve fuel amounts, fuel type, world spawn decisions and merchant stock across save/load.
- Follow Wild Wasteland's optional smooth equip and weapon-switch movement with arms and models together, and show the correct weapon HUD images.
- Add collapsible weapon settings and 0.1-step balance sliders within Apocasetter's normal settings panel.
- Stop repeated warnings when the optional Inspector mod is absent.

## 1.6.2

- Keep weapon sway smooth while running.
- Hide held weapons and stop their firing effects correctly on death.


- Fix severe weapon lag and repeated warnings when the optional player-body mod is absent.

## 1.6.1

- Restore Apocasetter's native settings rendering, scaling, search and detail tabs.
- Fix merchant requests that could cause errors in other mods or interrupt saving with Apocapocket.
- Use exact flamethrower range defaults of 5 m, 7 m and 10 m. Round player-facing weapon balance settings to one decimal place, including fuel costs (0.1 L steps, minimum 0.1 L), on load and when edited. Chainsaw swing rate now defaults to 0.8 swings/second.

Other balance defaults, saved weapons, spawn chances and merchant prices remain unchanged.

## 1.6.0

Added branded installation packaging, final world spawn chances, and Slumtown merchant stock and purchase requests. Chainsaw and flamethrower models, sounds, animations, fuel handling and Codex entries are included.
