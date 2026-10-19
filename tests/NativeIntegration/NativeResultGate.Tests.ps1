param(
 [switch]$Fixture,
 [string]$Case,
 [string]$OutputDirectory,
 [string]$GatePath=(Join-Path $PSScriptRoot '../../scripts/native-integration-result.ps1')
)
$ErrorActionPreference='Stop'
if($Fixture){
 $report=Join-Path $OutputDirectory 'result.txt'
 $engine=Join-Path $OutputDirectory 'engine.log'
 $bepinex=Join-Path $OutputDirectory 'bepinex.log'
 Set-Content -LiteralPath $report -Value 'PASS: controlled fixture completed.'
 Set-Content -LiteralPath $engine -Value 'Unity fixture started.'
 Set-Content -LiteralPath $bepinex -Value '[Info:Fixture] Checks complete.'
 switch($Case){
  'exit-one' {exit 1}
  'crash' {Add-Content -LiteralPath $engine -Value 'Crash!!!'}
  'access-violation' {Add-Content -LiteralPath $engine -Value 'EXCEPTION_ACCESS_VIOLATION (0xc0000005)'}
  'native-stacktrace' {Add-Content -LiteralPath $engine -Value 'Native Crash Reporting'}
  'fatal' {Add-Content -LiteralPath $bepinex -Value '[Fatal:Unity] Fatal error in GC'}
  'unhandled' {Add-Content -LiteralPath $engine -Value 'Unhandled Exception: shutdown fixture failure'}
  'shutdown' {Add-Content -LiteralPath $engine -Value 'Error while shutting down Unity.'}
  'stale-report' {(Get-Item -LiteralPath $report).LastWriteTimeUtc=[DateTime]::UtcNow.AddHours(-1)}
  'absent-report' {Remove-Item -LiteralPath $report}
  'failed-report' {Set-Content -LiteralPath $report -Value 'FAIL: fixture assertion failed.'}
  'stale-engine' {(Get-Item -LiteralPath $engine).LastWriteTimeUtc=[DateTime]::UtcNow.AddHours(-1)}
  'absent-engine' {Remove-Item -LiteralPath $engine}
  'empty-engine' {[IO.File]::WriteAllText($engine,'')}
  'stale-bepinex' {(Get-Item -LiteralPath $bepinex).LastWriteTimeUtc=[DateTime]::UtcNow.AddHours(-1)}
  'absent-bepinex' {Remove-Item -LiteralPath $bepinex}
  'expected-baseline' {
   Add-Content -LiteralPath $engine -Value 'ERROR: Shader fixture shader is not supported on this GPU (none of subshaders/fallbacks are suitable)'
   Add-Content -LiteralPath $bepinex -Value '[Info:Fixture] Save probe exception: System.NullReferenceException: intentional negative control','[Warning:HarmonyX] Could not find type named Apocaplayer.ThirdPerson','[Error:Fixture] Expected baseline failure.'
  }
  'timeout' {Start-Sleep -Seconds 30}
 }
 exit 0
}

# These use harmless child PowerShell processes, never Unity or a deliberate crash.
if(-not(Test-Path -LiteralPath $GatePath)){throw 'Native result gate is missing.'}
. $GatePath
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$outputRoot=Join-Path $repo ('artifacts/native-result-gate-tests/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$shell=(Get-Process -Id $PID).Path
$checks=0
$cases=@(
 @{Name='clean';Failure=$null},
 @{Name='exit-one';Failure='exit code'},
 @{Name='crash';Failure='failure marker'},
 @{Name='access-violation';Failure='failure marker'},
 @{Name='native-stacktrace';Failure='failure marker'},
 @{Name='fatal';Failure='failure marker'},
 @{Name='unhandled';Failure='failure marker'},
 @{Name='shutdown';Failure='failure marker'},
 @{Name='stale-report';Failure='Fresh integration report missing'},
 @{Name='absent-report';Failure='Fresh integration report missing'},
 @{Name='failed-report';Failure='report did not pass'},
 @{Name='stale-engine';Failure='Fresh integration log missing'},
 @{Name='absent-engine';Failure='Fresh integration log missing'},
 @{Name='empty-engine';Failure='Integration log is empty'},
 @{Name='stale-bepinex';Failure='Fresh integration log missing'},
 @{Name='absent-bepinex';Failure='Fresh integration log missing'},
 @{Name='expected-baseline';Failure=$null},
 @{Name='timeout';Failure='timed out'}
)
foreach($test in $cases){
 $directory=Join-Path $outputRoot $test.Name
 New-Item -ItemType Directory -Path $directory -Force | Out-Null
 $started=[DateTime]::UtcNow
 $process=Start-Process -FilePath $shell -WorkingDirectory $directory -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File "{0}" -Fixture -Case {1} -OutputDirectory "{2}"' -f $PSCommandPath,$test.Name,$directory) -WindowStyle Hidden -PassThru
 $timeout=10000
 if($test.Name -eq 'timeout'){$timeout=1500}
 $failure=$null
 $runningAfterGate=$false
 try{
  $result=Wait-NativeIntegrationResult -Process $process -ExecutablePath $shell -StartedUtc $started -ReportPath (Join-Path $directory 'result.txt') -LogPaths @((Join-Path $directory 'engine.log'),(Join-Path $directory 'bepinex.log')) -TimeoutMilliseconds $timeout -Label $test.Name
 }catch{$failure=$_.Exception.Message}
 finally{
  $process.Refresh()
  $runningAfterGate=-not $process.HasExited
  if(-not $process.HasExited){Stop-Process -Id $process.Id; $process.WaitForExit()}
 }
 if($test.Failure){
  if(-not $failure -or $failure -notlike "*$($test.Failure)*"){throw "$($test.Name): expected rejection matching '$($test.Failure)', got '$failure'."}
 }else{
  if($failure){throw "$($test.Name): unexpected rejection: $failure"}
  if(-not $result.StartsWith('PASS:')){throw "$($test.Name): accepted result was not returned."}
 }
 if($test.Name -eq 'timeout' -and $runningAfterGate){throw 'Timed-out fixture was left running.'}
 $process.Dispose()
 $checks++
 Write-Output "PASS: $($test.Name)"
}
Write-Output "$checks native integration result gate checks passed. Fixtures: $outputRoot"
