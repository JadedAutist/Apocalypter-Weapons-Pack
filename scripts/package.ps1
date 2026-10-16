param()
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifest=Get-Content -LiteralPath (Join-Path $repo 'packaging/runtime-files.json') -Raw | ConvertFrom-Json
$runtime=Join-Path $repo 'artifacts/runtime/BepInEx/plugins/Apocalypter Weapons Pack'
$dll=Join-Path $runtime 'ChainsawPrototype.dll'
& dotnet run --project (Join-Path $repo 'tests/ReleaseAudit/ReleaseAudit.csproj') -c Release -v quiet -- $dll $manifest.version
if($LASTEXITCODE -ne 0){throw 'Release identity audit failed.'}
$dist=Join-Path $repo 'dist';New-Item -ItemType Directory -Path $dist -Force | Out-Null
$zip=Join-Path $dist "Apocalypter-Weapons-Pack-$($manifest.version).zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$temporaryZip=Join-Path $dist ([Guid]::NewGuid().ToString('N')+'.tmp')
$stream=[IO.File]::Open($temporaryZip,[IO.FileMode]::CreateNew)
$archive=New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create)
try{
 $prefix='BepInEx/plugins/Apocalypter Weapons Pack/'
 [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$dll,($prefix+'ChainsawPrototype.dll'),[IO.Compression.CompressionLevel]::Optimal) | Out-Null
 foreach($asset in $manifest.assets){
  if([IO.Path]::IsPathRooted($asset.path) -or $asset.path -match '(^|/)\.\.(/|$)|:|spear|\.(dll|pdb)$'){throw "Invalid public runtime asset path: $($asset.path)"}
  $file=Join-Path $runtime $asset.path
  if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $asset.sha256){throw "Built runtime asset mismatch: $($asset.path)"}
  [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file,($prefix+$asset.path),[IO.Compression.CompressionLevel]::Optimal) | Out-Null
 }
 # The player README does not need the repository header image or build docs.
 $readme=Get-Content -LiteralPath (Join-Path $repo 'README.md') -Raw
 $readme=[regex]::Replace($readme,'(?m)^!\[.*\]\(docs/images/header\.png\)\s*','')
 $readme=($readme -split '## Source and development',2)[0]
 $readme=$readme.Replace('[testing](docs/TESTING.md)','the testing guide on GitHub')
 $readmeEntry=$archive.CreateEntry('README.md',[IO.Compression.CompressionLevel]::Optimal)
 $writer=New-Object IO.StreamWriter($readmeEntry.Open(),(New-Object Text.UTF8Encoding($false)))
 try{$writer.Write($readme)}finally{$writer.Dispose()}
 [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $repo 'CHANGELOG.md'),'CHANGELOG.md',[IO.Compression.CompressionLevel]::Optimal) | Out-Null
}catch{
 $archive.Dispose();$stream.Dispose()
 Remove-Item -LiteralPath $temporaryZip -Force
 throw
}finally{$archive.Dispose();$stream.Dispose()}
Move-Item -LiteralPath $temporaryZip -Destination $zip -Force
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
[IO.File]::WriteAllText(($zip+'.sha256'),($hash+'  '+[IO.Path]::GetFileName($zip)+[Environment]::NewLine))
Write-Output "Packaged only the audited DLL, approved runtime assets and release documentation: $zip"
