param([string]$GameDir=$env:APOCALYPTER_GAME_DIR,[switch]$IncludeIntegration,[string]$BaselineZip,[string]$WildWastelandDll,[string]$BloodFxDll,[string]$BloodFxConfig)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifest=Get-Content -LiteralPath (Join-Path $repo 'packaging/runtime-files.json') -Raw | ConvertFrom-Json
$dll=Join-Path $repo 'artifacts/runtime/BepInEx/plugins/Apocalypter Weapons Pack/ChainsawPrototype.dll'
& (Join-Path $repo 'tests/NativeIntegration/NativeResultGate.Tests.ps1')
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
if($IncludeIntegration){
 $runName='v-'+[Guid]::NewGuid().ToString('N').Substring(0,8)
 & (Join-Path $PSScriptRoot 'test-integration.ps1') -GameDir $GameDir -BaselineZip $BaselineZip
 & (Join-Path $PSScriptRoot 'test-holding-integration.ps1') -GameDir $GameDir -CandidateDll $dll
 & (Join-Path $PSScriptRoot 'test-equip-motion.ps1') -GameDir $GameDir -CandidateDll $dll -RunName ($runName+'-base')
 & (Join-Path $PSScriptRoot 'test-flame-windows.ps1') -GameDir $GameDir -CandidateDll $dll
 & (Join-Path $PSScriptRoot 'test-flame-patrol.ps1') -GameDir $GameDir -CandidateDll $dll -RunName ($runName+'-base')
 & (Join-Path $PSScriptRoot 'test-flame-ignite.ps1') -GameDir $GameDir -CandidateDll $dll -RunName $runName -WildWastelandDll $WildWastelandDll
 & (Join-Path $PSScriptRoot 'test-solid-melee.ps1') -GameDir $GameDir -CandidateDll $dll -RunName $runName
 & (Join-Path $PSScriptRoot 'test-flame-harvest.ps1') -GameDir $GameDir -CandidateDll $dll -RunName $runName
 & (Join-Path $PSScriptRoot 'test-settings-integration.ps1') -GameDir $GameDir -CandidateDll $dll
 & (Join-Path $PSScriptRoot 'test-fuel-capacity.ps1') -GameDir $GameDir -CandidateDll $dll
 & (Join-Path $repo 'tests/ProfileIndependence/RunnerTests.ps1') -GameDir $GameDir -CandidateDll $dll
 & (Join-Path $PSScriptRoot 'test-profile-independence.ps1') -GameDir $GameDir -CandidateDll $dll
 if($WildWastelandDll){
  & (Join-Path $PSScriptRoot 'test-equip-motion.ps1') -GameDir $GameDir -CandidateDll $dll -WildWastelandDll $WildWastelandDll -RunName ($runName+'-ww')
  & (Join-Path $PSScriptRoot 'test-solid-melee.ps1') -GameDir $GameDir -CandidateDll $dll -WildWastelandDll $WildWastelandDll -RunName ($runName+'-ww')
  & (Join-Path $PSScriptRoot 'test-flame-patrol.ps1') -GameDir $GameDir -CandidateDll $dll -WithWildWasteland -WildWastelandDll $WildWastelandDll -RunName ($runName+'-ww')
  & (Join-Path $PSScriptRoot 'test-hud-icons.ps1') -GameDir $GameDir -CandidateDll $dll -WildWastelandDll $WildWastelandDll -RunName $runName
 }
 if($BloodFxDll){
  & (Join-Path $PSScriptRoot 'test-solid-melee.ps1') -GameDir $GameDir -CandidateDll $dll -BloodFxDll $BloodFxDll -BloodFxConfig $BloodFxConfig -RunName ($runName+'-blood')
 }
}
Write-Output 'Automated checks passed. Complete the live gameplay and resolution checks in docs/TESTING.md before publishing.'
