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
- Investigate the first unexpected exception or error prompt, including failures reported by another mod. Fix or document the cause and rerun affected workflows; do not treat later cascading errors as independent proof of another mod's fault. Label deliberate negative-control failures separately.

The isolated save-path and metadata checks pass. Full gameplay, inventory restoration, merchant payment and visual resolution checks remain outstanding until recorded by a tester. Do not treat absence of log errors as proof of compatibility.

## Release verification record

For each candidate, record the version/commit, ZIP SHA-256, game and mod versions, test date, tester and display resolution/scaling. For each workflow above, record Pass, Fail or Not tested, with reproduction steps and supporting logs or prompt captures. Keep expected negative-control output separate. Recheck affected workflows after the final change. If game control is unavailable, provide the user with specific steps and leave the result Not tested until they report back.

Summarize automated results, live results, unresolved errors and missing checks before asking for a release decision. Do not claim full compatibility or release readiness while required checks remain unperformed or unexpected errors remain unexplained. Preserve the distinction even if the author chooses to publish for testing.
