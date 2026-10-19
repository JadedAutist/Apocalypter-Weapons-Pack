param([Parameter(Mandatory=$true)][string]$GameDir,[string]$CandidateDll,[string]$RunName='candidate')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'native-integration-result.ps1')
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$game=(Resolve-Path -LiteralPath $GameDir).Path
if($RunName -notmatch '^[A-Za-z0-9_-]+$'){throw 'Use a simple run name.'}
$hostDir=Join-Path $repo "artifacts/flame-windows-$RunName"
if(-not $CandidateDll){$CandidateDll=Join-Path $repo 'artifacts/runtime/BepInEx/plugins/Apocalypter Weapons Pack/ChainsawPrototype.dll'}
New-Item -ItemType Directory -Path "$hostDir/BepInEx/plugins/FlameChecks","$hostDir/lib" -Force | Out-Null
Copy-Item -LiteralPath "$game/Apocalypter.exe" -Destination "$hostDir/Apocalypter-FlameChecks.exe" -Force
Copy-Item -LiteralPath "$game/UnityPlayer.dll","$game/winhttp.dll","$game/doorstop_config.ini" -Destination $hostDir -Force
Copy-Item -LiteralPath "$game/BepInEx/core" -Destination "$hostDir/BepInEx" -Recurse -Force
Copy-Item -LiteralPath $CandidateDll -Destination "$hostDir/lib/ChainsawPrototype.dll" -Force
foreach($pair in @(@('Apocalypter-FlameChecks_Data','Apocalypter_Data'),@('MonoBleedingEdge','MonoBleedingEdge'))){
 $destination=Join-Path $hostDir $pair[0];$target=Join-Path $game $pair[1]
 if(-not(Test-Path -LiteralPath $destination)){New-Item -ItemType Junction -Path $destination -Target $target | Out-Null}
 elseif((Get-Item -LiteralPath $destination).Target -ne $target){throw 'Unexpected flame-host junction.'}
}
$compilerArgs=@('/nologo','/target:library',"/out:$hostDir/BepInEx/plugins/FlameChecks/FlameChecks.dll","/reference:$game/BepInEx/core/BepInEx.dll")
foreach($name in @('UnityEngine','UnityEngine.CoreModule','UnityEngine.PhysicsModule','PlayMaker','netstandard')){$compilerArgs+="/reference:$game/Apocalypter_Data/Managed/$name.dll"}
$compilerArgs+=(Join-Path $repo 'tests/FlameWindows/FlameWindowChecks.cs')
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Flame checker compilation failed.'}
if(@(Get-ChildItem "$hostDir/BepInEx/plugins" -Filter '*.dll' -Recurse).Count -ne 1){throw 'Unexpected plugin in flame host.'}
$executable=[IO.Path]::GetFullPath("$hostDir/Apocalypter-FlameChecks.exe")
$report=Join-Path $hostDir 'result.txt'
$engineLog=Join-Path $hostDir 'engine.log';$bepinexLog=Join-Path $hostDir 'BepInEx/LogOutput.log'
foreach($output in @($report,$engineLog,$bepinexLog)){if(Test-Path -LiteralPath $output){Remove-Item -LiteralPath $output -Force}}
$started=[DateTime]::UtcNow
$process=Start-Process -FilePath $executable -WorkingDirectory $hostDir -ArgumentList '-batchmode -nographics -logFile engine.log' -WindowStyle Hidden -PassThru
try{
 $result=Wait-NativeIntegrationResult -Process $process -ExecutablePath $executable -StartedUtc $started -ReportPath $report -LogPaths @($engineLog,$bepinexLog) -Label 'Flame window checks'
}finally{
 if(Test-Path -LiteralPath $bepinexLog){Copy-Item -LiteralPath $bepinexLog -Destination "$hostDir/bepinex-result.log" -Force}
 $process.Dispose()
}
Write-Output $result
if(Select-String -LiteralPath "$hostDir/bepinex-result.log" -Pattern '\[Error|\[Fatal|Could not find type named'){throw 'Unexpected flame host error.'}
