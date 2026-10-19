# AGENTS.md — Apocalypter Mod Development Rules

## Purpose

This repository contains an Apocalypter weapon pack mod.

The long-term objective is to create a stable, reliable, maintainable, standalone weapon pack that maximizes compatibility with other mods, preserves working gameplay behavior, and can be safely released to other players.

These are permanent project rules. Follow them during feature development, bug fixing, refactoring, testing, and release preparation.

## 1. Understand Before Modifying

- Inspect relevant code, assets, configurations, and existing behavior before making changes.
- Understand why an existing implementation works before replacing it.
- Never assume a file or system is unnecessary without verifying its purpose.
- Preserve existing working weapons, approved placements, damage values, animations, and effects unless the task explicitly requires changing them.
- Avoid broad refactoring when a smaller, safer change will solve the problem.
- Do not introduce unrelated features or gameplay modifications.
- Explain the risks before making major architectural changes.
- Never claim something has been tested unless the test was actually performed.

When uncertain about a game API or behavior, inspect the available evidence instead of inventing an implementation.

## 2. Compatibility First

The weapon pack must be designed to coexist with other mods wherever technically possible.

- Prefer supported game APIs and additive registrations over replacing shared systems.
- Avoid unnecessary Harmony patches, broad overrides, shared asset replacements, and global behavior modifications.
- Scope modifications to our own weapons and resources.
- Use unique identifiers, namespaces, configuration keys, and registration names.
- Do not overwrite another mod's configuration or files.
- Do not assume a fixed mod load order.
- Avoid depending on undocumented behavior when a supported alternative exists.
- If patches are necessary, make them narrowly targeted, defensive, and compatible with other patches wherever possible.
- Ensure initialization is safe when repeated, and clean up registered handlers or temporary resources when appropriate.
- Do not introduce unnecessary runtime dependencies on optional mods.
- Detect optional integrations safely.

Absolute compatibility with every possible third-party mod cannot be guaranteed. Aim for maximum compatibility, test representative conflicts, and report limitations accurately.

## 3. Respect Other Mods' Effects

Our mod must not unnecessarily override other mods' changes to shared visual, audio, or gameplay systems.

Examples include:

- Blood and impact effects
- Particles and decals
- Textures and materials
- Sounds
- Animations
- Enemy reactions
- Physics and collision behavior
- Shared damage effects
- UI behavior

If another mod changes the game's blood effects, our weapons should use the modified effects wherever the game's effect system allows.

Prefer dynamically using the game's current shared effect system instead of forcing a specific vanilla asset or permanently caching a replacement-prone resource.

Weapon-specific assets may remain custom when required, but must not unnecessarily replace global effects.

## 4. Strict Independence From Adjuster and Editor

The private Adjuster and Editor mods are development tools only.

The released weapon pack must not require either tool.

Rules:

- Approved weapon placements must be incorporated directly into the standalone weapon pack.
- Final positions, rotations, offsets, scales, and other required data must be saved in release-owned resources.
- After a placement is accepted as the main placement, it must function without the development tools.
- No released runtime code may load placement data from the Adjuster or Editor.
- No released runtime code may call APIs belonging to those tools.
- Never require private configuration files or cached tool outputs.
- Do not include Adjuster or Editor DLLs, scripts, settings, temporary files, or other development-only resources in a release.
- A future placement edit may use these development tools again, but the resulting approved placement must be baked into the weapon pack.

Test finalized placements without either tool installed.

The release should behave identically whether or not the developer's private tools are present.

## 5. Separate Development and Release Environments

Never treat the personal game installation as the authoritative release source.

Maintain logical separation between:

- Project source
- Approved weapon assets and placement data
- Private development tools
- Experimental content
- Build outputs
- Test installations
- Public release packages

Investigate whether private authoring tools can safely live outside the game's installation folder.

Do not relocate required game-managed files without confirming that the game and mod loader support doing so.

Do not automatically modify or clean the personal game directory.

