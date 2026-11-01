$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$rootPath = [IO.Path]::GetFullPath($PSScriptRoot)
$cachePath = Join-Path $rootPath '.setup-cache'
$runtimePath = Join-Path $rootPath 'runtime'
New-Item -ItemType Directory -Path $cachePath,$runtimePath -Force | Out-Null
$downloads = @(
    @{Name='tosu-windows-v4.26.3.zip'; Hash='700063da6377c5f69985e09b0d30d575d27653519d7a3b1154bc11f1f9bd5752'; Destination=$runtimePath},
    @{Name='tosu-overlay-v4.26.3.zip'; Hash='c61b12201ca46c7d0c874acb8e5b9ef370ab13561a78fdbf72f9d8cc59ad7dcd'; Destination=(Join-Path $runtimePath 'game-overlay')}
)
try {
    foreach ($asset in $downloads) {
        $archive = Join-Path $cachePath $asset.Name
        $valid = (Test-Path -LiteralPath $archive) -and ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -eq $asset.Hash)
        if (-not $valid) {
            Write-Host ('Downloading official component: ' + $asset.Name)
            Invoke-WebRequest -Uri ('https://github.com/tosuapp/tosu/releases/download/v4.26.3/' + $asset.Name) -OutFile $archive -UseBasicParsing
        }
        if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $asset.Hash) {
            throw ('SHA-256 verification failed: ' + $asset.Name)
        }
        Write-Host ('Verified. Extracting: ' + $asset.Name)
        New-Item -ItemType Directory -Path $asset.Destination -Force | Out-Null
        Expand-Archive -LiteralPath $archive -DestinationPath $asset.Destination -Force
    }
    Write-Host ''
    Write-Host 'Setup complete. Open ManiaTimingBar.exe and click Start when you want to use it.' -ForegroundColor Green
    Write-Host 'Nothing was added to Windows startup. The app has not been started.'
} catch {
    Write-Host ('Setup failed: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
