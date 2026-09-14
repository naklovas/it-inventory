# Runbook, Görev, Geri Dönüş Planı ve Senaryo

Bu doküman BookRunner'daki dört temel kavramı ve aralarındaki ilişkiyi anlatır:
**Runbook**, **Görev (Task)**, **Geri Dönüş Planı (Rollback)** ve **Senaryo (Scenario)**.

## 1. Genel bakış

Bir Runbook, sırayla yürütülen **görevlerden** oluşan bir çalışma/geçiş planıdır
(örn. bir sistem geçişi, bir değişiklik operasyonu). Normal akış tek bir görev
listesi olarak baştan sona ilerler. Ama gerçek operasyonlarda iki türlü sapma
gerekir:

- Bir görev **başarısız** olursa, işi eski haline döndürmek için önceden
  hazırlanmış bir **Geri Dönüş Planı**na geçilebilir.
- Duruma göre (bir görevin sonucuna bağlı olarak) ana akıştan **farklı bir
  koldan** (Senaryo) devam edilebilir; senaryo bittiğinde ana akışa geri
  dönülebilir.

Aşağıdaki diyagram bu dördünün birbirine nasıl bağlandığını gösterir:

```mermaid
flowchart TD
    subgraph ANA["Ana Runbook"]
        direction TB
        G1["Görev 1"] --> G2["Görev 2"] --> G3{{"Görev 3"}}
        G3 -->|"Başarılı: normal devam"| G4["Görev 4"]
        G4 --> G5["Görev 5"] --> G6{{"Görev 6"}}
        G6 -->|"Başarılı: normal devam"| G7["Görev 7"]
    end

    subgraph SA["Senaryo-A"]
        direction TB
        SA1["Görev 1"] --> SA2["Görev 2"] --> SA3["Görev 3"]
    end

    subgraph SB["Senaryo-B"]
        direction TB
        SB1["Görev 1"] --> SB2["Görev 2"] --> SB3["Görev 3"]
    end

    subgraph RB["Geri Dönüş Planı"]
        direction TB
        R1["Görev 1"] --> R2["Görev 2"] --> R3["Görev 3"] --> R4["Görev 4"]
    end

    G3 -->|"Senaryo-A'ya geç"| SA1
    SA3 -.->|"Senaryo bitince rejoin"| G4
    G6 -->|"Senaryo-B'ye geç"| SB1

    G3 -.->|"Başarısız olursa"| R1
    G6 -.->|"Başarısız olursa"| R1
    SA2 -.->|"Başarısız olursa"| R1

    classDef ana fill:#4F86F7,color:#fff,stroke:#2c5cbf;
    classDef senaryo fill:#8BC34A,color:#1b2e0a,stroke:#5a8f2a;
    classDef rollback fill:#9C6ADE,color:#fff,stroke:#6a3fae;
    class G1,G2,G3,G4,G5,G6,G7 ana;
    class SA1,SA2,SA3,SB1,SB2,SB3 senaryo;
    class R1,R2,R3,R4 rollback;
```

Kesikli oklar **otomatik tetiklenen** geçişleri (bir görevin sonucuna bağlı),
düz oklar ise **normal akışı / manuel geçişleri** gösterir.

---

## 2. Görev (Task)

Bir görev, runbook içindeki tek bir adımdır: başlık, sorumlu, planlanan
tarih/saat, öncül bağımlılıkları ve bir **durumu** vardır.

```mermaid
stateDiagram-v2
    [*] --> Baslamadi
    Baslamadi --> DevamEdiyor
    DevamEdiyor --> Tamamlandi
    DevamEdiyor --> Basarisiz
    DevamEdiyor --> Bloke
    Bloke --> DevamEdiyor
    Baslamadi --> Atlandi
    DevamEdiyor --> Atlandi
    Tamamlandi --> Baslamadi: Yonetici sifirlar
    Basarisiz --> Baslamadi: Yonetici sifirlar
    Basarisiz --> DevamEdiyor: Yonetici geri ceker

    Baslamadi: Başlamadı
    DevamEdiyor: Devam Ediyor
    Tamamlandi: Tamamlandı
    Basarisiz: Başarısız
    Bloke: Bloke
    Atlandi: Atlandı
```

> Not: "N/A - Gerekmiyor" durumu yalnızca geri dönüş adımlarında kullanılır
> (geri dönüşün başladığı noktaya göre bazı adımlara ihtiyaç olmayabilir).

### Görev bazında otomatik tetikleme

Her görev, kendi **sonucuna** göre otomatik bir eylem tanımlayabilir - operatör
ayrıca bir butona basmak zorunda kalmaz:

| Sonuç | Tanımlanabilecek otomatik eylem |
|---|---|
| **Başarısız** olursa | Geri Dönüş Planını otomatik başlat, **veya** belirli bir senaryoya otomatik geç |
| **Tamamlandı** (başarılı) olursa | Belirli bir senaryoya otomatik geç |

