param(
 [Parameter(Mandatory=$true)][string]$GameDir,
 [Parameter(Mandatory=$true)][string]$CandidateDll
)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$runName='path-length-negative-control-'+[Guid]::NewGuid().ToString('N')
$hostDir=Join-Path $repo "artifacts/profile-integration-host/$runName"
$logDirectory=Join-Path $repo 'artifacts/profile-runner-tests'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$log=Join-Path $logDirectory ($runName+'.log')
# Safety guard for the negative control: a regressed preflight must not launch
# a known-invalid Unity host while this test verifies its actual side effects.
function Start-Process { throw 'Path negative control must not launch Unity.' }
$failureMessage=''
try{& (Join-Path $repo 'scripts/test-profile-independence.ps1') -GameDir $GameDir -CandidateDll $CandidateDll -RunName $runName *> $log}
catch{$failureMessage=$_.Exception.Message}
$failures=0
if($failureMessage -notmatch 'Windows MAX_PATH'){$failures++;Write-Output ('FAIL: overlong host path must give an explicit Windows MAX_PATH rejection; got: '+$failureMessage)}
if(Test-Path -LiteralPath $hostDir){$failures++;Write-Output ('FAIL: overlong host path created a directory before rejection: '+$hostDir)}
if($failures){throw "$failures profile runner preflight regression checks failed; evidence: $log"}
Write-Output 'PASS: 2 profile runner checks; overlong host path rejected before host creation or Unity launch.'
