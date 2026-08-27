<#
.SYNOPSIS
    API ve Web uygulamalarini gelistirme modunda birlikte baslatir.

.EXAMPLE
    .\run-local.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Write-Host 'BookRunner API baslatiliyor (https://localhost:7443/swagger)...' -ForegroundColor Cyan
$api = Start-Process -PassThru -FilePath 'dotnet' `
    -ArgumentList 'run', '--project', (Join-Path $PSScriptRoot 'src/BookRunner.Api/BookRunner.Api.csproj')

Start-Sleep -Seconds 6

Write-Host 'BookRunner Web baslatiliyor (https://localhost:7080)...' -ForegroundColor Cyan
$web = Start-Process -PassThru -FilePath 'dotnet' `
    -ArgumentList 'run', '--project', (Join-Path $PSScriptRoot 'src/BookRunner.Web/BookRunner.Web.csproj')

Write-Host ''
Write-Host 'Kapatmak icin bu pencerede Enter tusuna basin.' -ForegroundColor Yellow
[void](Read-Host)

foreach ($process in @($web, $api)) {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}
