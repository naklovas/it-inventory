<#
.SYNOPSIS
    bookrunner/ klasorunden kendi kendine yeten bir kurulum script'i (scaffold-bookrunner.ps1) uretir.

.DESCRIPTION
    Cozumun tum kaynak kodunu, yapilandirmasini, SQL script'lerini ve arayuz
    kutuphanelerini tek bir ZIP'e sikistirip Base64 olarak bir PowerShell
    dosyasinin icine gomer. Ortaya cikan dosya tek basina dagitilabilir;
    calistirildiginda cozumu diske acar ve istege bagli olarak derler.

    Kaynak dosyalarda degisiklik yaptiktan sonra bu script'i yeniden calistirip
    scaffold-bookrunner.ps1 dosyasini guncelleyin.

.PARAMETER SourcePath
    Paketlenecek cozum klasoru. Varsayilan: depo kokundeki bookrunner klasoru.

.PARAMETER OutputFile
    Uretilecek script. Varsayilan: depo kokundeki scaffold-bookrunner.ps1.

.EXAMPLE
    .\tools\New-BookRunnerScaffold.ps1
#>
[CmdletBinding()]
param(
    [string]$SourcePath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'bookrunner'),
    [string]$OutputFile = (Join-Path (Split-Path -Parent $PSScriptRoot) 'scaffold-bookrunner.ps1')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (-not (Test-Path -LiteralPath $SourcePath)) {
    throw "Kaynak klasor bulunamadi: $SourcePath"
}

$SourcePath = (Resolve-Path -LiteralPath $SourcePath).Path

# Derleme ciktilari pakete girmemeli.
$excludedDirectories = @('bin', 'obj', 'publish', '.vs', '.git')
$excludedExtensions = @('.user', '.suo')

Write-Host "Kaynak     : $SourcePath" -ForegroundColor Cyan

# ---------------------------------------------------------------- hazirlik
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("bookrunner-scaffold-" + [Guid]::NewGuid().ToString('N'))
$archive = "$staging.zip"

