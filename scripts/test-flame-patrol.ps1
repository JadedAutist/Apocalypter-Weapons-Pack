param([Parameter(Mandatory=$true)][string]$GameDir,[string]$CandidateDll,[string]$RunName='candidate',[switch]$WithWildWasteland,[string]$WildWastelandDll)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'native-integration-result.ps1')
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$game=(Resolve-Path -LiteralPath $GameDir).Path
if($RunName -notmatch '^[A-Za-z0-9_-]{1,24}$'){throw 'Use a simple run name of at most 24 characters.'}
$hostDir=Join-Path $repo "artifacts/fp-$RunName"
if(Test-Path -LiteralPath $hostDir){throw "Preserve previous evidence: choose a fresh run name; $hostDir exists."}
if(-not $CandidateDll){$CandidateDll=Join-Path $repo 'artifacts/runtime/BepInEx/plugins/Apocalypter Weapons Pack/ChainsawPrototype.dll'}
$CandidateDll=(Resolve-Path -LiteralPath $CandidateDll).Path
if($WithWildWasteland -and -not $WildWastelandDll){$WildWastelandDll=Join-Path $game 'BepInEx/plugins/WildWasteland/WildWasteland.dll'}
if($WithWildWasteland){$WildWastelandDll=(Resolve-Path -LiteralPath $WildWastelandDll).Path}
New-Item -ItemType Directory -Path "$hostDir/BepInEx/plugins/FlameChecks","$hostDir/lib" -Force | Out-Null
Copy-Item -LiteralPath "$game/Apocalypter.exe" -Destination "$hostDir/Apocalypter-FP.exe" -Force
Copy-Item -LiteralPath "$game/UnityPlayer.dll","$game/winhttp.dll","$game/doorstop_config.ini" -Destination $hostDir -Force
Copy-Item -LiteralPath "$game/BepInEx/core" -Destination "$hostDir/BepInEx" -Recurse -Force
Copy-Item -LiteralPath $CandidateDll -Destination "$hostDir/lib/ChainsawPrototype.dll" -Force
if($WithWildWasteland){Copy-Item -LiteralPath $WildWastelandDll -Destination "$hostDir/lib/WildWasteland.dll" -Force}
foreach($pair in @(@('Apocalypter-FP_Data','Apocalypter_Data'),@('MonoBleedingEdge','MonoBleedingEdge'))){$destination=Join-Path $hostDir $pair[0];$target=Join-Path $game $pair[1];New-Item -ItemType Junction -Path $destination -Target $target | Out-Null}
$compilerArgs=@('/nologo','/target:library',"/out:$hostDir/BepInEx/plugins/FlameChecks/FlameChecks.dll","/reference:$game/BepInEx/core/BepInEx.dll")
foreach($name in @('UnityEngine','UnityEngine.CoreModule','UnityEngine.PhysicsModule','Assembly-CSharp','Assembly-CSharp-firstpass','PlayMaker','netstandard')){$compilerArgs+="/reference:$game/Apocalypter_Data/Managed/$name.dll"}
$compilerArgs+=(Join-Path $repo 'tests/FlamePatrol/FlamePatrolChecks.cs')
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Flame patrol checker compilation failed.'}
if(@(Get-ChildItem "$hostDir/BepInEx/plugins" -Filter '*.dll' -Recurse).Count -ne 1){throw 'Unexpected plugin in flame patrol host.'}
$identity=@{candidate=(Get-FileHash -LiteralPath "$hostDir/lib/ChainsawPrototype.dll" -Algorithm SHA256).Hash;withWildWasteland=[bool]$WithWildWasteland}
if($WithWildWasteland){$identity.wildWasteland=(Get-FileHash -LiteralPath "$hostDir/lib/WildWasteland.dll" -Algorithm SHA256).Hash}
$identity | ConvertTo-Json | Set-Content -LiteralPath "$hostDir/identity.json" -Encoding UTF8
$executable=[IO.Path]::GetFullPath("$hostDir/Apocalypter-FP.exe");$report=Join-Path $hostDir 'result.txt';$engineLog=Join-Path $hostDir 'engine.log';$bepinexLog=Join-Path $hostDir 'BepInEx/LogOutput.log'
$started=[DateTime]::UtcNow;$arguments='-batchmode -nographics -logFile engine.log';if($WithWildWasteland){$arguments+=' --with-patrol'}
$process=Start-Process -FilePath $executable -WorkingDirectory $hostDir -ArgumentList $arguments -WindowStyle Hidden -PassThru
try{$result=Wait-NativeIntegrationResult -Process $process -ExecutablePath $executable -StartedUtc $started -ReportPath $report -LogPaths @($engineLog,$bepinexLog) -Label 'Flame patrol checks'}finally{if(Test-Path -LiteralPath $bepinexLog){Copy-Item -LiteralPath $bepinexLog -Destination "$hostDir/bepinex-result.log" -Force};$process.Dispose()}
Write-Output $result
if(Select-String -LiteralPath "$hostDir/bepinex-result.log" -Pattern '\[Error|\[Fatal|Could not find type named'){throw 'Unexpected flame patrol host error.'}