Never recursively copy an entire private game or mods directory into a public release.

Use an explicit release-file allowlist or equivalent controlled packaging process.

## 6. Dependencies and Build Integrity

A released weapon pack must be self-contained except for clearly declared public requirements, such as the supported game and mod loader.

- No machine-specific paths.
- No hidden local dependencies.
- No private mod DLL references at runtime.
- No private saved game or cache dependencies.
- No undeclared third-party libraries.
- No absolute development paths embedded into packaged files.
- No missing textures, models, sounds, configurations, or resource references.
- No accidental inclusion of private resources or unrelated mods.
- No reliance on manually modified original game files.

Build from source using documented build inputs and tool versions.

Keep local reference assemblies and proprietary game resources out of public source repositories and releases unless redistribution is permitted.

Verify that the exact packaged release works in a clean environment.

## 7. Code Quality and Defensive Programming

- Validate external data and resource references before using them.
- Handle missing objects and unexpected values safely.
- Prefer explicit error handling over silent failures.
- Avoid unnecessary allocations or expensive work in frequently executed gameplay methods.
- Avoid repeated scanning or disk access during combat when it can be safely cached.
- Keep weapon-specific functionality modular.
- Limit shared mutable state.
- Avoid duplicate event subscriptions and registrations.
- Release temporary resources appropriately.
- Guard against double initialization and repeated loading.
- Keep version-specific compatibility code isolated.
- Preserve backward compatibility with previously released weapon configurations and saves wherever practical.

If changing serialized data formats, identifiers, or configuration formats, implement and test migration or compatibility behavior where needed.

Never silently delete player settings, saves, or previously supported data.

## 8. Weapon Functionality Standards

For each weapon, consider:

- Spawning and registration
- Models and textures
- Position, rotation, scale, and attachment offsets
- Equipping and unequipping
- Attacking and weapon switching
- Damage and balance
- Attack timing
- Animations
- Hit detection and collision
- Sounds and impact effects
- Dropping and picking up
- Save/load persistence, where relevant
- Interaction with shared game systems
- Compatibility with other mods

Never assume that a weapon functioning correctly once means all of its states work correctly.

Approved weapon values and placements are treated as intentional unless instructed otherwise.

## 9. Logging and Debugging

Inspect relevant game, mod-loader, and mod logs when debugging and testing.

Check for:

- Startup errors
- Missing assets
- Invalid references
- Failed initialization
- Duplicate registrations
- Unhandled exceptions
- Compatibility warnings
- Unintended shared-system overrides
- Errors introduced during gameplay
- Repeated warnings or errors
- Problems during save/load and game exit

Compare logs against a baseline where possible to distinguish existing game or third-party warnings from new problems caused by our mod.

Avoid repetitive diagnostic output.

Rate-limit recurring diagnostics where appropriate, but do not suppress genuine failures or disguise an unresolved error.

Debug logging should be configurable and should not spam ordinary players' logs.

## 10. Automated Testing and Validation

Where possible, implement repeatable checks for:

- Successful compilation
- Required file presence
- Resource reference validity
- Unique identifiers
- Forbidden development dependencies
- Invalid absolute paths
- Accidental private file inclusion
- Duplicate or unnecessary packaged files
- Release manifest consistency
- Configuration correctness
- Regression of previously working weapon behavior

When practical, use a separate clean test environment and representative compatibility test profiles.

Automated checks complement real gameplay testing; they do not replace it.

If the game cannot be launched in the available environment, report gameplay testing as NOT VERIFIED.

## 11. Safe Use of Modding Tools

Appropriate development tools may include:

- UnityExplorer for live Unity inspection
- ILSpy or ilspycmd for inspecting .NET assemblies
- dnSpyEx for advanced debugging
- AssetRipper for examining assets
- Git and GitHub for version control and automated validation
- Game-appropriate C# analyzers and build tools
- Optional Codex skills and documentation integrations

Only adopt tools after verifying compatibility with the installed game, framework, and project.

