Read README.md and docs/BUILDING.md before changes. This repository is the public chainsaw/flamethrower release source. Keep the separately developed Wooden Spear out of source, registration, settings, assets and release packages.

Keep the legacy ChainsawPrototype assembly name, plugin GUID, saved prefab IDs and reference mappings stable. Game and BepInEx assemblies are local build references; never copy them into this repository or a package. Changes must stay within Weapons Pack; editors and other mods are separate projects.

Use Apocasetter's standard scaled, tab-aware config renderer. Initialize PlayMaker components and variable names before publishing them, including inactive metadata objects. Test relevant shared-system changes with Apocapocket, Apocasetter and other affected installed mods. Exercise actual user workflows; clean logs and isolated/unit checks alone do not establish compatibility. Report automated results and outstanding live checks separately.

Build into artifacts, package through scripts/package.ps1, and review scripts/verify.ps1 output plus docs/TESTING.md before a release. Keep personal configs, saves, backups, diagnostic captures, caches and development weapons out of Git and the runtime ZIP. Do not publish or push without the user's authorization.
