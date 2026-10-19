param(
 [Parameter(Mandatory=$true)][string]$GameDir,
 [Parameter(Mandatory=$true)][string]$PlayerBodyDll,
 [Parameter(Mandatory=$true)][string]$WildWastelandDll,
 [string]$CandidateDll,
 [string]$BaselineZip,
 [string]$DevelopmentDll
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'native-integration-result.ps1')
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$game=(Resolve-Path -LiteralPath $GameDir).Path
$hostDir=Join-Path $repo 'artifacts/optional-integration-host'
$variants=@{public=(Join-Path $repo 'artifacts/runtime/BepInEx/plugins/Apocalypter Weapons Pack/ChainsawPrototype.dll')}
if($CandidateDll){$variants.public=(Resolve-Path -LiteralPath $CandidateDll).Path}
if($DevelopmentDll){$variants.development=(Resolve-Path -LiteralPath $DevelopmentDll).Path}
New-Item -ItemType Directory -Path "$hostDir/BepInEx/plugins/OptionalChecks","$hostDir/lib" -Force | Out-Null
if($BaselineZip){
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $archive=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $BaselineZip).Path)
 try{
  $entry=$archive.GetEntry('BepInEx/plugins/Apocalypter Weapons Pack/ChainsawPrototype.dll')
  if(-not $entry){throw 'Baseline ZIP does not contain the plugin path.'}
  New-Item -ItemType Directory -Path "$hostDir/lib/baseline" -Force | Out-Null
  [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,"$hostDir/lib/baseline/ChainsawPrototype.dll",$true)
 }finally{$archive.Dispose()}
 $variants.baseline="$hostDir/lib/baseline/ChainsawPrototype.dll"
}
Copy-Item -LiteralPath "$game/Apocalypter.exe" -Destination "$hostDir/Apocalypter-OptionalChecks.exe" -Force
Copy-Item -LiteralPath "$game/UnityPlayer.dll","$game/winhttp.dll","$game/doorstop_config.ini" -Destination $hostDir -Force
Copy-Item -LiteralPath "$game/BepInEx/core" -Destination "$hostDir/BepInEx" -Recurse -Force
Copy-Item -LiteralPath (Resolve-Path -LiteralPath $PlayerBodyDll).Path -Destination "$hostDir/lib/Apocaplayer.dll" -Force
Copy-Item -LiteralPath (Resolve-Path -LiteralPath $WildWastelandDll).Path -Destination "$hostDir/lib/WildWasteland.dll" -Force
foreach($variant in $variants.Keys){
 New-Item -ItemType Directory -Path "$hostDir/lib/$variant" -Force | Out-Null
 $destination=[IO.Path]::GetFullPath("$hostDir/lib/$variant/ChainsawPrototype.dll")
 if([IO.Path]::GetFullPath($variants[$variant]) -ne $destination){Copy-Item -LiteralPath $variants[$variant] -Destination $destination -Force}
}
foreach($pair in @(@('Apocalypter-OptionalChecks_Data','Apocalypter_Data'),@('MonoBleedingEdge','MonoBleedingEdge'))){
 $destination=Join-Path $hostDir $pair[0];$target=Join-Path $game $pair[1]
 if(-not(Test-Path -LiteralPath $destination)){New-Item -ItemType Junction -Path $destination -Target $target | Out-Null}
 elseif((Get-Item -LiteralPath $destination).Target -ne $target){throw 'Unexpected integration host junction.'}
}
$compilerArgs=@('/nologo','/target:library',"/out:$hostDir/BepInEx/plugins/OptionalChecks/OptionalChecks.dll","/reference:$game/BepInEx/core/BepInEx.dll")
foreach($name in @('UnityEngine','UnityEngine.CoreModule','netstandard')){$compilerArgs+="/reference:$game/Apocalypter_Data/Managed/$name.dll"}
$compilerArgs+=(Join-Path $repo 'tests/OptionalIntegration/OptionalIntegrationChecks.cs')
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Optional integration checker compilation failed.'}
if(@(Get-ChildItem "$hostDir/BepInEx/plugins" -Filter '*.dll' -Recurse).Count -ne 1){throw 'Unexpected plugin in integration host.'}
$executable=[IO.Path]::GetFullPath("$hostDir/Apocalypter-OptionalChecks.exe")
foreach($variant in ($variants.Keys | Sort-Object)){
 $report=Join-Path $hostDir "result-$variant.txt"
 $engineLog=Join-Path $hostDir "engine-$variant.log"
 $bepinexLog=Join-Path $hostDir 'BepInEx/LogOutput.log'
 foreach($output in @($report,$engineLog,$bepinexLog)){if(Test-Path -LiteralPath $output){Remove-Item -LiteralPath $output -Force}}
 $started=[DateTime]::UtcNow
 $process=Start-Process -FilePath $executable -WorkingDirectory $hostDir -ArgumentList "-batchmode -nographics -logFile engine-$variant.log --optional-variant $variant" -WindowStyle Hidden -PassThru
 try{
  $result=Wait-NativeIntegrationResult -Process $process -ExecutablePath $executable -StartedUtc $started -ReportPath $report -LogPaths @($engineLog,$bepinexLog) -Label "$variant optional integration"
 }finally{
  if(Test-Path -LiteralPath $bepinexLog){Copy-Item -LiteralPath $bepinexLog -Destination "$hostDir/bepinex-$variant.log" -Force}
  $process.Dispose()
 }
 Write-Output $result
 if($variant -ne 'baseline' -and (Select-String -LiteralPath "$hostDir/bepinex-$variant.log" -Pattern '\[Error|\[Fatal|Could not find type named Apocaplayer|Could not find type named WildWasteland')){throw "Unexpected error or missing-type spam: $variant"}
}
Write-Output 'Native type/flag checks passed. Live weapon performance and full companion gameplay still require testing.'