Bu, "Görevi Düzenle" ekranında veya (senaryo için) senaryo adımı eklerken
"bağlı görev + koşul" seçilerek tanımlanır.

---

## 3. Geri Dönüş Planı (Rollback)

Geri dönüş adımları, ana gövde listesinde **gizli** duran, ayrı bir alt
listedir. Bir görev başarısız olduğunda (elle "Geri Dönüş Adımlarını Başlat"
butonuyla ya da görev üzerinde tanımlı otomatik eylemle) **aktive edilir**:

```mermaid
sequenceDiagram
    participant Op as Operatör
    participant Task as Görev (ana akış)
    participant RB as Geri Dönüş Planı
    participant RB2 as Runbook

    Task->>Task: Durum -> Başarısız
    alt Görevde "Başarısız olursa: Geri dönüşü başlat" tanımlıysa
        Task-->>RB: Otomatik aktive et
    else Operatör manuel karar veriyor
        Op->>RB: "Geri Dönüş Adımlarını Başlat"
    end
    RB2->>RB2: IsRollbackActive = true
    RB2->>Task: Ana akıştaki açık (Bekliyor/Devam Eden/Bloke) görevler -> Atlandı
    RB2->>RB: Adımların planlanan tarihleri şimdiden itibaren hesaplanır
```

### Aktivasyon geri alınabilir

Tetikleyen koşul ortadan kalkarsa (örn. bir yönetici o görevi tekrar "Devam
Ediyor" veya "Başlamadı"ya çekerse ve akışta başka başarısız görev kalmazsa),
geri dönüş **otomatik olarak iptal edilir**. Ayrıca manuel iki iptal seçeneği
vardır:

```mermaid
flowchart LR
    A["Geri dönüş<br/>PASİF"] -->|"Bir görev Başarısız olur"| B["Geri dönüş<br/>AKTİF"]
    B -->|"Tetikleyen görev artık<br/>Başarısız değil (otomatik)"| A
    B -->|"'Yalnızca Geri Dönüşü İptal Et'<br/>(adımlar korunur, Başlamadı'ya döner)"| A
    B -->|"'Tüm Planı İptal Et'<br/>(adımlar kalıcı silinir)"| C["Geri dönüş planı yok"]
```

---

## 4. Senaryo (Scenario)

Senaryolar, ana akıştan **koşullu olarak dallanan** alternatif adım
gruplarıdır (örn. "Senaryo-A", "Senaryo-B"). Bir görev üzerinde tanımlanan
"bağlı görev + koşul" (başarılı/başarısız) ile otomatik tetiklenebilir, ya da
elle "X Senaryosuna Geç" butonuyla başlatılabilir.

```mermaid
flowchart TD
    T["Ana akıştaki görev"] -->|"Başarısız/Başarılı olursa<br/>(görev üzerinde tanımlı)"| S1["Senaryo adımları<br/>çalışır"]
    T -->|"veya elle"| Manuel["'X Senaryosuna Geç' butonu"]
    Manuel --> S1
    S1 --> Rejoin{"Rejoin noktası<br/>tanımlı mı?"}
    Rejoin -->|"Evet"| Devam["Ana akışta belirlenen<br/>görevden devam"]
    Rejoin -->|"Hayır"| Bitis["Runbook burada<br/>tamamlanır (tek yönlü)"]
```

Önemli noktalar:

- Senaryo seçildiğinde, **rejoin noktasından önceki** açık ana akış görevleri
  otomatik "Atlandı" olur; rejoin noktası ve sonrasına dokunulmaz.
- Senaryo **tüm adımlarıyla** tamamlandığında:
  - Rejoin noktası tanımlıysa, runbook o görevden **otomatik devam eder**.
  - Tanımlı değilse, senaryo tek yönlüdür ve runbook orada sona erer.
- Bir senaryo adımı da başarısız olursa, kendi üzerinde tanımlıysa Geri Dönüş
  Planını tetikleyebilir (görev bazında otomatik eylem senaryo/ana akış
  ayrımı yapmaz).

---

## 5. Üç mekanizmanın karşılaştırması

| | Nasıl tetiklenir | Ana akışa etkisi | Geri dönüşü |
|---|---|---|---|
| **Geri Dönüş Planı** | Bir görev "Başarısız" olunca (otomatik veya elle) | Açık görevler "Atlandı" olur | Otomatik (tetikleyen koşul kalkınca) veya elle iptal edilebilir |
| **Senaryo** | Bir görev "Başarılı/Başarısız" olunca (otomatik veya elle) | Rejoin noktasına kadarki açık görevler "Atlandı" olur | Rejoin noktası tanımlıysa senaryo bitince otomatik; değilse tek yönlü |

---

*Bu doküman, BookRunner uygulamasının `claude/bookrunner-runbook-app-57f25r`
dalındaki geri dönüş planı ve senaryo dallanması özelliklerini anlatır.*
