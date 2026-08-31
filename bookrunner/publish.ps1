<#
.SYNOPSIS
    BookRunner API ve Web uygulamalarini yayinlar.

.DESCRIPTION
    Iki mod destekler:
      - Varsayilan: self-contained, tek dosyalik exe ("Local exe" / Windows
        servisi dagitimi). Makinede .NET kurulu olmasi gerekmez.
      - -Iis: framework-dependent yayin + web.config uretir (IIS + ASP.NET Core
        Module icin). Hedef sunucuda .NET 9 Hosting Bundle kurulu olmalidir.

.PARAMETER OutputPath
    Yayin ciktisinin yazilacagi klasor. Varsayilan: .\publish

.PARAMETER Runtime
    Hedef calisma zamani kimligi. Varsayilan: win-x64

.PARAMETER Iis
    Belirtilirse IIS icin framework-dependent yayin yapar (web.config dahil).

.EXAMPLE
    .\publish.ps1 -OutputPath C:\BookRunner
    .\publish.ps1 -OutputPath C:\BookRunner -Iis
#>
[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'publish'),
    [string]$Runtime = 'win-x64',
    [switch]$Iis
)

$ErrorActionPreference = 'Stop'

$projects = @(
    @{ Name = 'BookRunner.Api'; Path = 'src/BookRunner.Api/BookRunner.Api.csproj' },
    @{ Name = 'BookRunner.Web'; Path = 'src/BookRunner.Web/BookRunner.Web.csproj' }
)

foreach ($project in $projects) {
    $target = Join-Path $OutputPath $project.Name
    Write-Host "==> $($project.Name) yayinlaniyor -> $target" -ForegroundColor Cyan

    if ($Iis) {
        # Framework-dependent: kucuk cikti, web.config uretilir (ANCM bunu okur),
        # hedef sunucuda .NET 9 Hosting Bundle kurulu olmasi gerekir.
        dotnet publish (Join-Path $PSScriptRoot $project.Path) `
            --configuration Release `
            --runtime $Runtime `
            --self-contained false `
            --output $target
    }
    else {
        dotnet publish (Join-Path $PSScriptRoot $project.Path) `
            --configuration Release `
            --runtime $Runtime `
            --self-contained true `
            --output $target `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:EnableCompressionInSingleFile=true
    }

    if ($LASTEXITCODE -ne 0) {
        throw "$($project.Name) yayinlanamadi."
    }
}

Write-Host ''
Write-Host 'Yayin tamamlandi.' -ForegroundColor Green

Write-Host ''
Write-Host 'Sonraki adimlar:' -ForegroundColor Yellow
Write-Host '  1. Sunucuya ozel gercek degerleriniz (baglanti dizesi, AD domaini...) src/BookRunner.Api ve'
Write-Host '     src/BookRunner.Web altindaki appsettings.Local.json dosyalarindaysa otomatik tasindi.'
Write-Host '     appsettings.json dosyalarini DUZENLEMEYIN; sonraki git pull''da ezilir.'
Write-Host '  2. sql/01_CreateDatabase.sql ve sql/02_BookRunner_Schema.sql dosyalarini calistirin.'

if ($Iis) {
    Write-Host "  3. IIS sitelerini kurmak icin: tools\New-IisSites.ps1 -ApiPath `"$(Join-Path $OutputPath 'BookRunner.Api')`" -WebPath `"$(Join-Path $OutputPath 'BookRunner.Web')`""
}
else {
    Write-Host "  3. Uygulamalari calistirin veya Windows servisi olarak kaydedin:"
    Write-Host "       sc.exe create BookRunnerApi binPath= `"$(Join-Path $OutputPath 'BookRunner.Api\BookRunner.Api.exe')`" obj= `"CONTOSO\svc-bookrunner`" start= auto"
    Write-Host "       sc.exe create BookRunnerWeb binPath= `"$(Join-Path $OutputPath 'BookRunner.Web\BookRunner.Web.exe')`" obj= `"CONTOSO\svc-bookrunner`" start= auto"
}
