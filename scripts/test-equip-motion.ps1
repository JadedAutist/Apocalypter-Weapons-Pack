param([Parameter(Mandatory=$true)][string]$GameDir,[string]$CandidateDll,[string]$RunName='candidate',[switch]$CompileOnly,[string]$WildWastelandDll)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'native-integration-result.ps1')
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$game=(Resolve-Path -LiteralPath $GameDir).Path
if($RunName -notmatch '^[A-Za-z0-9_-]+$'){throw 'Use a simple run name.'}
$hostDir=Join-Path $repo "artifacts/equip-motion-$RunName"
if(-not $CandidateDll){$CandidateDll=Join-Path $repo 'artifacts/runtime/BepInEx/plugins/Apocalypter Weapons Pack/ChainsawPrototype.dll'}
New-Item -ItemType Directory -Path "$hostDir/BepInEx/plugins/EquipChecks","$hostDir/lib" -Force | Out-Null
Copy-Item -LiteralPath "$game/Apocalypter.exe" -Destination "$hostDir/Apocalypter-EquipChecks.exe" -Force
Copy-Item -LiteralPath "$game/UnityPlayer.dll","$game/winhttp.dll","$game/doorstop_config.ini" -Destination $hostDir -Force
Copy-Item -LiteralPath "$game/BepInEx/core" -Destination "$hostDir/BepInEx" -Recurse -Force
Copy-Item -LiteralPath $CandidateDll -Destination "$hostDir/lib/ChainsawPrototype.dll" -Force
@{candidateSha256=(Get-FileHash -LiteralPath $CandidateDll).Hash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $hostDir 'identity.json')
if($WildWastelandDll){Copy-Item -LiteralPath $WildWastelandDll -Destination "$hostDir/lib/WildWasteland.dll" -Force}
elseif(Test-Path -LiteralPath "$hostDir/lib/WildWasteland.dll"){Remove-Item -LiteralPath "$hostDir/lib/WildWasteland.dll" -Force}
Copy-Item -LiteralPath (Join-Path (Split-Path $CandidateDll) 'ChainsawPlacement.json') -Destination "$hostDir/lib/ChainsawPlacement.json" -Force
Copy-Item -LiteralPath (Join-Path (Split-Path $CandidateDll) 'Defaults') -Destination "$hostDir/lib" -Recurse -Force
$skin=Join-Path (Split-Path $CandidateDll) 'Flamethrower/BurntSkin.png'
if(Test-Path -LiteralPath $skin){New-Item -ItemType Directory -Path "$hostDir/lib/Flamethrower" -Force | Out-Null;Copy-Item -LiteralPath $skin -Destination "$hostDir/lib/Flamethrower/BurntSkin.png" -Force}
foreach($pair in @(@('Apocalypter-EquipChecks_Data','Apocalypter_Data'),@('MonoBleedingEdge','MonoBleedingEdge'))){
 $destination=Join-Path $hostDir $pair[0];$target=Join-Path $game $pair[1]
 if(-not(Test-Path -LiteralPath $destination)){New-Item -ItemType Junction -Path $destination -Target $target | Out-Null}
 elseif((Get-Item -LiteralPath $destination).Target -ne $target){throw 'Unexpected equip-motion-host junction.'}
}
$compilerArgs=@('/nologo','/target:library',"/out:$hostDir/BepInEx/plugins/EquipChecks/EquipChecks.dll","/reference:$game/BepInEx/core/BepInEx.dll","/reference:$game/BepInEx/core/0Harmony.dll","/reference:System.Runtime.Serialization.dll")
foreach($name in @('UnityEngine','UnityEngine.CoreModule','UnityEngine.PhysicsModule','PlayMaker','Assembly-CSharp','Assembly-CSharp-firstpass','netstandard')){$compilerArgs+="/reference:$game/Apocalypter_Data/Managed/$name.dll"}
$compilerArgs+=(Join-Path $repo 'tests/EquipMotion/EquipChecks.cs')
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Equip motion checker compilation failed.'}
if(@(Get-ChildItem "$hostDir/BepInEx/plugins" -Filter '*.dll' -Recurse).Count -ne 1){throw 'Unexpected plugin in equip motion host.'}
if($CompileOnly){Write-Output "Compiled equip motion checks at $hostDir";return}
$executable=[IO.Path]::GetFullPath("$hostDir/Apocalypter-EquipChecks.exe")
$report=Join-Path $hostDir 'result.txt'
$engineLog=Join-Path $hostDir 'engine.log';$bepinexLog=Join-Path $hostDir 'BepInEx/LogOutput.log'
foreach($output in @($report,$engineLog,$bepinexLog)){if(Test-Path -LiteralPath $output){Remove-Item -LiteralPath $output -Force}}
$started=[DateTime]::UtcNow
$process=Start-Process -FilePath $executable -WorkingDirectory $hostDir -ArgumentList '-batchmode -logFile engine.log' -WindowStyle Hidden -PassThru
try{$result=Wait-NativeIntegrationResult -Process $process -ExecutablePath $executable -StartedUtc $started -ReportPath $report -LogPaths @($engineLog,$bepinexLog) -Label 'Equip motion checks'}
finally{if(Test-Path -LiteralPath $bepinexLog){Copy-Item -LiteralPath $bepinexLog -Destination "$hostDir/bepinex-result.log" -Force};$process.Dispose()}
Write-Output $result
if(Select-String -LiteralPath "$hostDir/bepinex-result.log" -Pattern '\[Error|\[Fatal|Could not find type named'){throw 'Unexpected equip motion host error.'}
