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

- With the usual mod collection, open Settings/About/What's New/Files. Verify Settings is searchable/editable and controls do not appear on other tabs.
- Check readable controls at both 1080p and 4K with the user's usual UI scaling.
- Visit Slumtown, generate/refresh the merchant request board and complete a chainsaw/flamethrower delivery at the requested price. Check ordinary requests still work.
- Save and reload a disposable test slot with each weapon in the world, a native weapon slot, and an Apocapocket extra slot. Verify weapon identity, fuel, type, inventory position and merchant state.
- Repeat saving after visiting a merchant and after swapping weapons. Confirm the saving indicator completes and input returns.
- Compare behavior with Apocapocket/Apocasetter enabled and disabled when investigating a compatibility report. Capture versions, exact steps and logs.

The isolated save-path and metadata checks pass. Full gameplay, inventory restoration, merchant payment and visual resolution checks remain outstanding until recorded by a tester. Do not treat absence of log errors as proof of compatibility.
