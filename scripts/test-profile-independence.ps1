param(
 [Parameter(Mandatory=$true)][string]$GameDir,
 [Parameter(Mandatory=$true)][string]$CandidateDll,
 [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunName=('run-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$game=(Resolve-Path -LiteralPath $GameDir).Path
$candidate=(Resolve-Path -LiteralPath $CandidateDll).Path
$hostDir=Join-Path $repo "artifacts/profile-integration-host/$RunName"
$managedProbe=Join-Path $hostDir 'Apocalypter-ProfileChecks_Data/Managed/UnityEngine.RuntimeInitializeOnLoadManagerInitializerModule.dll'
if($managedProbe.Length -ge 260){throw "Windows MAX_PATH: profile-host managed assembly path is $($managedProbe.Length) characters (must be below 260). Use a shorter RunName or checkout path: $managedProbe"}
if(Test-Path -LiteralPath $hostDir){throw 'Profile test run already exists; choose a new RunName to retain earlier evidence.'}
New-Item -ItemType Directory -Path "$hostDir/BepInEx/plugins/ProfileChecks","$hostDir/lib/candidate" -Force | Out-Null
Copy-Item -LiteralPath "$game/Apocalypter.exe" -Destination "$hostDir/Apocalypter-ProfileChecks.exe"
Copy-Item -LiteralPath "$game/UnityPlayer.dll","$game/winhttp.dll","$game/doorstop_config.ini" -Destination $hostDir
Copy-Item -LiteralPath "$game/BepInEx/core" -Destination "$hostDir/BepInEx" -Recurse
Copy-Item -LiteralPath $candidate -Destination "$hostDir/lib/candidate/ChainsawPrototype.dll"
$candidateProfiles=Join-Path (Split-Path -Parent $candidate) 'Defaults'
if(-not(Test-Path -LiteralPath $candidateProfiles -PathType Container)){throw 'Candidate must include its own bundled Defaults directory.'}
Copy-Item -LiteralPath $candidateProfiles -Destination "$hostDir/lib/candidate/Defaults" -Recurse
# A profile reader must not rewrite release-owned data. Shared authoring config
# changes happen only under this new host's own BepInEx/config directory.
Get-ChildItem -LiteralPath "$hostDir/lib/candidate/Defaults" -Recurse -File | ForEach-Object {$_.IsReadOnly=$true}
foreach($pair in @(@('Apocalypter-ProfileChecks_Data','Apocalypter_Data'),@('MonoBleedingEdge','MonoBleedingEdge'))){
 New-Item -ItemType Junction -Path (Join-Path $hostDir $pair[0]) -Target (Join-Path $game $pair[1]) | Out-Null
}
$compilerArgs=@('/nologo','/target:library',"/out:$hostDir/BepInEx/plugins/ProfileChecks/ProfileChecks.dll","/reference:$game/BepInEx/core/BepInEx.dll",'/reference:System.Runtime.Serialization.dll')
foreach($name in @('UnityEngine','UnityEngine.CoreModule','PlayMaker','netstandard')){$compilerArgs+="/reference:$game/Apocalypter_Data/Managed/$name.dll"}
$compilerArgs+=(Join-Path $repo 'tests/ProfileIndependence/ProfileEngineChecks.cs')
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Profile independence checker compilation failed.'}
if(@(Get-ChildItem -LiteralPath "$hostDir/BepInEx/plugins" -Filter '*.dll' -Recurse).Count -ne 1){throw 'Unexpected plugin in profile integration host.'}
. (Join-Path $PSScriptRoot 'native-integration-result.ps1')
$executable=[IO.Path]::GetFullPath("$hostDir/Apocalypter-ProfileChecks.exe")
$report=Join-Path $hostDir 'result.txt';$engineLog=Join-Path $hostDir 'engine.log';$bepinexLog=Join-Path $hostDir 'BepInEx/LogOutput.log'
$started=[DateTime]::UtcNow
$process=Start-Process -FilePath $executable -WorkingDirectory $hostDir -ArgumentList '-batchmode -force-d3d11 -logFile engine.log' -WindowStyle Hidden -PassThru
try{
 $result=Wait-NativeIntegrationResult -Process $process -ExecutablePath $executable -StartedUtc $started -ReportPath $report -LogPaths @($engineLog,$bepinexLog) -Label 'Profile independence'
 if(Select-String -LiteralPath $bepinexLog -Pattern '\[Error|\[Fatal'){throw 'Unexpected profile fixture error. Inspect retained host logs.'}
 Write-Output $result
}finally{
 if(Test-Path -LiteralPath $bepinexLog){Copy-Item -LiteralPath $bepinexLog -Destination (Join-Path $hostDir 'bepinex.log')}
 $process.Dispose()
}
Write-Output "Profile fixture evidence retained in $hostDir. Full clean-package gameplay remains unverified."
