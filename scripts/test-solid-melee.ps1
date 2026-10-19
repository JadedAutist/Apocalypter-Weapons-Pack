param([Parameter(Mandatory=$true)][string]$GameDir,[string]$CandidateDll,[string]$RunName='candidate',[string]$BloodFxDll,[string]$BloodFxConfig,[string]$WildWastelandDll)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'native-integration-result.ps1')
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$game=(Resolve-Path -LiteralPath $GameDir).Path
if($RunName -notmatch '^[A-Za-z0-9_-]+$'){throw 'Use a simple run name.'}
$hostDir=Join-Path $repo "artifacts/solid-melee-$RunName"
if(-not $CandidateDll){$CandidateDll=Join-Path $repo 'artifacts/runtime/BepInEx/plugins/Apocalypter Weapons Pack/ChainsawPrototype.dll'}
New-Item -ItemType Directory -Path "$hostDir/BepInEx/plugins/BloodChecks","$hostDir/lib" -Force | Out-Null
Copy-Item -LiteralPath "$game/Apocalypter.exe" -Destination "$hostDir/Apocalypter-BloodChecks.exe" -Force
Copy-Item -LiteralPath "$game/UnityPlayer.dll","$game/winhttp.dll","$game/doorstop_config.ini" -Destination $hostDir -Force
Copy-Item -LiteralPath "$game/BepInEx/core" -Destination "$hostDir/BepInEx" -Recurse -Force
Copy-Item -LiteralPath $CandidateDll -Destination "$hostDir/lib/ChainsawPrototype.dll" -Force
$optionalWild=Join-Path $hostDir 'lib/WildWasteland.dll'
if($WildWastelandDll){Copy-Item -LiteralPath $WildWastelandDll -Destination $optionalWild -Force}
elseif(Test-Path -LiteralPath $optionalWild){Remove-Item -LiteralPath $optionalWild -Force}
$optionalDll=Join-Path $hostDir 'lib/BloodFX.dll'
if($BloodFxDll){Copy-Item -LiteralPath $BloodFxDll -Destination $optionalDll -Force}
elseif(Test-Path -LiteralPath $optionalDll){Remove-Item -LiteralPath $optionalDll -Force}
$optionalConfig=Join-Path $hostDir 'BepInEx/config/com.apoca.bloodfx.cfg'
if($BloodFxConfig){New-Item -ItemType Directory -Path "$hostDir/BepInEx/config" -Force | Out-Null;Copy-Item -LiteralPath $BloodFxConfig -Destination $optionalConfig -Force}
elseif(Test-Path -LiteralPath $optionalConfig){Remove-Item -LiteralPath $optionalConfig -Force}
$identity=[ordered]@{candidateSha256=(Get-FileHash -LiteralPath $CandidateDll).Hash;bloodFxSha256=$null;bloodFxConfigSha256=$null;wildWastelandSha256=$null}
if($WildWastelandDll){$identity.wildWastelandSha256=(Get-FileHash -LiteralPath $WildWastelandDll).Hash}
if($BloodFxDll){$identity.bloodFxSha256=(Get-FileHash -LiteralPath $BloodFxDll).Hash}
if($BloodFxConfig){$identity.bloodFxConfigSha256=(Get-FileHash -LiteralPath $BloodFxConfig).Hash}
$identity | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $hostDir 'identity.json')
foreach($pair in @(@('Apocalypter-BloodChecks_Data','Apocalypter_Data'),@('MonoBleedingEdge','MonoBleedingEdge'))){
 $destination=Join-Path $hostDir $pair[0];$target=Join-Path $game $pair[1]
 if(-not(Test-Path -LiteralPath $destination)){New-Item -ItemType Junction -Path $destination -Target $target | Out-Null}
 elseif((Get-Item -LiteralPath $destination).Target -ne $target){throw 'Unexpected solid-melee-host junction.'}
}
$compilerArgs=@('/nologo','/target:library',"/out:$hostDir/BepInEx/plugins/BloodChecks/BloodChecks.dll","/reference:$game/BepInEx/core/BepInEx.dll")
foreach($name in @('UnityEngine','UnityEngine.CoreModule','UnityEngine.PhysicsModule','UnityEngine.AudioModule','UnityEngine.ParticleSystemModule','PlayMaker','Assembly-CSharp','Assembly-CSharp-firstpass','netstandard')){$compilerArgs+="/reference:$game/Apocalypter_Data/Managed/$name.dll"}
$compilerArgs+=(Join-Path $repo 'tests/SolidMelee/SolidMeleeChecks.cs')
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Solid melee checker compilation failed.'}
if(@(Get-ChildItem "$hostDir/BepInEx/plugins" -Filter '*.dll' -Recurse).Count -ne 1){throw 'Unexpected plugin in solid melee host.'}
$executable=[IO.Path]::GetFullPath("$hostDir/Apocalypter-BloodChecks.exe")
$report=Join-Path $hostDir 'result.txt'
$engineLog=Join-Path $hostDir 'engine.log';$bepinexLog=Join-Path $hostDir 'BepInEx/LogOutput.log'
foreach($output in @($report,$engineLog,$bepinexLog)){if(Test-Path -LiteralPath $output){Remove-Item -LiteralPath $output -Force}}
$started=[DateTime]::UtcNow
$process=Start-Process -FilePath $executable -WorkingDirectory $hostDir -ArgumentList '-batchmode -nographics -logFile engine.log' -WindowStyle Hidden -PassThru
try{
 $result=Wait-NativeIntegrationResult -Process $process -ExecutablePath $executable -StartedUtc $started -ReportPath $report -LogPaths @($engineLog,$bepinexLog) -Label 'Solid melee checks'
}finally{
 if(Test-Path -LiteralPath $bepinexLog){Copy-Item -LiteralPath $bepinexLog -Destination "$hostDir/bepinex-result.log" -Force}
 $process.Dispose()
}
Write-Output $result
if(Select-String -LiteralPath "$hostDir/bepinex-result.log" -Pattern '\[Error|\[Fatal|Could not find type named'){throw 'Unexpected solid melee host error.'}