Tools must remain development-only unless their runtime use is an intentional, approved, and documented public dependency.

Do not blindly install, upgrade, or replace frameworks or tools.

Do not redistribute extracted game resources or third-party mod content without permission.

## 12. Git and Change Management

- Use Git to track source changes.
- Keep generated files, local game installations, extracted proprietary data, private tools, and caches out of version control unless explicitly appropriate.
- Make changes in focused, reviewable groups.
- Preserve the ability to restore a known working version.
- Avoid mixing unrelated fixes in one change.
- Review changed files before accepting major modifications.
- Do not overwrite or discard uncommitted work without permission.
- Do not automatically push, publish, tag releases, or modify public release pages.

For risky work, create a safe checkpoint before applying changes.

## 13. Public Changelog Policy

Public changelogs must only describe actual, verified, player-facing changes involving the released weapon pack.

Appropriate entries include:

- Weapon bug fixes
- Damage or balance adjustments
- Weapon placement corrections
- Animation fixes
- Combat behavior fixes
- Impact effect fixes
- Changes to existing released weapon functionality

Do not include:

- Unreleased weapons or features
- Features deliberately left out
- Internal development details
- Private tools
- Testing procedures
- Dependency audits
- Packaging processes
- Experimental content
- Internal implementation decisions

Never discuss an unreleased weapon, such as a spear, merely to explain that it was excluded.

Keep internal engineering notes separate from public changelogs.

## 14. Release Safety

Before any release, follow the release checklist.

Never declare a release ready solely because the code compiles.

Release readiness requires evidence covering packaging, runtime independence, weapon functionality, and relevant compatibility tests.

Unknown or untested areas must be explicitly identified.

Do not certify a release with known release-blocking defects.

## 15. How Codex Should Work

For each meaningful development task:

1. Inspect the relevant implementation.
2. Identify the intended change and affected systems.
3. Consider compatibility and regression risks.
4. Choose the safest reasonably maintainable solution.
5. Implement only the requested changes.
6. Run relevant automated checks.
7. Inspect the resulting differences.
8. Report what changed and why.
9. Explain what was tested and what remains unverified.

For significant structural changes, propose a plan and request approval before implementation.

For normal approved development work, avoid unnecessary interruptions while following the established rules.

Never say that an issue is fixed without evidence appropriate to that issue.

**Primary objective:** Deliver a stable, standalone, future-resilient Apocalypter weapon pack that preserves intended behavior and coexists with other mods wherever technically feasible.

## Public source checkout safeguards

Read [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md), [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md), README.md and docs/BUILDING.md before relevant work. These three permanent policy documents are included in this checkout so a standalone clone does not depend on a private parent workspace. The permanent policy above takes precedence over conflicting older supplemental guidance below.

This is an existing public repository; do not change its visibility, history, tags or releases without explicit authorization. Project-owned source, build scripts and tests may remain in this source repository; private Adjuster/Editor resources and prohibited game resources must not be added to it or the player runtime package.

The following existing public-release safeguards remain in force where consistent with the permanent policy. Historical verification records are evidence for their recorded build and scope, not current certification. Do not copy private experimental-development notes into this checkout or use them in public changelogs.

Read README.md and docs/BUILDING.md before changes. This repository is the public chainsaw/flamethrower release source. Keep the separately developed Wooden Spear out of source, registration, settings, assets and release packages.

Keep the legacy ChainsawPrototype assembly name, plugin GUID, saved prefab IDs and reference mappings stable. Game and BepInEx assemblies are local build references; never copy them into this repository or a package. Changes must stay within Weapons Pack; editors and other mods are separate projects.

Keep player-facing balance defaults and saved/live values at one decimal place, including fuel costs (0.1/0.2/0.3 L; minimum 0.1 L). Keep actual tank contents and continuous draining at full precision. Values such as 4.3 and 1.8 are intentional; remove long slider tails without forcing whole numbers or multiples of ten. Preserve authored placement/rig/animation precision. Flamethrower range defaults are exactly 5/7/10 m.

