# RELEASE_CHECKLIST.md — Apocalypter Weapon Pack

## Release Validation Policy

Perform these checks before every public release.

The goal is to ensure that the exact files being distributed operate correctly, contain no accidental development dependencies, and do not introduce avoidable problems.

Use three possible outcomes for each check:

- PASS — Verified successfully
- FAIL — A confirmed issue exists
- NOT VERIFIED — Insufficient evidence or testing unavailable

Never turn a NOT VERIFIED result into PASS based on assumptions.

## A. Build and Project Integrity

- [ ] Source code builds successfully.
- [ ] Correct game and mod-loader references are used.
- [ ] No unexplained build errors or warnings have been introduced.
- [ ] All expected weapon resources are included.
- [ ] No unexpected changes to unrelated systems are present.
- [ ] Source changes have been reviewed.
- [ ] The release can be reproduced using documented build inputs.

## B. Standalone Independence

- [ ] No runtime dependency on the private Adjuster mod.
- [ ] No runtime dependency on the private Editor mod.
- [ ] Final approved weapon placements are stored in standalone release-owned data.
- [ ] No private saved files are required.
- [ ] No temporary or cached development data is required.
- [ ] No hardcoded personal file-system paths remain in the packaged output.
- [ ] No undeclared DLL or resource dependencies remain.
- [ ] The weapon pack works without private helper mods.
- [ ] All legitimate public requirements are clearly declared.

## C. Release Package Inspection

- [ ] No duplicated DLLs or asset files.
- [ ] No unnecessary or obsolete files.
- [ ] No experimental or unreleased weapon resources.
- [ ] No development-only scripts or utilities.
- [ ] No Editor/Adjuster files.
- [ ] No unrelated private mods.
- [ ] No backup, cache, or temporary files.
- [ ] No private logs or personal settings.
- [ ] No prohibited proprietary game assets.
- [ ] No missing required weapon assets.
- [ ] Correct directory structure for installation.
- [ ] Package contents match an approved release manifest or allowlist.

Inspect generated binaries and their dependency references, not only source files.

## D. Weapon Gameplay

Test all released weapons against their expected behavior.

- [ ] Weapon registration and spawning.
- [ ] Correct models and textures.
- [ ] Correct positions and rotations.
- [ ] Correct final placements without development tools.
- [ ] Equipping and unequipping.
- [ ] Weapon switching.
- [ ] Attacks and hit detection.
- [ ] Damage values and balance.
- [ ] Attack animations and timing.
- [ ] Sounds.
- [ ] Blood and impact effects.
- [ ] Dropping and picking up.
- [ ] Save/load where applicable.
- [ ] Repeated use without accumulating errors.
- [ ] No unintended changes to other weapons.

Compare important behavior against the last known working release.

## E. Compatibility With Other Mods

Maintain a practical compatibility test matrix.

Test, where available:

- [ ] Weapon and combat modifications.
- [ ] Blood and visual effect modifications.
- [ ] Animation modifications.
- [ ] Sound modifications.
- [ ] Damage and physics modifications.
- [ ] UI modifications.
- [ ] Relevant popular Apocalypter mods.
- [ ] Different load orders where applicable.
- [ ] Optional mods being absent.

Verify that the weapon pack does not unnecessarily reset or replace shared changes introduced by other mods.

Where a conflict exists, isolate the cause and document the results.

Do not claim compatibility with mods that have not been tested.

## F. Logs and Runtime Stability

Collect logs from a relevant gameplay test, including startup and shutdown.

- [ ] No new mod-caused startup exceptions.
- [ ] No missing assembly or asset errors.
- [ ] No duplicate registration failures.
- [ ] No new recurring error spam.
- [ ] No unexpected compatibility warnings caused by our mod.
- [ ] No repeated initialization errors.
- [ ] No unhandled exceptions during weapon use.
- [ ] No unintended growth in diagnostic output.
- [ ] No mod-caused failures during save/load or exit.

Compare with a baseline where possible.

Do not hide or suppress errors to obtain a clean-looking log.

## G. Clean Installation Test

Test the exact final package, not merely the development project.

- [ ] Fresh game environment.
- [ ] Supported base game and public mod-loader dependencies only.
- [ ] No Adjuster installed.
- [ ] No Editor installed.
- [ ] No private development mods installed.
- [ ] No reliance on personal game configuration.
- [ ] Released weapons function as expected.
- [ ] Relevant additional mod combinations tested separately.
- [ ] Final package matches the files tested.

If a true clean gameplay test is unavailable, mark it NOT VERIFIED.

## H. Public Changelog

- [ ] Only verified, player-facing changes included.
- [ ] Weapon fixes accurately described.
- [ ] Balance adjustments accurately described.
- [ ] Behavior and animation changes accurately described.
- [ ] No mention of intentionally excluded weapons or features.
- [ ] No details of internal development tools.
- [ ] No internal auditing or release-testing explanations.
- [ ] No claims about changes not actually included.

Keep the public changelog separate from technical release reports.

## I. Final Release Decision

Produce a private validation report containing:

**Release version:**

**Target game version:**

**Build result:**

**Package inspection:**

**Standalone dependency test:**

**Weapon regression tests:**

**Compatibility tests performed:**

**Log inspection results:**

**Outstanding issues:**

**Unverified checks:**

**Release recommendation: GO / NO-GO**

GO requires no known release-blocking defects and completion of all required checks.

If essential checks could not be performed, explain the uncertainty and request the necessary verification before certifying release readiness.

Do not upload or publish anything without explicit authorization.
