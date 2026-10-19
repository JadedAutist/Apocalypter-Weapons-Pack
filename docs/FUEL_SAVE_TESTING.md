# Fuel and saved metadata checks

Run `scripts/test-fuel-capacity.ps1 -GameDir <game> [-CandidateDll <dll>]`. The isolated host uses native PlayMaker liquid variables, FloatClamp, actual can-fill callbacks and owned ES3 disk files. It does not open personal saves or start a world. Fresh reports, logs and clean process exit are required.

The 2026-10-18 public candidate passed 42 checks. Both tanks accept and retain up to 10 L. Tests preserve 5 L contents without topping them up, retain fractions, empty the final tiny fuel amount, pour gasoline/diesel/oil from 5 to 10 L, conserve a near-full transfer, reject mixed fuels and retain the existing spawn-fuel policy. Native getters repair the owned receiver capacity so its original clamp cannot remove fuel above 5 L.

The save sweep confirmed an existing serialization defect. Native JsonUtility omitted nested record arrays even when production savers logged records. The original world/merchant output retained locally contained only `{"version":1}`. A primitive-array control worked, so a blanket claim that all host serialization was unavailable would be incorrect.

The four existing metadata payloads now use the framework JSON serializer with explicit optional members. Their ES3 keys, version and member names are unchanged. The test runs production save/load and restoration with multiple independent 5/9.8 L tanks, diesel/oil kinds, Codex discovery, failed spawn rolls and merchant price/stock decisions. It also reads valid older JSON, accepts missing/empty lists safely, rejects unsupported versions without replacing fuel, and verifies loads do not rewrite the stored payload. Native Vector3/Quaternion site fields survive a JSON roundtrip. Omitted historical data cannot be reconstructed by this correction.

This does not verify a physical can collision in a rendered world, the complete native item/inventory save state machine, generated location restoration or third-party storage. Test those workflows with the exact package and a disposable save before release.
