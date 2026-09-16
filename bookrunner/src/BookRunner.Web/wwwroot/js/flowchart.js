/* ==========================================================================
   BookRunner - runbook akis semasi (ayri sayfa).
   Ana akisi, senaryo dallanmalarini ve geri donus planini config.tasks'taki
   gercek verilerle mermaid.js kullanarak cizer. Runbook.js'ten ayri tutulur
   ki bu sayfa kendi basina, Details ekranindaki diger ozelliklere (SignalR,
   gorev duzenleme vb.) bagli olmadan calissin.
   ========================================================================== */
(function () {
    "use strict";

    const config = JSON.parse(document.getElementById("brFlowchartConfig").textContent);

    function escapeHtml(value) {
        const div = document.createElement("div");
        div.textContent = value == null ? "" : value;
        return div.innerHTML;
    }

    /** Oznitelik icine yazilacak degerler icin tirnaklari da kacisla yazar. */
    function escapeAttr(value) {
        return String(value == null ? "" : value)
            .replace(/&/g, "&amp;")
            .replace(/"/g, "&quot;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;");
    }

    /** Kisa sureli bildirim; sayfanin ustunde belirir. */
    function toast(message, type) {
        const holder = document.createElement("div");
        holder.className = "alert alert-" + (type || "info") + " position-fixed top-0 start-50 translate-middle-x mt-3 shadow";
        holder.style.zIndex = "2000";
        holder.textContent = message;
        document.body.appendChild(holder);
        setTimeout(() => holder.remove(), 6000);
    }

    function flowNodeId(taskId) {
        return "t" + String(taskId).replace(/-/g, "");
    }

    /**
     * Mermaid dugum/subgraph etiketleri "..." ile sarmalanir; escapeHtml() metin
     * dugumleri icin yeterlidir ama duz cift tirnagi KACISLAMAZ (HTML metin
     * icerigi icin gerekmez) - bu yuzden burada ayrica &quot;'a cevrilir, aksi
     * halde bir gorev basligindaki " karakteri Mermaid'in disaridaki tirnak
     * sinirini erken kapatip sozdizimi hatasi verir.
     */
    function flowEscapeText(value) {
        return escapeHtml(value).replace(/"/g, "&quot;");
    }

    /**
     * Bir gorevin aktif atamalarindan kisa ad + rozet HTML'i uretir. Fotograf
     * BURADA <img> olarak eklenmez: Mermaid'in htmlLabels olcum gecisi her
     * dugumu gercekten tarayiciya cizdirip olcer - onlarca gercek fotograf
     * URL'si (AD'den) es zamanli yuklenmeye calisilirsa bu olcum asamasi cok
     * yavaslayip buyuk runbook'larda arayuzu kilitleyebiliyor. Bunun yerine
     * hafif bir bas harf rozeti cizilir; gercek fotograf render bittikten
     * SONRA applyFlowchartPhotos() ile ayrica (engelleyici olmadan) yuklenir.
     */
    function flowAssigneeHtml(task) {
        const assignments = task.assignments || [];
        if (assignments.length === 0) {
            return "Atanmamis";
        }
        // Birden fazla atanan varsa hepsi gosterilir (yalnizca ilki degil) -
        // her biri kendi rozeti+adiyla, virgulle ayrilarak.
        return assignments.map((assignment) => {
            const name = flowEscapeText(assignment.name);
            const initials = flowEscapeText(assignment.initials || "?");
            const color = escapeAttr(assignment.avatarColor || "#7B8794");
            const badge = assignment.photoUrl
                ? "<span class='br-flow-avatar' data-photo-url='" + escapeAttr(assignment.photoUrl) +
                  "' style='background-color:" + color + "'>" + initials + "</span>"
                : (assignment.isGroup ? "\u{1F465} " : "\u{1F464} ");
            return badge + name;
        }).join(", ");
    }

    /**
     * mermaid.render() tamamlandiktan SONRA cagrilir: dugum etiketlerindeki bas
     * harf rozetlerine, varsa gercek fotograflari arka planda (engellemeden)
     * yukler. Bir fotograf yuklenemezse rozet oldugu gibi (bas harfli) kalir.
     */
    function applyFlowchartPhotos(container) {
        container.querySelectorAll(".br-flow-avatar[data-photo-url]").forEach((el) => {
            const url = el.getAttribute("data-photo-url");
            if (!url) {
                return;
            }
            const img = new Image();
            img.onload = () => {
                el.style.backgroundImage = "url('" + url + "')";
                el.style.backgroundSize = "cover";
                el.style.backgroundPosition = "center";
                el.textContent = "";
            };
            img.src = url;
        });
    }

    function flowNodeLabel(task) {
        return "\"<div class='br-flow-node'>" +
            "<b>" + task.order + ". " + flowEscapeText(task.title) + "</b><br/>" +
            "<span class='br-flow-name'>" + flowAssigneeHtml(task) + "</span><br/>" +
            "<span class='br-flow-status br-flow-status-" + task.status + "'>" + flowEscapeText(task.statusText) + "</span>" +
            "</div>\"";
    }

    /** config.tasks'taki gercek verilerle bir Mermaid flowchart tanimi uretir. */
    function buildFlowchartDefinition() {
        const tasks = config.tasks || [];
        const mainTasks = tasks.filter((t) => !t.isRollbackStep && !t.scenarioGroup).sort((a, b) => a.order - b.order);
        const rollbackSteps = tasks.filter((t) => t.isRollbackStep).sort((a, b) => a.order - b.order);

        const scenarioGroupNames = [];
        tasks.forEach((t) => {
            if (!t.isRollbackStep && t.scenarioGroup && scenarioGroupNames.indexOf(t.scenarioGroup) === -1) {
                scenarioGroupNames.push(t.scenarioGroup);
            }
        });
        const scenarioGroups = scenarioGroupNames.map((name) => ({
            name,
            tasks: tasks.filter((t) => !t.isRollbackStep && t.scenarioGroup === name).sort((a, b) => a.order - b.order)
        }));

        const lines = ["flowchart TD"];

        function renderChain(list) {
            list.forEach((t, i) => {
                lines.push("    " + flowNodeId(t.id) + "[" + flowNodeLabel(t) + "]");
                if (i > 0) {
                    lines.push("    " + flowNodeId(list[i - 1].id) + " --> " + flowNodeId(t.id));
                }
            });
        }

        if (mainTasks.length > 0) {
            lines.push("    subgraph ANA[\"Ana Akis\"]");
            lines.push("    direction TB");
            renderChain(mainTasks);
            lines.push("    end");
        }

        scenarioGroups.forEach((group, gi) => {
            const active = config.activeScenarioGroup === group.name ? " (AKTIF)" : "";
            lines.push("    subgraph SC" + gi + "[\"Senaryo: " + flowEscapeText(group.name) + active + "\"]");
            lines.push("    direction TB");
            renderChain(group.tasks);
            lines.push("    end");
        });

        if (rollbackSteps.length > 0) {
            const active = config.isRollbackActive ? " (AKTIF)" : "";
            lines.push("    subgraph RB[\"Geri Donus Plani" + active + "\"]");
            lines.push("    direction TB");
            renderChain(rollbackSteps);
            lines.push("    end");
        }

        // Tetikleme / rejoin oklari (kesikli - otomatik veya manuel gecisler)
        tasks.forEach((t) => {
            if (t.isRollbackStep) {
                return;
            }
            if (t.failureAction === "StartRollback" && rollbackSteps.length > 0) {
                lines.push("    " + flowNodeId(t.id) + " -.->|\"Basarisiz\"| " + flowNodeId(rollbackSteps[0].id));
            } else if (t.failureAction === "SwitchToScenario" && t.failureScenarioGroup) {
                const target = scenarioGroups.find((g) => g.name === t.failureScenarioGroup);
                if (target && target.tasks.length > 0) {
                    lines.push("    " + flowNodeId(t.id) + " -.->|\"Basarisiz\"| " + flowNodeId(target.tasks[0].id));
                }
            }
            if (t.successScenarioGroup) {
                const target = scenarioGroups.find((g) => g.name === t.successScenarioGroup);
                if (target && target.tasks.length > 0) {
                    lines.push("    " + flowNodeId(t.id) + " -.->|\"Basarili\"| " + flowNodeId(target.tasks[0].id));
                }
            }
        });

        scenarioGroups.forEach((group) => {
            const rejoinSource = group.tasks.find((t) => t.scenarioRejoinTaskId);
            if (rejoinSource) {
                const rejoinTarget = mainTasks.find((t) => t.id === rejoinSource.scenarioRejoinTaskId);
                const lastStep = group.tasks[group.tasks.length - 1];
                if (rejoinTarget && lastStep) {
                    lines.push("    " + flowNodeId(lastStep.id) + " -.->|\"Rejoin\"| " + flowNodeId(rejoinTarget.id));
                }
            }
        });

        lines.push("    classDef default fill:#F5F7FA,stroke:#334E68,color:#1F2933;");

        return lines.join("\n");
    }

    function loadMermaid() {
        if (window.mermaid) {
            return Promise.resolve();
        }
        return new Promise((resolve, reject) => {
            const script = document.createElement("script");
            // Sabit "/lib/..." yerine Razor'un urettigi adres kullanilir - uygulama
            // IIS altinda bir sanal dizinde (orn. /bookrunner/) calisiyorsa sabit
            // kok-goreli yol yanlis adrese (404) gider ve cizim hic baslamaz.
            script.src = config.mermaidUrl || "/lib/mermaid/mermaid.min.js";
            script.onload = resolve;
            script.onerror = () => reject(new Error("mermaid-script-load-failed: " + script.src));
            document.head.appendChild(script);
        });
    }

    /** N saniye icinde cozulmezse reddeden bir zaman asimi - cizim hicbir sekilde sonsuza kadar donmesin diye. */
    function withTimeout(promise, ms) {
        return Promise.race([
            promise,
            new Promise((_, reject) => setTimeout(() => reject(new Error("timeout")), ms))
        ]);
    }

    let flowchartRenderAttempt = 0;

    async function renderFlowchart() {
        const loading = document.getElementById("flowchartLoading");
        const scroll = document.getElementById("flowchartScroll");
        const container = document.getElementById("flowchartMermaid");

        try {
            await withTimeout(loadMermaid(), 15000);
            window.mermaid.initialize({
                startOnLoad: false,
                theme: "base",
                securityLevel: "loose",
                maxTextSize: 900000,
                maxEdges: 2000,
                flowchart: { htmlLabels: true, curve: "basis" }
            });
            const definition = buildFlowchartDefinition();
            // Ayni id'yi tekrar denemelerde yeniden kullanmamak icin sayac eklenir -
            // mermaid'in ic render onbelleginde eski bir kaydin kalmasi ihtimaline karsi.
            flowchartRenderAttempt += 1;
            const result = await withTimeout(
                window.mermaid.render("flowchartSvg" + flowchartRenderAttempt, definition), 20000);
            container.innerHTML = result.svg;
            applyFlowchartPhotos(container);
            // Not: gorunurluk .hidden ozniteligi yerine .d-none sinifiyla degistirilir -
            // flowchartLoading'in Bootstrap .d-flex sinifi "!important" ile display:flex
            // dayattigi icin native "hidden" ozniteligi gormezden geliniyordu; cember
            // hicbir zaman kaybolmuyordu (rapor edilen "sonsuz donuyor" sorununun
            // gercek sebebi buydu - cizim aslinda tamamlaniyordu).
            loading.classList.add("d-none");
            scroll.classList.remove("d-none");
        } catch (error) {
            loading.classList.add("d-none");
            console.error("Akis semasi cizim hatasi:", error);
            const detail = (error && error.message) || "bilinmeyen hata";
            const message = detail === "timeout"
                ? "Akis semasi cizimi zaman asimina ugradi (cok fazla gorev/dallanma olabilir)."
                : "Akis semasi cizilemedi: " + detail;
            toast(message, "danger");
        }
    }

    /**
     * Tanilama yardimcisi: bir runbook'ta cizim takilirsa, konsoldan
     * "copy(__brFlowchartDefinition())" calistirip uretilen Mermaid metnini
     * pano uzerinden paylasmak, sorunu yeniden uretmeyi cok kolaylastirir.
     */
    window.__brFlowchartDefinition = buildFlowchartDefinition;

    renderFlowchart();
})();
