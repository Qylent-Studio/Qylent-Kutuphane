param([switch]$NoBuild)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $repositoryRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
$testProject = Join-Path $repositoryRoot 'tests\Qylent.Kutuphane.Tests\Qylent.Kutuphane.Tests.csproj'

$arguments = @('run', '--project', $testProject, '-c', 'Release', '--', '--performance')
if ($NoBuild) { $arguments = @('run', '--project', $testProject, '-c', 'Release', '--no-build', '--', '--performance') }

& $dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw '50.000/250.000 kayıt performans doğrulaması başarısız.' }
