$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not (Test-Path -LiteralPath $compiler)){throw '.NET Framework 4.8 (64-bit) is required.'}
$sources=Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'source') -Filter '*.cs' -File | ForEach-Object {$_.FullName}
& $compiler /nologo /target:winexe /platform:x64 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll ("/out:"+(Join-Path $PSScriptRoot 'ManiaHelper.exe')) $sources
if($LASTEXITCODE -ne 0){throw 'Build failed.'}
