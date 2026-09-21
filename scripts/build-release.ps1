param([switch]$Installer)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $repositoryRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
$solution = Join-Path $repositoryRoot 'Qylent.Kutuphane.slnx'
$appProject = Join-Path $repositoryRoot 'src\Qylent.Kutuphane.App\Qylent.Kutuphane.App.csproj'
$testProject = Join-Path $repositoryRoot 'tests\Qylent.Kutuphane.Tests\Qylent.Kutuphane.Tests.csproj'
$publishDirectory = Join-Path $repositoryRoot 'artifacts\publish\win-x64'

& $dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw 'Paketler geri yüklenemedi.' }
& $dotnet build $solution -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Release derlemesi başarısız.' }
& $dotnet run --project $testProject -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'Doğrulama senaryoları başarısız.' }
& $dotnet restore $appProject -r win-x64
if ($LASTEXITCODE -ne 0) { throw 'Windows çalışma zamanı paketleri geri yüklenemedi.' }
& $dotnet publish $appProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --no-restore -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'Self-contained yayın üretilemedi.' }

if ($Installer) {
    $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    $compilerPath = if ($compiler) { $compiler.Source } else {
        @(
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
        ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
    if (-not $compilerPath) { throw 'Inno Setup 6 bulunamadı. Önce Inno Setup kurun veya -Installer parametresini kullanmadan yayın üretin.' }
    & $compilerPath (Join-Path $repositoryRoot 'installer\QylentKutuphane.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Kurulum EXE dosyası üretilemedi.' }
}

Write-Host "Yayın hazır: $publishDirectory"
