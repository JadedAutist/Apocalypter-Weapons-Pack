param(
 [Parameter(Mandatory=$true)][string]$GameDir,
 [Parameter(Mandatory=$true)][string]$WildWastelandDll,
 [string]$CandidateDll,
 [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunName='candidate'
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'native-integration-result.ps1')
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$game=(Resolve-Path -LiteralPath $GameDir).Path
$hostDir=Join-Path $repo "artifacts/hud-icons-$RunName"
if(-not $CandidateDll){$CandidateDll=Join-Path $repo 'artifacts/runtime/BepInEx/plugins/Apocalypter Weapons Pack/ChainsawPrototype.dll'}
New-Item -ItemType Directory -Path "$hostDir/BepInEx/plugins/HudIconChecks","$hostDir/lib" -Force | Out-Null
Copy-Item -LiteralPath "$game/Apocalypter.exe" -Destination "$hostDir/Apocalypter-HudIconChecks.exe" -Force
Copy-Item -LiteralPath "$game/UnityPlayer.dll","$game/winhttp.dll","$game/doorstop_config.ini" -Destination $hostDir -Force
Copy-Item -LiteralPath "$game/BepInEx/core" -Destination "$hostDir/BepInEx" -Recurse -Force
Copy-Item -LiteralPath (Resolve-Path -LiteralPath $WildWastelandDll).Path -Destination "$hostDir/lib/WildWasteland.dll" -Force
Copy-Item -LiteralPath (Resolve-Path -LiteralPath $CandidateDll).Path -Destination "$hostDir/lib/ChainsawPrototype.dll" -Force
foreach($pair in @(@('Apocalypter-HudIconChecks_Data','Apocalypter_Data'),@('MonoBleedingEdge','MonoBleedingEdge'))){
 $destination=Join-Path $hostDir $pair[0];$target=Join-Path $game $pair[1]
 if(-not(Test-Path -LiteralPath $destination)){New-Item -ItemType Junction -Path $destination -Target $target | Out-Null}
 elseif((Get-Item -LiteralPath $destination).Target -ne $target){throw 'Unexpected HUD integration host junction.'}
}
$compilerArgs=@('/nologo','/target:library',"/out:$hostDir/BepInEx/plugins/HudIconChecks/HudIconChecks.dll","/reference:$game/BepInEx/core/BepInEx.dll","/reference:$game/BepInEx/core/0Harmony.dll")
foreach($name in @('UnityEngine','UnityEngine.CoreModule','UnityEngine.ImageConversionModule','netstandard')){$compilerArgs+="/reference:$game/Apocalypter_Data/Managed/$name.dll"}
$compilerArgs+=(Join-Path $repo 'tests/HudIntegration/HudIconChecks.cs')
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' @compilerArgs
if($LASTEXITCODE -ne 0){throw 'HUD icon checker compilation failed.'}
if(@(Get-ChildItem "$hostDir/BepInEx/plugins" -Filter '*.dll' -Recurse).Count -ne 1){throw 'Unexpected plugin in HUD icon integration host.'}
$report=Join-Path $hostDir 'result.txt';$engine=Join-Path $hostDir 'engine.log';$bepinex=Join-Path $hostDir 'BepInEx/LogOutput.log'
foreach($output in @($report,$engine,$bepinex)){if(Test-Path -LiteralPath $output){Remove-Item -LiteralPath $output -Force}}
$executable=[IO.Path]::GetFullPath("$hostDir/Apocalypter-HudIconChecks.exe")
$arguments="-batchmode -nographics -logFile `"$engine`" --hud-pack `"$hostDir/lib/ChainsawPrototype.dll`" --hud-report `"$report`" --hud-wild `"$hostDir/lib/WildWasteland.dll`" --hud-assets `"$repo/assets/runtime`""
$started=[DateTime]::UtcNow
$process=Start-Process -FilePath $executable -WorkingDirectory $hostDir -ArgumentList $arguments -WindowStyle Hidden -PassThru
try{$result=Wait-NativeIntegrationResult -Process $process -ExecutablePath $executable -StartedUtc $started -ReportPath $report -LogPaths @($engine,$bepinex) -Label 'HUD icon integration'}finally{$process.Dispose()}
Write-Output $result
if(Select-String -LiteralPath $bepinex -Pattern '\[Error|\[Fatal|Could not find type named'){throw 'Unexpected HUD integration error or missing-type warning.'}
Get-FileHash -LiteralPath "$hostDir/lib/ChainsawPrototype.dll","$hostDir/lib/WildWasteland.dll"
Write-Output 'Native HUD icon routing passed against the supplied companion DLL. Visual quickslot gameplay remains a separate check.'
