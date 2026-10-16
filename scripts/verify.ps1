param([string]$GameDir=$env:APOCALYPTER_GAME_DIR,[switch]$IncludeIntegration,[string]$BaselineZip)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifest=Get-Content -LiteralPath (Join-Path $repo 'packaging/runtime-files.json') -Raw | ConvertFrom-Json
$dll=Join-Path $repo 'artifacts/runtime/BepInEx/plugins/Apocalypter Weapons Pack/ChainsawPrototype.dll'
& dotnet run --project (Join-Path $repo 'tests/MerchantFactory/FactoryTests.csproj') -c Release -v quiet
if($LASTEXITCODE -ne 0){throw 'Merchant factory checks failed.'}
& dotnet run --project (Join-Path $repo 'tests/HoldingMotion/MotionTests.csproj') -c Release -v quiet
if($LASTEXITCODE -ne 0){throw 'Weapon holding motion checks failed.'}
& dotnet run --project (Join-Path $repo 'tests/ReleaseAudit/ReleaseAudit.csproj') -c Release -v quiet -- $dll $manifest.version
if($LASTEXITCODE -ne 0){throw 'Release binary checks failed.'}
foreach($asset in $manifest.assets){
 if([IO.Path]::IsPathRooted($asset.path) -or $asset.path -match '(^|/)\.\.(/|$)|:|spear|\.(dll|pdb)$'){throw "Invalid public runtime asset path: $($asset.path)"}
 $file=Join-Path $repo ("assets/runtime/"+$asset.path)
 if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $asset.sha256){throw "Bundled asset changed: $($asset.path). Review and update the manifest intentionally."}
}
if($IncludeIntegration){& (Join-Path $PSScriptRoot 'test-integration.ps1') -GameDir $GameDir -BaselineZip $BaselineZip}
Write-Output 'Automated checks passed. Complete the live gameplay and resolution checks in docs/TESTING.md before publishing.'
