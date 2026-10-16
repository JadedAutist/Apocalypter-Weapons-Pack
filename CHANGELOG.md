# Changes

## 1.6.2

- Use shared time-based holding sway so running does not accelerate the added weapon bob; sample once per rendered frame.
- Hide private weapon rigs when native weapon visibility or player health/death forbids them, including in late pose/render callbacks. Stop owned firing and shake on teardown.
- Preserve authored holding profiles and attack data. Native fixture checks pass; the author confirmed the local sway/death fixes.


- Cache missing optional player-body types so equipping a weapon without Apocaplayer does not repeatedly scan every loaded type or flood the log. Late-loaded companion assemblies remain detectable. The author confirmed the local lag fix; native regression checks cover absent and late-loaded companion types.

## 1.6.1

- Restore Apocasetter's native settings rendering, scaling, search and detail tabs.
- Initialize merchant request PlayMaker FSMs before assigning metadata on inactive objects.
- Use real variable names and refreshed lookups for ItemName and SellValue.
- Remove an unpublished request immediately if construction fails.
- Prevent the reproduced null-FSM failure in Apocapocket 2.0.10's save-start scan.
- Use exact flamethrower range defaults of 5 m, 7 m and 10 m. Round player-facing weapon balance settings to one decimal place, including fuel costs (0.1 L steps, minimum 0.1 L), on load and when edited. Chainsaw swing rate now defaults to 0.8 swings/second.

The runtime includes the chainsaw and flamethrower. Other balance defaults, save identities, world chances and merchant prices remain unchanged. Authored grip, animation and presentation data retain their precision. This source tree restores the original release source and applies the hotfix; experimental local weapons are excluded.

## 1.6.0

Added branded installation packaging, final world spawn chances, and Slumtown merchant stock and purchase requests. Chainsaw and flamethrower models, sounds, animations, fuel handling and Codex entries are included.
