# Changes

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
