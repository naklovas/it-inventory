<#
.SYNOPSIS
    BookRunner API ve Web icin IIS sitelerini olusturur/gunceller.

.DESCRIPTION
    Iki ayri IIS sitesi kurar (ayri app pool, ayri port). Anonymous
    Authentication'i ACAR, IIS'in kendi Windows Authentication'ini KAPALI
    birakir - kimlik dogrulamayi IIS degil, uygulamanin kendisi (Negotiate
    middleware, Program.cs > AddNegotiate()) yapar. IIS'in Windows
    Authentication'ini da acarsaniz uygulama acilista şu hatayla cöker:
    "The Negotiate Authentication handler cannot be used on a server that
    directly supports Windows Authentication." publish.ps1 -Iis ile
    uretilen iki klasoru (framework-dependent, web.config icerir) hedefler.

    On kosul: sunucuda IIS + ASP.NET Core Hosting Bundle (.NET 9) kurulu
    olmali. "Windows Authentication" IIS rol hizmetinin kurulu olmasi
    GEREKMEZ - kullanilmiyor.

.PARAMETER ApiPath
    publish.ps1 -Iis ciktisindaki BookRunner.Api klasorunun tam yolu.

.PARAMETER WebPath
    publish.ps1 -Iis ciktisindaki BookRunner.Web klasorunun tam yolu.

.PARAMETER ApiPort
    API sitesinin dinleyecegi port. Varsayilan: 7443 (tarayici bu adrese hic
    gitmez; yalnizca Web'in kendisi API'yi bu porttan cagirir).

.PARAMETER WebPort
    Web sitesinin dinleyecegi port. Varsayilan: 443 (kullanicilarin actigi adres).

.PARAMETER CertificateThumbprint
    Verilirse her iki siteye de bu sertifikayla HTTPS binding eklenir
    (sertifikanin makinenin "Local Computer\Personal" deposunda kurulu olmasi
    gerekir: (Get-ChildItem Cert:\LocalMachine\My).Thumbprint).
    Verilmezse yalnizca HTTP binding kurulur; HTTPS'i IIS Manager'dan elle
    ekleyebilirsiniz.

.EXAMPLE
    .\New-IisSites.ps1 -ApiPath C:\BookRunner\BookRunner.Api -WebPath C:\BookRunner\BookRunner.Web
    .\New-IisSites.ps1 -ApiPath C:\BookRunner\BookRunner.Api -WebPath C:\BookRunner\BookRunner.Web -CertificateThumbprint AB12CD34...
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$ApiPath,
    [Parameter(Mandatory)] [string]$WebPath,
    [int]$ApiPort = 7443,
    [int]$WebPort = 443,
    [string]$ApiSiteName = 'BookRunner API',
    [string]$WebSiteName = 'BookRunner Web',
    [string]$CertificateThumbprint
)

$ErrorActionPreference = 'Stop'

Import-Module WebAdministration

function New-BookRunnerIisSite {
    param(
        [string]$Name,
        [string]$PhysicalPath,
        [int]$Port
    )

    if (-not (Test-Path $PhysicalPath)) {
        throw "Klasor bulunamadi: $PhysicalPath (once publish.ps1 -Iis calistirdiniz mi?)"
    }

    $poolName = $Name -replace '\s', ''

    if (-not (Test-Path "IIS:\AppPools\$poolName")) {
        New-WebAppPool -Name $poolName | Out-Null
        Write-Host "App pool olusturuldu: $poolName" -ForegroundColor Green
    }

    # ASP.NET Core kendi runtime'ini tasir; IIS'in ASP.NET CLR pipeline'ina
    # ihtiyaci yoktur - "No Managed Code" burasi.
    Set-ItemProperty "IIS:\AppPools\$poolName" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty "IIS:\AppPools\$poolName" -Name startMode -Value AlwaysRunning

    if (Test-Path "IIS:\Sites\$Name") {
        Set-ItemProperty "IIS:\Sites\$Name" -Name physicalPath -Value $PhysicalPath
        Write-Host "Site zaten vardi, yolu guncellendi: $Name -> $PhysicalPath" -ForegroundColor Yellow
    }
    else {
        New-Website -Name $Name -PhysicalPath $PhysicalPath -ApplicationPool $poolName -Port $Port -Force | Out-Null
        Write-Host "Site olusturuldu: $Name (http, port $Port) -> $PhysicalPath" -ForegroundColor Green
    }

    # Anonymous ACIK birakilir; IIS'in kendi Windows Authentication'ina HIC
    # dokunulmaz (varsayilan olarak zaten kapalidir). Kimlik dogrulamayi
    # uygulamanin kendi Negotiate middleware'i yapar (bkz. Program.cs
    # AddNegotiate()) - IIS'in Windows Authentication'ini da acmak uygulamayi
    # acilista cokertir (bkz. script basindaki aciklama).
    Set-WebConfigurationProperty -PSPath "IIS:\Sites\$Name" `
        -Filter /system.webServer/security/authentication/anonymousAuthentication -Name enabled -Value true

    if ($CertificateThumbprint) {
        $existing = Get-WebBinding -Name $Name -Protocol https -Port $Port -ErrorAction SilentlyContinue
        if (-not $existing) {
            New-WebBinding -Name $Name -Protocol https -Port $Port -SslFlags 0
        }

        $binding = Get-WebBinding -Name $Name -Protocol https -Port $Port
        $binding.AddSslCertificate($CertificateThumbprint, 'my')
        Write-Host "HTTPS binding eklendi: $Name (port $Port)" -ForegroundColor Green
    }
}

New-BookRunnerIisSite -Name $ApiSiteName -PhysicalPath $ApiPath -Port $ApiPort
New-BookRunnerIisSite -Name $WebSiteName -PhysicalPath $WebPath -Port $WebPort

$scheme = if ($CertificateThumbprint) { 'https' } else { 'http' }

Write-Host ''
Write-Host 'Tamamlandi.' -ForegroundColor Green
Write-Host "  API : ${scheme}://<sunucu-adi>:$ApiPort"
Write-Host "  Web : ${scheme}://<sunucu-adi>:$WebPort"
Write-Host ''
Write-Host 'ONEMLI: Web, API''yi bu adresten cagiracak sekilde ayarlanmali.' -ForegroundColor Yellow
Write-Host "  src/BookRunner.Web/appsettings.Local.json > Api:BaseUrl = `"${scheme}://<sunucu-adi>:$ApiPort`""
Write-Host 'CertificateThumbprint vermediyseniz HTTPS binding''i IIS Manager''dan elle ekleyin.'
