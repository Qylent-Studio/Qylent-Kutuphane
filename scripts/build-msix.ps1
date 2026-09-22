param(
    [string]$IdentityName = 'QylentStudio.QylentKutuphane',
    [string]$Publisher = 'CN=Qylent Studio',
    [string]$PublisherDisplayName = 'Qylent Studio',
    [string]$Version = '1.2.0.0',
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repositoryRoot 'artifacts'
$publishDirectory = Join-Path $artifactRoot 'publish\win-x64'
$outputDirectory = Join-Path $artifactRoot 'store'
$stagingDirectory = Join-Path $artifactRoot 'msix-staging'
$templatePath = Join-Path $repositoryRoot 'packaging\msix\AppxManifest.xml'
$assetSource = Join-Path $repositoryRoot 'packaging\msix\Assets'

if ($IdentityName -notmatch '^[A-Za-z0-9.-]{3,50}$') { throw 'Partner Center paket kimliği geçersiz.' }
if ($Publisher -notmatch '^CN=.+') { throw 'Publisher değeri Partner Center kimliğindeki CN ile başlamalıdır.' }
if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'MSIX sürümü dört sayılı olmalıdır (1.2.0.0 gibi).' }
if (-not (Test-Path -LiteralPath $assetSource)) { throw 'MSIX görsel varlıkları bulunamadı.' }

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot 'build-release.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Release yayını oluşturulamadı.' }
}
if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory 'Qylent.Kutuphane.exe'))) { throw 'Yayın EXE dosyası bulunamadı.' }

$resolvedArtifactRoot = (Resolve-Path -LiteralPath $artifactRoot).Path
if (Test-Path -LiteralPath $stagingDirectory) {
    $resolvedStaging = (Resolve-Path -LiteralPath $stagingDirectory).Path
    if (-not $resolvedStaging.StartsWith($resolvedArtifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Güvensiz MSIX staging hedefi.' }
    Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stagingDirectory, (Join-Path $stagingDirectory 'Assets'), $outputDirectory | Out-Null

Get-ChildItem -LiteralPath $publishDirectory -File | Where-Object Extension -ne '.pdb' | Copy-Item -Destination $stagingDirectory
Copy-Item -LiteralPath (Join-Path $assetSource 'StoreLogo.png'), (Join-Path $assetSource 'Square44x44Logo.png'), (Join-Path $assetSource 'Square150x150Logo.png') -Destination (Join-Path $stagingDirectory 'Assets')

$manifest = Get-Content -LiteralPath $templatePath -Raw
$manifest = $manifest.Replace('__IDENTITY_NAME__', [Security.SecurityElement]::Escape($IdentityName))
$manifest = $manifest.Replace('__PUBLISHER__', [Security.SecurityElement]::Escape($Publisher))
$manifest = $manifest.Replace('__PUBLISHER_DISPLAY_NAME__', [Security.SecurityElement]::Escape($PublisherDisplayName))
$manifest = $manifest.Replace('__VERSION__', $Version)
[IO.File]::WriteAllText((Join-Path $stagingDirectory 'AppxManifest.xml'), $manifest, [Text.UTF8Encoding]::new($false))

$makeAppx = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Filter makeappx.exe -File -Recurse -ErrorAction SilentlyContinue |
    Where-Object FullName -Match '\\x64\\makeappx\.exe$' | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeAppx) { throw 'Windows SDK makeappx.exe bulunamadı.' }

$outputPath = Join-Path $outputDirectory "Qylent-Kutuphane-$Version-x64.msix"
& $makeAppx.FullName pack /d $stagingDirectory /p $outputPath /o
if ($LASTEXITCODE -ne 0) { throw 'MSIX paketi oluşturulamadı.' }

$hash = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash
Write-Host "MSIX hazır: $outputPath"
Write-Host "SHA-256: $hash"
Write-Host "Partner Center değerleriyle yeniden üretmeden Store'a yüklemeyin."
