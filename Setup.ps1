$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$rootPath = [IO.Path]::GetFullPath($PSScriptRoot)
$cachePath = Join-Path $rootPath '.setup-cache'
$runtimePath = Join-Path $rootPath 'runtime'
$helper = Join-Path $rootPath 'ManiaHelper.exe'
$version = 'v4.26.3'
$releaseUrl = "https://github.com/tosuapp/tosu/releases/download/$version"
$components = @(
    @{
        Name = "tosu-windows-$version.zip"
        Hash = '700063da6377c5f69985e09b0d30d575d27653519d7a3b1154bc11f1f9bd5752'
        Destination = $runtimePath
        Files = @('tosu.exe')
    },
    @{
        Name = "tosu-overlay-$version.zip"
        Hash = 'c61b12201ca46c7d0c874acb8e5b9ef370ab13561a78fdbf72f9d8cc59ad7dcd'
        Destination = (Join-Path $runtimePath 'game-overlay')
        Files = @('tosu-ingame-overlay.exe', 'icudtl.dat', 'resources.pak', 'resources/app.asar',
            'resources/app.asar.unpacked/node_modules/@asdf-overlay/core/asdf_overlay-x64.dll')
    }
)

function Test-Component($component) {
    if (!(Test-Path -LiteralPath $component.Marker -PathType Leaf) -or
        (Get-Content -LiteralPath $component.Marker -Raw).Trim() -ne $component.Hash) { return $false }
    foreach ($name in $component.Files) {
        $file = Get-Item -LiteralPath (Join-Path $component.Destination $name) -ErrorAction SilentlyContinue
        if (!$file -or $file.PSIsContainer -or !$file.Length) { return $false }
    }
    return $true
}

function Install-Component($component) {
    Write-Host ('Installing ' + $component.Name)
    [IO.File]::Delete($component.Marker)
    $destination = [IO.Path]::GetFullPath($component.Destination) + [IO.Path]::DirectorySeparatorChar
    $zip = [IO.Compression.ZipFile]::OpenRead($component.Archive)
    try {
        foreach ($entry in $zip.Entries) {
            $target = [IO.Path]::GetFullPath((Join-Path $destination $entry.FullName))
            if (!$target.StartsWith($destination, [StringComparison]::OrdinalIgnoreCase)) {
                throw ('Invalid archive entry: ' + $entry.FullName)
            }
            if (!$entry.Name) { continue }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            $temporary = $target + '.setup-part'
            try {
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $temporary, $true)
                if ([IO.File]::Exists($target)) { [IO.File]::Replace($temporary, $target, [NullString]::Value) }
                else { [IO.File]::Move($temporary, $target) }
            } finally {
                [IO.File]::Delete($temporary)
            }
        }
        Set-Content -LiteralPath $component.Marker -Value $component.Hash -Encoding ASCII
    } finally {
        $zip.Dispose()
    }
}

$downloads = @()
$exitCode = 0
$setupLock = $null
try {
    if (!(Test-Path -LiteralPath $helper -PathType Leaf)) { throw 'Extract all Mania Helper files first.' }
    [IO.Directory]::CreateDirectory($cachePath) | Out-Null
    $setupLock = [IO.File]::Open((Join-Path $cachePath 'setup.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    $pending = @(
        foreach ($component in $components) {
            $component.Archive = Join-Path $cachePath $component.Name
            $component.Marker = $component.Archive + '.installed'
            if (!(Test-Component $component)) { $component }
        }
    )

    if ($pending.Count) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        foreach ($component in $pending) {
            if ((Test-Path -LiteralPath $component.Archive -PathType Leaf) -and
                (Get-FileHash -LiteralPath $component.Archive -Algorithm SHA256).Hash -eq $component.Hash) { continue }
            $component.Part = $component.Archive + '.part'
            $component.Client = New-Object Net.WebClient
            $downloads += $component
            Write-Host ('Downloading ' + $component.Name)
            $component.Task = $component.Client.DownloadFileTaskAsync([Uri]("$releaseUrl/" + $component.Name), $component.Part)
        }

        $failed = @()
        foreach ($component in $downloads) {
            try {
                $component.Task.GetAwaiter().GetResult()
                if ((Get-FileHash -LiteralPath $component.Part -Algorithm SHA256).Hash -ne $component.Hash) {
                    throw 'SHA-256 verification failed.'
                }
                if ([IO.File]::Exists($component.Archive)) { [IO.File]::Replace($component.Part, $component.Archive, [NullString]::Value) }
                else { [IO.File]::Move($component.Part, $component.Archive) }
            } catch {
                $failed += $component.Name + ': ' + $_.Exception.Message
            }
        }
        if ($failed.Count) { throw ($failed -join [Environment]::NewLine) }

        foreach ($component in $pending) { Install-Component $component }
    } else {
        Write-Host 'Components are already installed.'
    }

    $register = Start-Process -FilePath $helper -ArgumentList '--register-auto-start' -WorkingDirectory $rootPath -WindowStyle Hidden -Wait -PassThru
    if ($register.ExitCode -ne 0) { throw 'Could not configure automatic startup.' }
    $watch = New-Object Diagnostics.ProcessStartInfo -ArgumentList $helper, '--watch'
    $watch.WorkingDirectory = $rootPath
    $watch.UseShellExecute = $false
    $watch.CreateNoWindow = $true
    [Diagnostics.Process]::Start($watch).Dispose()
    Write-Host 'Setup complete.' -ForegroundColor Green
} catch {
    Write-Host ('Setup failed: ' + $_.Exception.Message) -ForegroundColor Red
    $exitCode = 1
} finally {
    foreach ($component in $downloads) {
        $component.Client.Dispose()
        Remove-Item -LiteralPath $component.Part -Force -ErrorAction SilentlyContinue
    }
    if ($setupLock) { $setupLock.Dispose() }
}
exit $exitCode