try {
    New-Item -ItemType Directory -Force -Path $staging | Out-Null

    # -Force: .gitignore gibi nokta ile baslayan dosyalar Linux/macOS uzerinde
    # gizli sayilir; onlarsiz paket eksik kalirdi.
    $files = Get-ChildItem -LiteralPath $SourcePath -Recurse -File -Force | Where-Object {
        $relative = $_.FullName.Substring($SourcePath.Length).TrimStart([char]'\', [char]'/')
        $segments = $relative -split '[\\/]'

        # Yol uzerindeki herhangi bir klasor dislanan listedeyse dosya atlanir.
        $inExcludedDirectory = $false
        foreach ($segment in $segments[0..($segments.Length - 2)]) {
            if ($excludedDirectories -contains $segment) { $inExcludedDirectory = $true; break }
        }

        -not $inExcludedDirectory -and ($excludedExtensions -notcontains $_.Extension)
    }

    foreach ($file in $files) {
        $relative = $file.FullName.Substring($SourcePath.Length).TrimStart([char]'\', [char]'/')
        $target = Join-Path $staging $relative
        $targetDirectory = Split-Path -Parent $target

        if (-not (Test-Path -LiteralPath $targetDirectory)) {
            New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
        }

        Copy-Item -LiteralPath $file.FullName -Destination $target
    }

    Write-Host "Dosya      : $($files.Count)" -ForegroundColor Cyan

    # ------------------------------------------------------------- paketleme
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $staging, $archive, [System.IO.Compression.CompressionLevel]::Optimal, $false)

    $bytes = [System.IO.File]::ReadAllBytes($archive)
    Write-Host ("Arsiv      : {0:N0} KB" -f ($bytes.Length / 1KB)) -ForegroundColor Cyan

    # Base64'u satirlara bolerek gomeriz; tek satirlik dev bir dize duzenleyicileri zorlar.
    $base64 = [Convert]::ToBase64String($bytes)
    $chunkSize = 120
    $payloadLines = New-Object System.Collections.Generic.List[string]
    for ($offset = 0; $offset -lt $base64.Length; $offset += $chunkSize) {
        $payloadLines.Add($base64.Substring($offset, [Math]::Min($chunkSize, $base64.Length - $offset)))
    }

    # --------------------------------------------------------------- ciktinin ust kismi
    # Not: asagidaki here-string, uretilen script'in Base64 bloguna kadar olan
    # kismini icerir. Kapanis isareti ayri bir satir olarak eklenir; aksi halde
    # buradaki here-string'i erken kapatirdi.
    $header = @'
<#
.SYNOPSIS
    BookRunner cozumunu, SQL script'lerini ve arayuz kutuphanelerini diske acar.

.DESCRIPTION
    Bu dosya kendi kendine yeten bir kurulum paketidir. Cozumun tum icerigi
    (kaynak kod, yapilandirma, EF Core migration'lari, SQL script'leri, ornek
    CSX script'leri ve Bootstrap/SignalR kutuphaneleri) sikistirilmis olarak
    icine gomulmustur; internet baglantisi gerekmez.

    Calistirildiginda cozumu hedef klasore acar ve .NET SDK varsa derler.

    Dosya duzeni: once yardim ve parametreler, sonra tum isi yapan
    Install-BookRunner fonksiyonu, ardindan sikistirilmis icerigin Base64
    blogu, en sonda da fonksiyonun cagrisi yer alir.

.PARAMETER OutputPath
    Cozumun acilacagi klasor. Varsayilan: calisilan dizin altinda "BookRunner".

.PARAMETER Force
    Hedef klasor doluysa uzerine yazar.

.PARAMETER SkipBuild
    Acma isleminden sonra "dotnet build" calistirmaz.

.EXAMPLE
    .\scaffold-bookrunner.ps1

.EXAMPLE
    .\scaffold-bookrunner.ps1 -OutputPath C:\Projeler\BookRunner -Force

.EXAMPLE
    .\scaffold-bookrunner.ps1 -OutputPath D:\src\BookRunner -SkipBuild

.NOTES
    Gereksinimler : .NET 9 SDK (derleme icin), SQL Server, etki alanina uye sunucu
    Uretim tarihi : __GENERATED__
#>
[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path (Get-Location).Path 'BookRunner'),
    [switch]$Force,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

function Install-BookRunner {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string]$OutputPath,
        [Parameter(Mandatory)] [string]$Payload,
        [switch]$Force,
        [switch]$SkipBuild
    )

    Write-Host ''
    Write-Host '  BookRunner - Runbook hazirlama ve isbirligi platformu' -ForegroundColor Cyan
    Write-Host '  ----------------------------------------------------' -ForegroundColor DarkGray
    Write-Host ''

    # ----------------------------------------------------------- hedef klasor
    if (Test-Path -LiteralPath $OutputPath) {
        $existing = @(Get-ChildItem -LiteralPath $OutputPath -Force -ErrorAction SilentlyContinue)
        if ($existing.Count -gt 0 -and -not $Force) {
            throw "Hedef klasor bos degil: $OutputPath. Uzerine yazmak icin -Force kullanin."
        }
    }
    else {
        New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
    }

    $OutputPath = (Resolve-Path -LiteralPath $OutputPath).Path

    # ------------------------------------------------------------------- acma
    # Windows PowerShell 5.1'de sikistirma turleri acikca yuklenmelidir.
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    Write-Host 'Dosyalar aciliyor...' -ForegroundColor Yellow

    # Convert.FromBase64String bosluk karakterlerini yok sayar; satirlara
    # bolunmus blogu oldugu gibi verebiliriz.
    $bytes = [Convert]::FromBase64String($Payload)
    $stream = New-Object System.IO.MemoryStream(, $bytes)
    $zip = New-Object System.IO.Compression.ZipArchive($stream, [System.IO.Compression.ZipArchiveMode]::Read)

    try {
        foreach ($entry in $zip.Entries) {
            # Adi bos olan girdiler klasordur; dosya olarak acilmaz.
            if ([string]::IsNullOrEmpty($entry.Name)) { continue }

            $target = Join-Path $OutputPath $entry.FullName
            $targetDirectory = Split-Path -Parent $target

            if (-not (Test-Path -LiteralPath $targetDirectory)) {
                New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
            }

            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
    }
    finally {
        $zip.Dispose()
        $stream.Dispose()
    }

    $written = @(Get-ChildItem -LiteralPath $OutputPath -Recurse -File -Force)
    Write-Host ("  {0} dosya olusturuldu: {1}" -f $written.Count, $OutputPath) -ForegroundColor Green

    # --------------------------------------------------------------- derleme
    $solution = Join-Path $OutputPath 'BookRunner.sln'

    if ($SkipBuild) {
        Write-Host 'Derleme atlandi (-SkipBuild).' -ForegroundColor DarkGray
    }
    else {
        $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
        if (-not $dotnet) {
            Write-Warning '.NET SDK bulunamadi; derleme atlandi. https://dotnet.microsoft.com/download adresinden .NET 9 SDK kurun.'
        }
        else {
            Write-Host ''
            Write-Host 'Cozum derleniyor (ilk calistirmada NuGet paketleri indirilir)...' -ForegroundColor Yellow
            & dotnet build $solution --nologo

            if ($LASTEXITCODE -ne 0) {
                throw 'Derleme basarisiz oldu. Yukaridaki hatalari inceleyin.'
            }

            Write-Host ''
            Write-Host 'Derleme basarili.' -ForegroundColor Green
        }
    }

    # ---------------------------------------------------------- sonraki adimlar
    $sqlPath = Join-Path $OutputPath 'sql'
    $apiSettings = Join-Path $OutputPath 'src\BookRunner.Api\appsettings.json'

    Write-Host ''
    Write-Host 'Sonraki adimlar' -ForegroundColor Cyan
    Write-Host '---------------' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '1. Veritabanini olusturun (SQL Server uzerinde, yonetici yetkisiyle):'
    Write-Host ("     sqlcmd -S <sunucu> -i `"{0}`"" -f (Join-Path $sqlPath '01_CreateDatabase.sql')) -ForegroundColor Gray
    Write-Host ("     sqlcmd -S <sunucu> -d BookRunner -i `"{0}`"" -f (Join-Path $sqlPath '02_BookRunner_Schema.sql')) -ForegroundColor Gray
    Write-Host ''
    Write-Host '2. AD grubu -> rol eslemesini yapin (SID degerlerini kendi gruplarinizla degistirin):'
    Write-Host ("     sqlcmd -S <sunucu> -d BookRunner -i `"{0}`"" -f (Join-Path $sqlPath '04_RoleMappings.sql')) -ForegroundColor Gray
    Write-Host "     Grup SID'i:  (Get-ADGroup 'BookRunner-Authors').SID.Value" -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '3. (Istege bagli) Service Manager salt-okunur erisimi:'
    Write-Host ("     sqlcmd -S <scsm-dw> -i `"{0}`"" -f (Join-Path $sqlPath '03_ServiceManager_ReadOnly.sql')) -ForegroundColor Gray
    Write-Host ''
    Write-Host '4. Yapilandirmayi ortaminiza gore duzenleyin:'
    Write-Host ("     {0}" -f $apiSettings) -ForegroundColor Gray
    Write-Host '     - ConnectionStrings:BookRunner ve ConnectionStrings:ServiceManager'
    Write-Host '     - ActiveDirectory:Domain (cok domainli kurulumda ActiveDirectory:Domains)'
    Write-Host '     - Authorization:RoleMappings, Email:Host, Email:FromAddress'
    Write-Host ''
    Write-Host '5. Calistirin:'
    Write-Host ("     cd `"{0}`"; .\run-local.ps1" -f $OutputPath) -ForegroundColor Gray
    Write-Host '     Web     : https://localhost:7080'
    Write-Host '     Swagger : https://localhost:7443/swagger'
    Write-Host ''
    Write-Host '6. Tek dosyalik exe olarak yayinlamak icin:'
    Write-Host ("     cd `"{0}`"; .\publish.ps1 -OutputPath C:\BookRunner" -f $OutputPath) -ForegroundColor Gray
    Write-Host ''
    Write-Host ("Ayrintili belge: {0}" -f (Join-Path $OutputPath 'README.md')) -ForegroundColor DarkGray
    Write-Host ''
}

# Cozumun sikistirilmis hali. Dosyanin en sonundaki cagri bunu diske acar.
$Payload = @'
'@

    $footer = @'

Install-BookRunner -OutputPath $OutputPath -Payload $Payload -Force:$Force -SkipBuild:$SkipBuild
'@

    # ------------------------------------------------------------------ yazma
    # Uretilen dosya yalnizca ASCII karakter icerir; boylece Windows PowerShell
    # 5.1 dosyayi BOM olmadan da dogru okur.
    $output = New-Object System.Collections.Generic.List[string]
    $output.Add(($header -replace '__GENERATED__', (Get-Date -Format 'yyyy-MM-dd')))
    $output.AddRange($payloadLines)
    $output.Add("'@")
    $output.Add($footer)

    $text = ($output -join "`r`n") + "`r`n"
    [System.IO.File]::WriteAllText($OutputFile, $text, (New-Object System.Text.UTF8Encoding($false)))

    $size = (Get-Item -LiteralPath $OutputFile).Length
    Write-Host ("Cikti      : {0} ({1:N0} KB)" -f $OutputFile, ($size / 1KB)) -ForegroundColor Green
}
finally {
    Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
}
