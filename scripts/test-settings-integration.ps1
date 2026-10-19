param([Parameter(Mandatory=$true)][string]$GameDir,[string]$CandidateDll,[string]$BaselineDll)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'native-integration-result.ps1')
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$game=(Resolve-Path -LiteralPath $GameDir).Path
$hostDir=Join-Path $repo 'artifacts/settings-integration-host'
if(-not $CandidateDll){$CandidateDll=Join-Path $repo 'artifacts/runtime/BepInEx/plugins/Apocalypter Weapons Pack/ChainsawPrototype.dll'}
$variants=@{fixed=(Resolve-Path -LiteralPath $CandidateDll).Path}
if($BaselineDll){$variants.baseline=(Resolve-Path -LiteralPath $BaselineDll).Path}
New-Item -ItemType Directory -Path "$hostDir/BepInEx/plugins/SettingsChecks","$hostDir/lib" -Force | Out-Null
Copy-Item -LiteralPath "$game/Apocalypter.exe" -Destination "$hostDir/Apocalypter-SettingsChecks.exe" -Force
Copy-Item -LiteralPath "$game/UnityPlayer.dll","$game/winhttp.dll","$game/doorstop_config.ini" -Destination $hostDir -Force
Copy-Item -LiteralPath "$game/BepInEx/core" -Destination "$hostDir/BepInEx" -Recurse -Force
$apocasetter=Join-Path $game 'BepInEx/plugins/Apocasetter/Apocasetter.dll'
Copy-Item -LiteralPath $apocasetter -Destination "$hostDir/lib/Apocasetter.dll" -Force
Write-Output ("Actual installed Apocasetter: "+(Get-FileHash -LiteralPath $apocasetter -Algorithm SHA256).Hash)
foreach($variant in $variants.Keys){New-Item -ItemType Directory -Path "$hostDir/lib/$variant" -Force | Out-Null;$destination=[IO.Path]::GetFullPath("$hostDir/lib/$variant/ChainsawPrototype.dll");if($destination -ne $variants[$variant]){Copy-Item -LiteralPath $variants[$variant] -Destination $destination -Force}}
foreach($pair in @(@('Apocalypter-SettingsChecks_Data','Apocalypter_Data'),@('MonoBleedingEdge','MonoBleedingEdge'))){$destination=Join-Path $hostDir $pair[0];$target=Join-Path $game $pair[1];if(-not(Test-Path -LiteralPath $destination)){New-Item -ItemType Junction -Path $destination -Target $target | Out-Null}elseif((Get-Item -LiteralPath $destination).Target -ne $target){throw 'Unexpected settings host junction.'}}
$compilerArgs=@('/nologo','/target:library',"/out:$hostDir/BepInEx/plugins/SettingsChecks/SettingsChecks.dll","/reference:$game/BepInEx/core/BepInEx.dll","/reference:$game/BepInEx/core/0Harmony.dll")
foreach($name in @('UnityEngine','UnityEngine.CoreModule','UnityEngine.IMGUIModule','UnityEngine.TextRenderingModule','PlayMaker','netstandard')){$compilerArgs+="/reference:$game/Apocalypter_Data/Managed/$name.dll"}
$compilerArgs+=(Join-Path $repo 'tests/SettingsIntegration/SettingsEngineChecks.cs')
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Settings checker compilation failed.'}
if(@(Get-ChildItem "$hostDir/BepInEx/plugins" -Recurse -Filter '*.dll').Count -ne 1){throw 'Unexpected plugin in settings integration host.'}
$executable=[IO.Path]::GetFullPath("$hostDir/Apocalypter-SettingsChecks.exe")
foreach($variant in ($variants.Keys | Sort-Object)){
 $report=Join-Path $hostDir "result-$variant.txt";$engineLog=Join-Path $hostDir "engine-$variant.log";$bepinexLog=Join-Path $hostDir 'BepInEx/LogOutput.log'
 foreach($file in @($report,$engineLog,$bepinexLog)){if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file -Force}}
 $started=[DateTime]::UtcNow
 $arguments="-batchmode -nographics -logFile engine-$variant.log --settings-variant $variant"
 $process=Start-Process -FilePath $executable -WorkingDirectory $hostDir -ArgumentList $arguments -WindowStyle Hidden -PassThru
 try{
  if($variant -eq 'baseline'){
   if(-not $process.WaitForExit(45000)){throw 'Settings baseline timed out.'}
   if($process.ExitCode -ne 0){throw "Settings baseline exited with $($process.ExitCode)."}
   $result=Get-Content -LiteralPath $report -Raw
   if(-not $result.StartsWith('FAIL:') -or $result -notmatch 'production maintenance installs native weapon foldouts'){throw 'Baseline did not reproduce the missing foldouts assertion.'}
   Write-Output 'Expected negative control: original production maintenance does not install native weapon foldouts.'
  }else{Write-Output (Wait-NativeIntegrationResult -Process $process -ExecutablePath $executable -StartedUtc $started -ReportPath $report -LogPaths @($engineLog,$bepinexLog) -Label 'Apocasetter settings integration')}
 }finally{if(-not $process.HasExited){Stop-Process -Id $process.Id};if(Test-Path -LiteralPath $bepinexLog){Copy-Item -LiteralPath $bepinexLog -Destination "$hostDir/bepinex-$variant.log" -Force};$process.Dispose()}
 if($variant -eq 'fixed' -and (Select-String -LiteralPath "$hostDir/bepinex-$variant.log" -Pattern '\[Error|\[Fatal')){throw 'Unexpected settings integration error; inspect logs.'}
}
