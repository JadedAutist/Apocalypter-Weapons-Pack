param([string]$GameDir=$env:APOCALYPTER_GAME_DIR,[string]$BaselineZip)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'native-integration-result.ps1')
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if(-not $GameDir){throw 'Integration checks need -GameDir or APOCALYPTER_GAME_DIR.'}
$game=(Resolve-Path -LiteralPath $GameDir).Path
$hostDir=Join-Path $repo 'artifacts/integration-host'
$runtime=Join-Path $repo 'artifacts/runtime/BepInEx/plugins/Apocalypter Weapons Pack/ChainsawPrototype.dll'
$pocket=Join-Path $game 'BepInEx/plugins/Apocapocket/Apocapocket.dll'
if(-not(Test-Path -LiteralPath $pocket)){throw 'Install Apocapocket for this optional integration check.'}
New-Item -ItemType Directory -Path "$hostDir/lib/fixed","$hostDir/lib/baseline","$hostDir/BepInEx/core","$hostDir/BepInEx/plugins/MerchantChecks" -Force | Out-Null
Copy-Item -LiteralPath "$game/Apocalypter.exe" -Destination "$hostDir/Apocalypter-MerchantChecks.exe" -Force
Copy-Item -LiteralPath "$game/UnityPlayer.dll","$game/winhttp.dll","$game/doorstop_config.ini" -Destination $hostDir -Force
Copy-Item -LiteralPath "$game/BepInEx/core" -Destination "$hostDir/BepInEx" -Recurse -Force
Copy-Item -LiteralPath $runtime -Destination "$hostDir/lib/fixed/ChainsawPrototype.dll" -Force
Copy-Item -LiteralPath $pocket -Destination "$hostDir/lib/Apocapocket.dll" -Force
$variants=@('fixed')
if($BaselineZip){
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $archive=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $BaselineZip).Path)
 try{
  $entry=$archive.GetEntry('BepInEx/plugins/Apocalypter Weapons Pack/ChainsawPrototype.dll')
  if(-not $entry){throw 'Baseline ZIP does not contain the original plugin path.'}
  [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,"$hostDir/lib/baseline/ChainsawPrototype.dll",$true)
 }finally{$archive.Dispose()}
 if((Get-FileHash -LiteralPath "$hostDir/lib/baseline/ChainsawPrototype.dll" -Algorithm SHA256).Hash -ne '3AB989E53364EC4CFBB4E2B3253E190BB726862162D0C7F7226CEF09D3CEBBF5'){throw 'Unrecognized baseline DLL.'}
 $variants=@('baseline','fixed')
}
foreach($link in @(@('Apocalypter-MerchantChecks_Data','Apocalypter_Data'),@('MonoBleedingEdge','MonoBleedingEdge'))){
 $destination=Join-Path $hostDir $link[0];$target=Join-Path $game $link[1]
 if(-not(Test-Path -LiteralPath $destination)){New-Item -ItemType Junction -Path $destination -Target $target | Out-Null}
 elseif((Get-Item -LiteralPath $destination).Target -ne $target){throw 'Unexpected integration-host junction.'}
}
$compiler='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$compilerArgs=@('/nologo','/target:library',"/out:$hostDir/BepInEx/plugins/MerchantChecks/MerchantChecks.dll","/reference:$game/BepInEx/core/BepInEx.dll")
foreach($name in @('UnityEngine','UnityEngine.CoreModule','PlayMaker','Assembly-CSharp-firstpass','netstandard')){$compilerArgs+="/reference:$game/Apocalypter_Data/Managed/$name.dll"}
$compilerArgs+=(Join-Path $repo 'tests/MerchantIntegration/MerchantEngineChecks.cs')
& $compiler @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Integration checker compilation failed.'}
if((Get-ChildItem -LiteralPath "$hostDir/BepInEx/plugins" -Recurse -Filter '*.dll').Count -ne 1){throw 'Unexpected plugin in isolated host. Inspect it before testing.'}
$executable=[IO.Path]::GetFullPath((Join-Path $hostDir 'Apocalypter-MerchantChecks.exe'))
foreach($variant in $variants){
 $report=Join-Path $hostDir "result-$variant.txt"
 $engineLog=Join-Path $hostDir "engine-$variant.log"
 $bepinexLog=Join-Path $hostDir 'BepInEx/LogOutput.log'
 foreach($output in @($report,$engineLog,$bepinexLog)){if(Test-Path -LiteralPath $output){Remove-Item -LiteralPath $output -Force}}
 $started=[DateTime]::UtcNow
 $process=Start-Process -FilePath $executable -WorkingDirectory $hostDir -ArgumentList "-batchmode -nographics -logFile engine-$variant.log --merchant-variant $variant" -WindowStyle Hidden -PassThru
 try{
  $result=Wait-NativeIntegrationResult -Process $process -ExecutablePath $executable -StartedUtc $started -ReportPath $report -LogPaths @($engineLog,$bepinexLog) -Label "$variant merchant integration"
 }finally{
  if(Test-Path -LiteralPath $bepinexLog){Copy-Item -LiteralPath $bepinexLog -Destination "$hostDir/bepinex-$variant.log" -Force}
  $process.Dispose()
 }
 Write-Output $result
}
