# PROJECT_CONTEXT.md — Apocalypter Weapon Pack

## Project Overview

Game: Apocalypter

Development environment: Codex desktop app

Project: Custom weapon pack mod

Associated private development tools: Adjuster and Editor mods

Project objective: Create and maintain a standalone public weapon pack with stable weapon behavior, minimal conflicts, and no dependence on private development tools or personal game files.

## Established Decisions

1. Weapon placements approved as final/main must be permanently stored in the weapon pack.
2. The Adjuster and Editor are authoring tools, not public runtime requirements.
3. The weapon pack must operate in an independent, clean game environment.
4. Other mods must be allowed to modify shared effects and behavior wherever the game architecture supports it.
5. Compatibility should be prioritized over unnecessarily overriding shared game systems.
6. Public changelogs should describe only verified changes to released weapons and player-facing bug fixes.
7. Releases must undergo thorough file, dependency, functionality, compatibility, and log checks.
8. Private game files and development resources must not unintentionally enter releases.

## Technical Environment

Apocalypter's community modding ecosystem includes BepInEx 5.x for Unity Mono x64.

Before making architecture or dependency decisions, inspect and document:

- The exact game build being targeted
- Installed BepInEx version
- Actual runtime and compatible .NET target
- Existing project and solution structure
- Current build process
- Existing assembly references
- Weapon registration mechanism
- Placement and attachment system
- Effect and damage implementation
- Configuration and save-data handling
- Patch and hook usage
- Current release package structure

Do not assume the existing weapon pack follows the same structure as other community mods.

Record verified findings in project documentation.

## Recommended Development Tools

### Essential or strongly recommended

**Git / GitHub**

Use for source tracking, restoring working versions, code review, and automated validation.

Prefer a private repository until the author decides otherwise.

**ILSpy / ilspycmd**

Use to understand available .NET classes and methods when game APIs are undocumented.

Prefer generating an internal API reference over repeatedly guessing game structures.

**UnityExplorer**

Use in a separate development installation to inspect objects, components, transforms, and other live Unity state.

Only install a version confirmed compatible with the existing game runtime and mod loader.

### Optional tools

**dnSpyEx**

Advanced assembly inspection and debugging when ordinary logs and inspection are insufficient.

**AssetRipper**

Asset and resource inspection when necessary. Keep extracted proprietary game assets private.

**Superpowers plugin**

Investigate using it for structured planning, debugging, and test-driven workflows in Codex.

**Context7 plugin**

Use for up-to-date documentation on supported development libraries. Do not assume it provides authoritative Apocalypter game API details.

**Custom Codex skill**

A reusable Apocalypter mod-development skill may contain validated knowledge of game systems, weapon registration, placement workflows, known compatibility concerns, debugging procedures, and release requirements.

Build the skill from verified information obtained from the actual project and game, not invented APIs.

### Important distinction

Installing a tool does not automatically make Codex capable of controlling or reading it.

Use supported command-line interfaces, exported reports, available files, or explicitly configured integrations.

Do not assume direct control over another application's graphical interface.

## Proposed Project Organization

If compatible with the current build system, consider keeping logical areas for:

- Source code
- Weapon assets
- Final approved placement data
- Private development tooling
- Tests
- Build scripts
- Documentation
- Temporary outputs
- Release packages

Do not restructure a working project merely to match a suggested folder layout.

First inspect its current architecture and recommend the least disruptive organization.

## Current Unknowns

The following must be discovered rather than guessed:

- Exact source repository layout
- Build command and runtime references
- Actual names and interfaces of Adjuster and Editor
- How final weapon placements are currently saved
- Which files are currently copied into releases
- Existing compatibility conflicts
- Game update compatibility status
- Whether automated tests already exist
- Whether a separate clean game installation is available
- Whether the existing plugin and skill setup is suitable

## Desired Development Workflow

Use the existing source project as the authoritative source.

Treat the live game installation as a test environment and a source of game references only where permitted.

Keep source changes reviewable.

Test the release artifact independently of private tools.

Maintain internal technical notes separately from public release changelogs.

Do not install optional tools or perform structural changes unless their value has been established.

## Long-Term Goals

- Consistent weapon behavior
- Stable approved weapon placements
- Reliable standalone distribution
- Compatibility with relevant third-party mods
- Minimal regression risk
- Better debugging and diagnostics
- Reproducible releases
- Reduced maintenance after game updates
- Easier addition of future weapons
- Clear technical documentation for future Codex development sessions
