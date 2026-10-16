# Building

The source builds the public chainsaw/flamethrower plugin. Use an installed .NET SDK (the author uses .NET 10), PowerShell, and your local Apocalypter installation with BepInEx 5. The project targets .NET Framework 4.7.2; the SDK resolves its reference assemblies through NuGet on the first build. Tests target .NET 10.

```powershell
./scripts/build.ps1 -GameDir 'C:/Program Files (x86)/Steam/steamapps/common/Apocalypter'
./scripts/verify.ps1
./scripts/verify.ps1 -GameDir 'C:/Program Files (x86)/Steam/steamapps/common/Apocalypter' -IncludeIntegration
./scripts/package.ps1
```

You can set the environment variable APOCALYPTER_GAME_DIR instead of repeating GameDir. Game/BepInEx DLLs are references with copying disabled. Building does not install the plugin into the game.

Build output is under `artifacts/runtime/BepInEx/plugins/Apocalypter Weapons Pack`. Package output is under `dist`. Install the ZIP with the game closed to test the release, retaining your existing config files. Keep a separate development installation when working on unreleased weapons.

Runtime assets live only in `assets/runtime`. `packaging/runtime-files.json` lists their accepted relative paths and SHA-256 hashes. Intentional asset changes require updating that manifest after review. No test output or author files are selected by the packager.

`-NoRestore` on build.ps1 supports an already restored/offline project. An offline restore can use a local feed containing Microsoft.NETFramework.ReferenceAssemblies 1.0.3 and Microsoft.NETFramework.ReferenceAssemblies.net472 1.0.3. These are build dependencies, not release files.

The DLL is still named ChainsawPrototype.dll to preserve compatibility. The project, plugin display name and installed directory use Apocalypter Weapons Pack. No binary patcher or companion helper DLL is required by this source build.
