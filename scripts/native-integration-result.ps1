function Wait-NativeIntegrationResult {
 param(
  [Parameter(Mandatory=$true)][System.Diagnostics.Process]$Process,
  [Parameter(Mandatory=$true)][string]$ExecutablePath,
  [Parameter(Mandatory=$true)][DateTime]$StartedUtc,
  [Parameter(Mandatory=$true)][string]$ReportPath,
  [Parameter(Mandatory=$true)][string[]]$LogPaths,
  [ValidateRange(1,2147483647)][int]$TimeoutMilliseconds=45000,
  [string]$Label='Native integration'
 )
 $ErrorActionPreference='Stop'
 if(-not $Process.WaitForExit($TimeoutMilliseconds)){
  $owned=Get-Process -Id $Process.Id -ErrorAction SilentlyContinue
  if($owned -and $owned.Path -eq $ExecutablePath){
   Stop-Process -Id $owned.Id -ErrorAction Stop
   $null=$Process.WaitForExit(5000)
  }
  throw "$Label timed out."
 }
 $Process.Refresh()
 $exitCode=$Process.ExitCode
 if($null -eq $exitCode -or $exitCode -ne 0){throw "$Label process exit code was '$exitCode'; expected 0."}
 if(-not(Test-Path -LiteralPath $ReportPath -PathType Leaf) -or (Get-Item -LiteralPath $ReportPath).LastWriteTimeUtc -lt $StartedUtc){throw "Fresh integration report missing: $ReportPath"}
 $result=[string](Get-Content -LiteralPath $ReportPath -Raw)
 if(-not $result.StartsWith('PASS:')){throw "$Label report did not pass: $ReportPath"}
 # Unity emits expected headless shader errors, and baseline fixtures intentionally
 # log handled exceptions/missing types. Only definitive crash/shutdown failures
 # belong here; the callers retain their stricter non-baseline BepInEx checks.
 $failurePattern='(?i)Crash!!!|Native Crash Reporting|EXCEPTION_ACCESS_VIOLATION|\bAccess Violation\b|\bSIGSEGV\b|\bReceived signal\b|\bFatal error\b|\[Fatal\b|\bUnhandled Exception\b|\bAssertion failed on expression\b|\b(?:error|exception|failed|failure)\b[^\r\n]*\b(?:shutdown|shutting down)\b|\b(?:shutdown|shutting down)\b[^\r\n]*\b(?:error|exception|failed|failure)\b'
 foreach($log in $LogPaths){
  if(-not(Test-Path -LiteralPath $log -PathType Leaf) -or (Get-Item -LiteralPath $log).LastWriteTimeUtc -lt $StartedUtc){throw "Fresh integration log missing: $log"}
  if((Get-Item -LiteralPath $log).Length -eq 0){throw "Integration log is empty: $log"}
  $failure=Select-String -LiteralPath $log -Pattern $failurePattern | Select-Object -First 1
  if($failure){throw "$Label native failure marker in ${log}:$($failure.LineNumber): $($failure.Line.Trim())"}
 }
 return $result
}
