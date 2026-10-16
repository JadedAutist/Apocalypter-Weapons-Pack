# Verification

Run build.ps1, verify.ps1 and package.ps1 as documented in BUILDING.md. Automated checks and the live checklist serve different purposes.

The factory test has 11 checks for inactive metadata creation, native-shaped null-FSM behavior and failure cleanup. ReleaseAudit inspects the built PE metadata without loading the game, checking plugin identity/version and the absence of experimental weapon types and the retired settings override. Runtime asset hashes are checked against the packaging manifest.

`verify.ps1 -GameDir <installation> -IncludeIntegration` runs the actual release DLL, installed PlayMaker and Apocapocket 2.0.10 in an isolated Unity batch process. It creates inert player references and empty slots, exercises merchant metadata and prices, calls Apocapocket BeginSave/EndSave, flushes an owned ES3 cache and verifies the pocket mapping. The host has its own BepInEx files, logs and test save; no personal save is opened. It does not run the complete native world-save state machine or inventory restoration.

To reproduce the original failure as a negative control, supply the original 1.6.0 download:

```powershell
./scripts/verify.ps1 -GameDir '<installation>' -IncludeIntegration -BaselineZip '<original 1.6.0 ZIP>'
```

The original DLL must match its known SHA-256. The original merchant exception leaves an incomplete component, and Apocapocket BeginSave then fails at its FsmName resource scan. Expected negative-control failures are reported separately from fixed checks.

## Live checklist before release

- Test the exact ZIP intended for players in a disposable installation/test slot with the user's usual mod collection. Record the package SHA-256 and installed mod versions; do not substitute the local experimental DLL.
- Start a fresh session and retain its BepInEx LogOutput.log and Unity Player.log where available. Watch for startup, game, Windows, Unity and harness error dialogs throughout testing and on shutdown. Record each unexpected prompt's text and triggering action before dismissing it.
- With the usual mod collection, open Settings/About/What's New/Files. Verify Settings is searchable/editable and controls do not appear on other tabs.
- Check readable controls at both 1080p and 4K with the user's usual UI scaling.
- Verify range defaults are exactly 5/7/10 m. Enter a range such as 4.34546434 and confirm it settles at 4.3, while 4.3 stays 4.3. Fuel costs advance in tenths (0.1/0.2/0.3 L), stay at or above 0.1 L, and do not quantize actual tank contents or continuous consumption. Save/restart and verify these values persist without recursive setting updates or new errors. Check existing settings upgrade cleanly; holding/animation profiles must retain their precision.
- Visit Slumtown, generate/refresh the merchant request board and complete a chainsaw/flamethrower delivery at the requested price. Check ordinary requests still work.
- Save and reload a disposable test slot with each weapon in the world, a native weapon slot, and an Apocapocket extra slot. Verify weapon identity, fuel, type, inventory position and merchant state.
- Repeat saving after visiting a merchant and after swapping weapons. Confirm the saving indicator completes and input returns.
- Quit normally, check for shutdown errors, restart and load the test save. Verify relevant companion mod features and weapon state still work.
- Compare behavior with Apocapocket/Apocasetter enabled and disabled when investigating a compatibility report. Capture versions, exact steps and logs.
- Equip, attack with and switch between both weapons with Apocaplayer/WildWasteland present and absent, and with the placement adjuster enabled and disabled. Check frame timing and repeated missing-type warnings; optional integrations must remain cheap when their mod is absent.
- Investigate the first unexpected exception or error prompt, including failures reported by another mod. Fix or document the cause and rerun affected workflows; do not treat later cascading errors as independent proof of another mod's fault. Label deliberate negative-control failures separately.

The isolated save-path and metadata checks pass. Full gameplay, inventory restoration, merchant payment and visual resolution checks remain outstanding until recorded by a tester. Do not treat absence of log errors as proof of compatibility.

## Release verification record

For each candidate, record the version/commit, ZIP SHA-256, game and mod versions, test date, tester and display resolution/scaling. For each workflow above, record Pass, Fail or Not tested, with reproduction steps and supporting logs or prompt captures. Keep expected negative-control output separate. Recheck affected workflows after the final change. If game control is unavailable, provide the user with specific steps and leave the result Not tested until they report back.

Summarize automated results, live results, unresolved errors and missing checks before asking for a release decision. Do not claim full compatibility or release readiness while required checks remain unperformed or unexpected errors remain unexplained. Preserve the distinction even if the author chooses to publish for testing.

## Optional body-mod lookup regression, 2026-10-16 (local candidate)

The published 1.6.1 DLL repeatedly called Harmony's fallback type search when Apocaplayer was absent. In an isolated native Unity process, 100 calls to its actual third-person check took 1356.087 ms and produced repeated missing-type warnings. The fixed public DLL performed 10,000 checks in 0.414 ms; the fixed local development DLL took 0.427 ms. These are method timings, not whole-game FPS measurements.

Both candidates passed 16 native checks covering absent-mod behavior, cached misses, loading the real Apocaplayer and WildWasteland assemblies after a miss, and all combinations of the actual third-person flags. Companion plugins were not started. The existing 11 factory checks, release identity/asset audit and 53 Unity/PlayMaker/Apocapocket fixture checks also passed. Full companion gameplay, live weapon frame timing and save/load remain pending. Published ZIPs were not replaced.

Reproduce after building using `scripts/test-optional-integrations.ps1 -GameDir <game> -PlayerBodyDll <Apocaplayer.dll> -WildWastelandDll <WildWasteland.dll>`. Optional `-BaselineZip` runs the old DLL as an explicitly labeled slow/log-spam negative control; `-DevelopmentDll` checks the separate local build too. Reports and logs stay in `artifacts/optional-integration-host`, with owned fixture objects and no personal save opened.

Live follow-up, 2026-10-16: the author confirmed that the fix worked after installing the local development Weapons Pack DLL and fixed placement adjuster. The reported severe weapon lag is resolved in that setup. Companion versions/state and individual workflows were not specified. The public packaged build, complete optional-mod matrix, save/load and editor preview controls still require their own live checks; published downloads are unchanged.

## 1.6.2 publication verification, 2026-10-16

The author requested publication of the Weapons Pack fix and explicitly excluded the adjuster from publication. The public two-weapon build was versioned 1.6.2. Installable ZIP SHA-256: `23B9603C98725F5B9F7692B9A55F3834D4E682A438D1DD594823303C96F18473`; packaged DLL SHA-256: `F11381F5C658A7686E53C9B6CCFE8D14790409A6AB209075CC07ADA585D6A228`.

The DLL extracted from that exact ZIP passed all 16 native optional-integration checks (10,000 absent-mod view checks in 0.411 ms). The 53 Unity/PlayMaker/Apocapocket fixture checks, 11 merchant factory checks, version/identity audit, runtime asset hashes and four Nexus preflight test methods passed. The existing source serialization-field compiler warnings were unchanged. The current local gameplay log showed Wild Wasteland 0.1.28 and no Error/Fatal/exception or repeated missing-Apocaplayer warnings in the inspected log; this is supporting evidence, not a complete live compatibility check.

The user-confirmed live performance result applies to the local development build with both local fixes. The exact public ZIP has not had the full live startup/settings/merchant/inventory/save/reload/4K checklist rerun. These outstanding checks are disclosed in the release notes. No adjuster DLL, development weapon, personal settings or debug tools are included in the installable package.