Future weapon motion must be compared with the native wrench at walking and running speeds. Added sway must use a smooth time-based cadence, not a travel-distance oscillator that speeds up during sprinting. Sample once per rendered frame and reuse the result in render callbacks. Preserve authored grips and attacks. Private held rigs outside WeaponsArm must obey native container visibility and player health/death, including in LateUpdate/pre-render callbacks. Verify weapons, effects and audio disappear appropriately on death and return correctly after reload/respawn. Record these checks separately from implementation claims.

Treat stock weapons as the baseline for core behavior: equip/unequip, switching, carrying/dropping and inventory, movement sway, pause/input gating, first/third-person visibility, death/scene cleanup, reload/respawn and save/load contracts. Compare each custom weapon with a suitable native reference. Keep its own identity, models, grips, attacks, balance and intended special mechanics. Any departure from stock core behavior must be deliberate and tested. Avoid per-frame discovery scans and duplicated motion/effect work in render callbacks.

Use Apocasetter's standard scaled, tab-aware config renderer. Initialize PlayMaker components and variable names before publishing them, including inactive metadata objects. Test relevant shared-system changes with Apocapocket, Apocasetter and other affected installed mods. Exercise actual user workflows; clean logs and isolated/unit checks alone do not establish compatibility. Report automated results and outstanding live checks separately.

Build into artifacts, package through scripts/package.ps1, and review scripts/verify.ps1 output plus docs/TESTING.md before a release. Keep personal configs, saves, backups, diagnostic captures, caches and development weapons out of Git and the runtime ZIP. Do not publish or push without the user's authorization.

Remember the 1.6.0/1.6.1 incident: incomplete merchant PlayMaker metadata broke other mods' scans and Apocapocket save startup, while a custom settings panel bypassed Apocasetter's tabs and scaling. Builds and mocked tests alone missed this. Check actual native engine behavior and downstream mod workflows.

For every release, test the exact packaged build with the usual mod collection in a disposable setup. Follow docs/TESTING.md, review fresh BepInEx/Unity logs and watch for game, Windows, Unity and test-harness error prompts from startup through shutdown. Record and investigate unexpected prompts and the first error; do not dismiss them or blame another mod without evidence. Distinguish intentional negative-control failures from unexpected errors. Record package identity, tested mod versions, workflows, results and outstanding checks. Report unresolved errors and missing live checks before the release decision; never claim unperformed compatibility checks passed. When direct game control is unavailable, request specific user-run checks and retain their pending status until results arrive.

Fuel-patch/lifecycle lesson, 2026-10-18: native NPC Health FSMs can create their physical corpse below 1 HP, before zero. Derive flame-death attribution from the enabled current-state Health comparison and its qualified local corpse transition; never impose a human/species allowlist or infer death from a mutable loot output. Test positive-fraction native deaths and preserve unrelated-death controls. Ground fuel and the stream have independent damage funding, while overlapping contact intervals must be combined to preserve the agreed ignite stacking speed. Test barriers, supported patch connectivity, final fuel fractions and cleanup. The user-approved diagnostic proved that a DDOL helper can itself be destroyed during loading; a passing explicit Update check does not verify automatic lifecycle. Keep any separately authorized third-party lifecycle repair outside pack runtime/source archives, and require live confirmation after a scene-recovery change. See docs/FUEL_PATCH_TESTING.md and the current TESTING.md; publication remains blocked on missing live workflows.

Fire/blood integration lesson, 2026-10-18: optional effect availability needs an active component or observed updater, not just an enabled config. Exercise the original public DLL plus absent, disabled and non-running cases; never rely on a private repair. Reuse native corpse-hit policy so cooldown, intensity/count settings and pools remain respected. Material/particle assertions are not rendered visual approval. The attempted startup Awake Camera.Render preview crashed natively; retain that failure and do not repeat startup render previews as a substitute for live checks.
