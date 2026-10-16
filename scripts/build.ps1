param([string]$GameDir=$env:APOCALYPTER_GAME_DIR,[switch]$NoRestore)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if(-not $GameDir){throw 'Use -GameDir with your Apocalypter installation, or set APOCALYPTER_GAME_DIR.'}
$game=(Resolve-Path -LiteralPath $GameDir).Path
if(-not(Test-Path -LiteralPath (Join-Path $game 'Apocalypter_Data/Managed/Assembly-CSharp.dll'))){throw 'GameDir is not an Apocalypter installation.'}
$arguments=@('build',(Join-Path $repo 'src/ApocalypterWeaponsPack.csproj'),'-c','Release','-v','minimal',"-p:GameDir=$game")
if($NoRestore){$arguments+='--no-restore'}
& dotnet @arguments
if($LASTEXITCODE -ne 0){throw 'Source build failed.'}
Write-Output "Built into $repo/artifacts/runtime. The installed game plugin has not been replaced."
