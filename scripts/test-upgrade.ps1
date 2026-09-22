param(
    [Parameter(Mandatory = $true)][string]$OldInstaller,
    [Parameter(Mandatory = $true)][string]$NewInstaller
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$installDirectory = Join-Path $artifactsRoot 'upgrade-smoke-app'
$isolatedLocalAppData = Join-Path $artifactsRoot 'upgrade-smoke-data'
$dataDirectory = Join-Path $isolatedLocalAppData 'QylentStudio\Kutuphane'
$oldLog = Join-Path $artifactsRoot 'upgrade-old-install.log'
$newLog = Join-Path $artifactsRoot 'upgrade-new-install.log'
$oldInstallerPath = (Resolve-Path -LiteralPath $OldInstaller).Path
$newInstallerPath = (Resolve-Path -LiteralPath $NewInstaller).Path
$environment = @{ LOCALAPPDATA = $isolatedLocalAppData }

function Invoke-Setup([string]$path, [string[]]$arguments) {
    $process = Start-Process -FilePath $path -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden -Environment $environment
    if ($process.ExitCode -ne 0) { throw "Kurulum $($process.ExitCode) koduyla başarısız oldu: $path" }
}

try {
    New-Item -ItemType Directory -Force -Path $installDirectory, $dataDirectory, (Join-Path $dataDirectory 'Backups') | Out-Null
    Invoke-Setup $oldInstallerPath @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', '/CURRENTUSER', ('/DIR="{0}"' -f $installDirectory), ('/LOG="{0}"' -f $oldLog))

    $application = Join-Path $installDirectory 'Qylent.Kutuphane.exe'
    if (-not (Test-Path -LiteralPath $application)) { throw 'Eski sürüm uygulaması kurulmadı.' }
    $oldApplicationHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $application).Hash

    $database = Join-Path $dataDirectory 'kutuphane.db'
    $settingsKey = Join-Path $dataDirectory 'sensitive-fields.key'
    $backup = Join-Path $dataDirectory 'Backups\upgrade-test.qylibbackup'
    Set-Content -LiteralPath $database -Value 'database-before-upgrade' -NoNewline
    Set-Content -LiteralPath $settingsKey -Value 'settings-before-upgrade' -NoNewline
    Set-Content -LiteralPath $backup -Value 'backup-before-upgrade' -NoNewline
    $before = @($database, $settingsKey, $backup) | ForEach-Object { (Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash }

    Invoke-Setup $newInstallerPath @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', '/CURRENTUSER', ('/LOG="{0}"' -f $newLog))
    if (-not (Test-Path -LiteralPath $application)) { throw 'Yükseltme mevcut kurulum dizinini kullanmadı.' }
    $newApplicationHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $application).Hash
    if ($oldApplicationHash -eq $newApplicationHash) { throw 'Uygulama dosyası yeni sürümle değiştirilmedi.' }

    $after = @($database, $settingsKey, $backup) | ForEach-Object {
        if (-not (Test-Path -LiteralPath $_)) { throw "Kullanıcı verisi kayboldu: $_" }
        (Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash
    }
    if (Compare-Object $before $after) { throw 'Veritabanı, ayar anahtarı veya yedek içeriği değişti.' }

    Write-Host 'Yükseltme doğrulaması başarılı: mevcut kurulum dizini kullanıldı, uygulama güncellendi, veritabanı/ayar/yedek korundu.'
}
finally {
    $uninstaller = Join-Path $installDirectory 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller) {
        $process = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -Wait -PassThru -WindowStyle Hidden -Environment $environment
        if ($process.ExitCode -ne 0) { Write-Warning "Test kurulumu kaldırılamadı: $($process.ExitCode)" }
    }
}
