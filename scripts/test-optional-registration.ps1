param(
 [Parameter(Mandatory=$true)][string]$GameDir,
 [Parameter(Mandatory=$true)][string]$InspectorDll,
 [string]$CandidateDll,
 [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunName='candidate',
 [string]$ExtraOptionalType,
 [string]$ExtraRetryMethods,
 [string]$ExtraTemplateFields
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'native-integration-result.ps1')
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$game=(Resolve-Path -LiteralPath $GameDir).Path
$hostDir=Join-Path $repo "artifacts/optional-registration-$RunName"
if(-not $CandidateDll){$CandidateDll=Join-Path $repo 'artifacts/runtime/BepInEx/plugins/Apocalypter Weapons Pack/ChainsawPrototype.dll'}
New-Item -ItemType Directory -Path "$hostDir/BepInEx/plugins/RegistrationChecks","$hostDir/lib" -Force | Out-Null
Copy-Item -LiteralPath "$game/Apocalypter.exe" -Destination "$hostDir/Apocalypter-RegistrationChecks.exe" -Force
Copy-Item -LiteralPath "$game/UnityPlayer.dll","$game/winhttp.dll","$game/doorstop_config.ini" -Destination $hostDir -Force
Copy-Item -LiteralPath "$game/BepInEx/core" -Destination "$hostDir/BepInEx" -Recurse -Force
Copy-Item -LiteralPath (Resolve-Path -LiteralPath $InspectorDll).Path -Destination "$hostDir/lib/ApocalypterInspector.dll" -Force
Copy-Item -LiteralPath (Resolve-Path -LiteralPath $CandidateDll).Path -Destination "$hostDir/lib/ChainsawPrototype.dll" -Force
foreach($pair in @(@('Apocalypter-RegistrationChecks_Data','Apocalypter_Data'),@('MonoBleedingEdge','MonoBleedingEdge'))){
 $destination=Join-Path $hostDir $pair[0];$target=Join-Path $game $pair[1]
 if(-not(Test-Path -LiteralPath $destination)){New-Item -ItemType Junction -Path $destination -Target $target | Out-Null}
 elseif((Get-Item -LiteralPath $destination).Target -ne $target){throw 'Unexpected registration-host junction.'}
}
$compilerArgs=@('/nologo','/target:library',"/out:$hostDir/BepInEx/plugins/RegistrationChecks/RegistrationChecks.dll","/reference:$game/BepInEx/core/BepInEx.dll")
foreach($name in @('UnityEngine','UnityEngine.CoreModule','netstandard')){$compilerArgs+="/reference:$game/Apocalypter_Data/Managed/$name.dll"}
$compilerArgs+=(Join-Path $repo 'tests/OptionalIntegration/OptionalRegistrationChecks.cs')
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Registration checker compilation failed.'}
if(@(Get-ChildItem "$hostDir/BepInEx/plugins" -Filter '*.dll' -Recurse).Count -ne 1){throw 'Unexpected plugin in registration host.'}
$report=Join-Path $hostDir 'result.txt'
$engine=Join-Path $hostDir 'engine.log'
$executable=[IO.Path]::GetFullPath("$hostDir/Apocalypter-RegistrationChecks.exe")
$arguments="-batchmode -nographics -logFile `"$engine`" --registration-pack `"$hostDir/lib/ChainsawPrototype.dll`" --registration-report `"$report`" --registration-inspector `"$hostDir/lib/ApocalypterInspector.dll`""
foreach($option in @(@('--registration-extra-type',$ExtraOptionalType),@('--registration-extra-methods',$ExtraRetryMethods),@('--registration-extra-template-fields',$ExtraTemplateFields))){
 if($option[1]){if($option[1].Contains('"')){throw 'Double quotes are not accepted in extra test arguments.'};$arguments+=" $($option[0]) `"$($option[1])`""}
}
$started=[DateTime]::UtcNow
$process=Start-Process -FilePath $executable -WorkingDirectory $hostDir -ArgumentList $arguments -WindowStyle Hidden -PassThru
$result=Wait-NativeIntegrationResult -Process $process -ExecutablePath $executable -StartedUtc $started -ReportPath $report -LogPaths @($engine,"$hostDir/BepInEx/LogOutput.log") -Label 'Optional registration checks'
Write-Output $result
if(Select-String -LiteralPath "$hostDir/BepInEx/LogOutput.log" -Pattern '\[Error|\[Fatal|Could not find type named'){throw 'Unexpected registration-host error or missing-type warning.'}
Write-Output 'Optional registration retries are quiet and late Inspector type loading works. Full gameplay remains a separate check.'
