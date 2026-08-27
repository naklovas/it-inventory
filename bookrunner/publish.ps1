<#
.SYNOPSIS
    BookRunner API ve Web uygulamalarini tek dosyalik Windows exe olarak yayinlar.

.DESCRIPTION
    "Local exe" dagitimi icin her iki uygulamayi da self-contained, tek dosya
    halinde uretir. Cikti klasorleri hedef sunucuya kopyalanip dogrudan
    calistirilabilir; makinede .NET kurulu olmasi gerekmez.

.PARAMETER OutputPath
    Yayin ciktisinin yazilacagi klasor. Varsayilan: .\publish

.PARAMETER Runtime
    Hedef calisma zamani kimligi. Varsayilan: win-x64

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -OutputPath C:\BookRunner -Runtime win-x64
#>
[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'publish'),
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$projects = @(
    @{ Name = 'BookRunner.Api'; Path = 'src/BookRunner.Api/BookRunner.Api.csproj' },
    @{ Name = 'BookRunner.Web'; Path = 'src/BookRunner.Web/BookRunner.Web.csproj' }
)

foreach ($project in $projects) {
    $target = Join-Path $OutputPath $project.Name
    Write-Host "==> $($project.Name) yayinlaniyor -> $target" -ForegroundColor Cyan

    dotnet publish (Join-Path $PSScriptRoot $project.Path) `
        --configuration Release `
        --runtime $Runtime `
        --self-contained true `
        --output $target `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true

    if ($LASTEXITCODE -ne 0) {
        throw "$($project.Name) yayinlanamadi."
    }
}

Write-Host ''
Write-Host 'Yayin tamamlandi.' -ForegroundColor Green
Write-Host "  API : $(Join-Path $OutputPath 'BookRunner.Api\BookRunner.Api.exe')"
Write-Host "  Web : $(Join-Path $OutputPath 'BookRunner.Web\BookRunner.Web.exe')"
Write-Host ''
Write-Host 'Sonraki adimlar:' -ForegroundColor Yellow
Write-Host '  1. Her iki klasordeki appsettings.json dosyalarini ortaminiza gore duzenleyin.'
Write-Host '  2. sql/01_CreateDatabase.sql ve sql/02_BookRunner_Schema.sql dosyalarini calistirin.'
Write-Host '  3. Uygulamalari calistirin veya Windows servisi olarak kaydedin:'
Write-Host '       sc.exe create BookRunnerApi binPath= "C:\BookRunner\BookRunner.Api\BookRunner.Api.exe" obj= "CONTOSO\svc-bookrunner" start= auto'
Write-Host '       sc.exe create BookRunnerWeb binPath= "C:\BookRunner\BookRunner.Web\BookRunner.Web.exe" obj= "CONTOSO\svc-bookrunner" start= auto'
