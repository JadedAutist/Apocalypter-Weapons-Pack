param([Parameter(Mandatory=$true)][string]$GameDir,[string]$CandidateDll,[string]$RunName='candidate',[string]$WildWastelandDll,[string]$ModelFixture)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'native-integration-result.ps1')
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$game=(Resolve-Path -LiteralPath $GameDir).Path
if($RunName -notmatch '^[A-Za-z0-9_-]+$'){throw 'Use a simple run name.'}
$hostDir=Join-Path $repo "artifacts/flame-ignite-$RunName"
if(-not $CandidateDll){$CandidateDll=Join-Path $repo 'artifacts/runtime/BepInEx/plugins/Apocalypter Weapons Pack/ChainsawPrototype.dll'}
New-Item -ItemType Directory -Path "$hostDir/BepInEx/plugins/IgniteChecks","$hostDir/lib" -Force | Out-Null
Copy-Item -LiteralPath "$game/Apocalypter.exe" -Destination "$hostDir/Apocalypter-IgniteChecks.exe" -Force
Copy-Item -LiteralPath "$game/UnityPlayer.dll","$game/winhttp.dll","$game/doorstop_config.ini" -Destination $hostDir -Force
Copy-Item -LiteralPath "$game/BepInEx/core" -Destination "$hostDir/BepInEx" -Recurse -Force
Copy-Item -LiteralPath $CandidateDll -Destination "$hostDir/lib/ChainsawPrototype.dll" -Force
$atlas=Join-Path (Split-Path $CandidateDll) 'Flamethrower/NativeEffects/Flame1.png'
if(-not(Test-Path -LiteralPath $atlas)){throw 'Candidate native flame atlas missing.'}
New-Item -ItemType Directory -Path "$hostDir/lib/Flamethrower/NativeEffects" -Force|Out-Null
Copy-Item -LiteralPath $atlas -Destination "$hostDir/lib/Flamethrower/NativeEffects/Flame1.png" -Force
$smokeTexture=Join-Path (Split-Path $CandidateDll) 'Flamethrower/NativeEffects/Scorch.png'
if(-not(Test-Path -LiteralPath $smokeTexture)){throw 'Candidate smoke mask missing.'}
Copy-Item -LiteralPath $smokeTexture -Destination "$hostDir/lib/Flamethrower/NativeEffects/Scorch.png" -Force
$skinTexture=Join-Path (Split-Path $CandidateDll) 'Flamethrower/BurntSkin.png'
if(-not(Test-Path -LiteralPath $skinTexture)){throw 'Candidate burnt skin missing.'}
Copy-Item -LiteralPath $skinTexture -Destination "$hostDir/lib/Flamethrower/BurntSkin.png" -Force
$modelFixtureCopy=Join-Path $hostDir 'lib/model-coverage.json'
if($ModelFixture){Copy-Item -LiteralPath $ModelFixture -Destination $modelFixtureCopy -Force}
elseif(Test-Path -LiteralPath $modelFixtureCopy){Remove-Item -LiteralPath $modelFixtureCopy -Force}
$optionalWild=Join-Path $hostDir 'lib/WildWasteland.dll'
if($WildWastelandDll){Copy-Item -LiteralPath $WildWastelandDll -Destination $optionalWild -Force}
elseif(Test-Path -LiteralPath $optionalWild){Remove-Item -LiteralPath $optionalWild -Force}
$identity=@{candidateSha256=(Get-FileHash -LiteralPath $CandidateDll).Hash;flameAtlasSha256=(Get-FileHash -LiteralPath $atlas).Hash;smokeMaskSha256=(Get-FileHash -LiteralPath $smokeTexture).Hash;burntSkinSha256=(Get-FileHash -LiteralPath $skinTexture).Hash;wildWastelandSha256=$null}
if($WildWastelandDll){$identity.wildWastelandSha256=(Get-FileHash -LiteralPath $WildWastelandDll).Hash}
$identity.modelFixtureSha256=if($ModelFixture){(Get-FileHash -LiteralPath $ModelFixture).Hash}else{$null}
$identity | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $hostDir 'identity.json')
foreach($pair in @(@('Apocalypter-IgniteChecks_Data','Apocalypter_Data'),@('MonoBleedingEdge','MonoBleedingEdge'))){
 $destination=Join-Path $hostDir $pair[0];$target=Join-Path $game $pair[1]
 if(-not(Test-Path -LiteralPath $destination)){New-Item -ItemType Junction -Path $destination -Target $target | Out-Null}
 elseif((Get-Item -LiteralPath $destination).Target -ne $target){throw 'Unexpected flame-ignite-host junction.'}
}
$compilerArgs=@('/nologo','/target:library',"/out:$hostDir/BepInEx/plugins/IgniteChecks/IgniteChecks.dll","/reference:$game/BepInEx/core/BepInEx.dll","/reference:$game/BepInEx/core/0Harmony.dll")
foreach($name in @('UnityEngine','UnityEngine.CoreModule','UnityEngine.ImageConversionModule','UnityEngine.PhysicsModule','UnityEngine.AudioModule','UnityEngine.ParticleSystemModule','PlayMaker','Assembly-CSharp','Assembly-CSharp-firstpass','netstandard')){$compilerArgs+="/reference:$game/Apocalypter_Data/Managed/$name.dll"}
$compilerArgs+=@('/reference:System.Runtime.Serialization.dll',(Join-Path $repo 'tests/FlameIgnite/IgniteChecks.cs'),(Join-Path $repo 'tests/FlameIgnite/ModelCoverageChecks.cs'),(Join-Path $repo 'tests/FlameIgnite/CustomSkinChecks.cs'))
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Flame ignite checker compilation failed.'}
if(@(Get-ChildItem "$hostDir/BepInEx/plugins" -Filter '*.dll' -Recurse).Count -ne 1){throw 'Unexpected plugin in flame ignite host.'}
$executable=[IO.Path]::GetFullPath("$hostDir/Apocalypter-IgniteChecks.exe")
$report=Join-Path $hostDir 'result.txt'
$engineLog=Join-Path $hostDir 'engine.log';$bepinexLog=Join-Path $hostDir 'BepInEx/LogOutput.log'
foreach($output in @($report,$engineLog,$bepinexLog,(Join-Path $hostDir 'timings.txt'))){if(Test-Path -LiteralPath $output){Remove-Item -LiteralPath $output -Force}}
$started=[DateTime]::UtcNow
$process=Start-Process -FilePath $executable -WorkingDirectory $hostDir -ArgumentList '-batchmode -logFile engine.log' -WindowStyle Hidden -PassThru
try{
 $result=Wait-NativeIntegrationResult -Process $process -ExecutablePath $executable -StartedUtc $started -ReportPath $report -LogPaths @($engineLog,$bepinexLog) -Label 'Flame ignite checks'
}finally{
 if(Test-Path -LiteralPath $bepinexLog){Copy-Item -LiteralPath $bepinexLog -Destination "$hostDir/bepinex-result.log" -Force}
 $process.Dispose()
}
Write-Output $result
if(Select-String -LiteralPath "$hostDir/bepinex-result.log" -Pattern '\[Error|\[Fatal|Could not find type named'){throw 'Unexpected flame ignite host error.'}









