param([Parameter(Mandatory=$true)][string]$GameDir,[string]$CandidateDll,[string]$BaselineDll,[string]$DevelopmentDll,[switch]$SaveOnly)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'native-integration-result.ps1')
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$game=(Resolve-Path -LiteralPath $GameDir).Path
$hostDir=Join-Path $repo 'artifacts/fuel-capacity-host'
if(-not $CandidateDll){$CandidateDll=Join-Path $repo 'artifacts/runtime/BepInEx/plugins/Apocalypter Weapons Pack/ChainsawPrototype.dll'}
$variants=@{fixed=(Resolve-Path -LiteralPath $CandidateDll).Path}
if($BaselineDll){$variants.baseline=(Resolve-Path -LiteralPath $BaselineDll).Path}
if($DevelopmentDll){$variants.development=(Resolve-Path -LiteralPath $DevelopmentDll).Path}
New-Item -ItemType Directory -Path "$hostDir/BepInEx/plugins/CapacityChecks","$hostDir/lib" -Force | Out-Null
Copy-Item -LiteralPath "$game/Apocalypter.exe" -Destination "$hostDir/Apocalypter-CapacityChecks.exe" -Force
Copy-Item -LiteralPath "$game/UnityPlayer.dll","$game/winhttp.dll","$game/doorstop_config.ini" -Destination $hostDir -Force
Copy-Item -LiteralPath "$game/BepInEx/core" -Destination "$hostDir/BepInEx" -Recurse -Force
foreach($variant in $variants.Keys){New-Item -ItemType Directory -Path "$hostDir/lib/$variant" -Force | Out-Null;$destination=[IO.Path]::GetFullPath("$hostDir/lib/$variant/ChainsawPrototype.dll");if($destination -ne $variants[$variant]){Copy-Item -LiteralPath $variants[$variant] -Destination $destination -Force}}
foreach($pair in @(@('Apocalypter-CapacityChecks_Data','Apocalypter_Data'),@('MonoBleedingEdge','MonoBleedingEdge'))){$destination=Join-Path $hostDir $pair[0];$target=Join-Path $game $pair[1];if(-not(Test-Path -LiteralPath $destination)){New-Item -ItemType Junction -Path $destination -Target $target | Out-Null}elseif((Get-Item -LiteralPath $destination).Target -ne $target){throw 'Unexpected capacity host junction.'}}
$compilerArgs=@('/nologo','/target:library',"/out:$hostDir/BepInEx/plugins/CapacityChecks/CapacityChecks.dll","/reference:$game/BepInEx/core/BepInEx.dll","/reference:$game/BepInEx/core/0Harmony.dll")
foreach($name in @('UnityEngine','UnityEngine.CoreModule','UnityEngine.PhysicsModule','UnityEngine.JSONSerializeModule','PlayMaker','Assembly-CSharp','Assembly-CSharp-firstpass','netstandard')){$compilerArgs+="/reference:$game/Apocalypter_Data/Managed/$name.dll"}
$compilerArgs+=(Join-Path $repo 'tests/FuelCapacity/FuelCapacityEngineChecks.cs')
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Fuel capacity checker compilation failed.'}
if(@(Get-ChildItem "$hostDir/BepInEx/plugins" -Recurse -Filter '*.dll').Count -ne 1){throw 'Unexpected plugin in capacity host.'}
$executable=[IO.Path]::GetFullPath("$hostDir/Apocalypter-CapacityChecks.exe")
foreach($variant in ($variants.Keys | Sort-Object)){
 $report=Join-Path $hostDir "result-$variant.txt";$engineLog=Join-Path $hostDir "engine-$variant.log";$bepinexLog=Join-Path $hostDir 'BepInEx/LogOutput.log'
 foreach($file in @($report,$engineLog,$bepinexLog)){if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file -Force}}
 $arguments="-batchmode -nographics -logFile engine-$variant.log --capacity-variant $variant";if($SaveOnly){$arguments+=' --capacity-save-only'}
 $started=[DateTime]::UtcNow;$process=Start-Process -FilePath $executable -WorkingDirectory $hostDir -ArgumentList $arguments -WindowStyle Hidden -PassThru
 try{
  if($variant -eq 'baseline'){
   if(-not $process.WaitForExit(45000)){throw 'Fuel capacity baseline timed out.'};if($process.ExitCode -ne 0){throw "Capacity baseline exited with $($process.ExitCode)."}
   $result=Get-Content -LiteralPath $report -Raw;$expected=if($SaveOnly){'actual native save/load preserves both tanks and fuel kind'}else{'shared tank reads 8 L above former 5 L limit'}
   if(-not $result.StartsWith('FAIL:') -or $result -notmatch $expected){throw 'Baseline did not reproduce the expected capacity/save failure.'}
   Write-Output $(if($SaveOnly){'Expected negative control: original nested fuel record arrays disappear from native saved JSON.'}else{'Expected negative control: old shared tank clamps 8 L to 5 L.'})
  }else{Write-Output (Wait-NativeIntegrationResult -Process $process -ExecutablePath $executable -StartedUtc $started -ReportPath $report -LogPaths @($engineLog,$bepinexLog) -Label "$variant fuel capacity integration")}
 }finally{if(-not $process.HasExited){Stop-Process -Id $process.Id};if(Test-Path -LiteralPath $bepinexLog){Copy-Item -LiteralPath $bepinexLog -Destination "$hostDir/bepinex-$variant.log" -Force};$process.Dispose()}
 if($variant -ne 'baseline' -and (Select-String -LiteralPath "$hostDir/bepinex-$variant.log" -Pattern '\[Error|\[Fatal')){throw "Unexpected fuel capacity integration error: $variant"}
}
