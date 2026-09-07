/* ==========================================================================
   BookRunner - runbook detay ekrani.
   Tum istekler Web katmanindaki JSON uclarina gider; tarayici API'ye dogrudan
   erismez, boylece Kerberos ve CORS tek noktada yonetilir.
   ========================================================================== */
(function () {
    "use strict";

    const config = JSON.parse(document.getElementById("brConfig").textContent);
    const taskList = document.getElementById("taskList");

    // --------------------------------------------------------------- yardimci

    /**
     * Sunucudan gelen { ok, error, kind } yanitini bir Error'a cevirir.
     * "kind" GIRIS HATASI (input - kullanicinin duzeltebilecegi bir sorun,
     * orn. gecersiz tarih araligi/dongu olusturan bagimlilik) ile UYGULAMA
     * HATASI (application - sistemsel bir ariza) ayrimini tasir; error.kind
     * uzerinden showActionError() bu ayrimi hem metinde hem renkte gosterir -
     * boylece ekrani gorebilecek bir gelistirici de bunu uygulama arizasi sanmaz.
     */
    function toActionError(payload) {
        const kind = payload && payload.kind === "application" ? "application" : "input";
        const label = kind === "application" ? "Uygulama hatasi" : "Giris hatasi";
        const error = new Error(label + ": " + ((payload && payload.error) || "Islem basarisiz."));
        error.kind = kind;
        return error;
    }

    /** post()/get() catch bloklarinin ortak hata gosterimi: giris hatasi sari, uygulama hatasi kirmizi uyari olur. */
    function showActionError(error) {
        toast((error && error.message) || "Islem basarisiz.", error && error.kind === "application" ? "danger" : "warning");
    }

    /** Web katmanindaki JSON ucuna POST eder. */
    async function post(action, body, query) {
        const url = "/Runbooks/" + action + (query ? "?" + new URLSearchParams(query) : "");
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": config.antiforgery
            },
            body: body === undefined ? null : JSON.stringify(body)
        });

        const payload = await response.json().catch(() => ({ ok: false, error: "Yanit okunamadi.", kind: "application" }));
        if (!payload.ok) {
            throw toActionError(payload);
        }
        return payload.data;
    }

    async function get(action, query) {
        const url = "/Runbooks/" + action + (query ? "?" + new URLSearchParams(query) : "");
        const response = await fetch(url, { headers: { "Accept": "application/json" } });
        const payload = await response.json().catch(() => ({ ok: false, error: "Yanit okunamadi.", kind: "application" }));
        if (!payload.ok) {
            throw toActionError(payload);
        }
        return payload.data;
    }

    /** Kisa sureli bildirim; sayfanin ustunde belirir. */
    function toast(message, type) {
        const holder = document.createElement("div");
        holder.className = "alert alert-" + (type || "info") + " position-fixed top-0 start-50 translate-middle-x mt-3 shadow";
        holder.style.zIndex = "2000";
        holder.textContent = message;
        document.body.appendChild(holder);
        setTimeout(() => holder.remove(), 4000);
    }

    function reload() {
        window.location.reload();
    }

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

    /**
     * Sunucudaki _Avatar partial'i ile ayni yapiyi uretir: bas harfler rozetin
     * icinde durur, fotograf varsa uzerine biner ve yuklenemezse kaldirilir.
     */
    function avatarHtml(person, options) {
        const settings = options || {};
        const classes = ["br-avatar"];

        if (settings.size) {
            classes.push("br-avatar-" + settings.size);
        }
        if (settings.isGroup) {
            classes.push("br-avatar-group");
        }

        const label = person.displayName || person.name || "";
        const photo = (person.hasPhoto && person.id)
            ? '<img class="br-avatar-photo" src="/Home/Photo/' + encodeURIComponent(person.id) +
              '" alt="" loading="lazy" />'
            : "";

        return '<span class="' + classes.join(" ") + '" style="background:' + escapeAttr(person.avatarColor) +
            '" title="' + escapeAttr(label) + '">' +
            '<span class="br-avatar-initials">' + escapeHtml(person.initials) + "</span>" +
            photo + "</span>";
    }

    function toIsoOrNull(value) {
        return value ? new Date(value).toISOString() : null;
    }

    /** ISO/DateTimeOffset degerini datetime-local input'un bekledigi yerel "yyyy-MM-ddTHH:mm" bicimine cevirir. */
    function toLocalInputValue(iso) {
        if (!iso) {
            return "";
        }

        const d = new Date(iso);
        const pad = (n) => String(n).padStart(2, "0");
        return d.getFullYear() + "-" + pad(d.getMonth() + 1) + "-" + pad(d.getDate()) +
            "T" + pad(d.getHours()) + ":" + pad(d.getMinutes());
    }

    /**
     * Runbook'un planlanan araligi disinda bir deger secilmisse en yakin sinira
     * ceker. min/max HTML ozniteligi yalnizca tarayicinin KENDI tarih secicisine
     * ipucu verir; bu ekran native form submit KULLANMADIGINDAN (fetch ile JS
     * gonderiyor) tarayici bu siniri hicbir zaman zorunlu kilmaz - kullanici
     * araligin disinda bir deger yapistirabilir/yazabilir ve sunucuya kadar
     * gider (orada hata olarak reddedilir, ama gec ve dolayli bir geri bildirimdir).
     * "change" ile hemen sinira cekmek, secim aninda engellemeye en yakin davranistir.
     */
    function clampToPlannedRange(input) {
        if (!input.value || !config.plannedStart || !config.plannedEnd) {
            return;
        }

        const value = new Date(input.value).getTime();
        const min = new Date(config.plannedStart).getTime();
        const max = new Date(config.plannedEnd).getTime();

        if (value < min) {
            input.value = toLocalInputValue(config.plannedStart);
            toast("Gorev tarihi runbook'un planlanan baslangicindan once olamaz; baslangica ayarlandi.", "warning");
        } else if (value > max) {
            input.value = toLocalInputValue(config.plannedEnd);
            toast("Gorev tarihi runbook'un planlanan bitisinden sonra olamaz; bitise ayarlandi.", "warning");
        }
    }

    /** Gorev tarih secicilerine runbook'un planlanan araligini min/max olarak uygular ve zorunlu kilar. */
    function applyPlannedRangeConstraint(startInput, endInput) {
        if (!startInput || !endInput || !config.plannedStart || !config.plannedEnd) {
            return;
        }

        const min = toLocalInputValue(config.plannedStart);
        const max = toLocalInputValue(config.plannedEnd);
        startInput.min = min;
        startInput.max = max;
        endInput.min = min;
        endInput.max = max;

        [startInput, endInput].forEach((input) => {
            if (input.dataset.rangeClampWired) {
                return;
            }
            input.dataset.rangeClampWired = "true";
            input.addEventListener("change", () => clampToPlannedRange(input));
        });
    }

    applyPlannedRangeConstraint(document.getElementById("newTaskStart"), document.getElementById("newTaskEnd"));

    /** Secili oncul kimliklerinden en gec bitenin PlannedEnd degerini (ISO) dondurur. */
    function latestPredecessorEnd(selectedIds) {
        if (!selectedIds.length) {
            return null;
        }

        const ends = (config.tasks || [])
            .filter((t) => selectedIds.includes(t.id) && t.plannedEnd)
            .map((t) => new Date(t.plannedEnd).getTime());

        return ends.length ? new Date(Math.max(...ends)).toISOString() : null;
    }

    /** Sure (dk) alanini planlanan baslangic/bitis farkindan hesaplar (kullanici sonradan degistirebilir). */
    function applyDurationFromRange(startInput, endInput, minutesInput) {
        if (!startInput || !endInput || !minutesInput || !startInput.value || !endInput.value) {
            return;
        }

        const start = new Date(startInput.value).getTime();
        const end = new Date(endInput.value).getTime();
        if (end > start) {
            minutesInput.value = Math.round((end - start) / 60000);
        }
    }

    /**
     * Oncul secim kutusu (yeni gorev paneli / gorev duzenle modali) ile
     * planlanan baslangic-bitis-sure alanlari arasindaki otomatik doldurmayi
     * kurar. Oncul kutusu "change" olayini dinler (delege) ki duzenle
     * modalindeki checkbox'lar her acilista yeniden cizilse de calismaya
     * devam etsin.
     */
    function wireTaskDateAutoFill(dependenciesContainerId, startInputId, endInputId, minutesInputId) {
        const startInput = document.getElementById(startInputId);
        const endInput = document.getElementById(endInputId);
        const minutesInput = document.getElementById(minutesInputId);

        const dependenciesContainer = document.getElementById(dependenciesContainerId);
        if (dependenciesContainer) {
            dependenciesContainer.addEventListener("change", () => {
                const selectedIds = Array.from(dependenciesContainer.querySelectorAll("input:checked")).map((el) => el.value);
                const latestEnd = latestPredecessorEnd(selectedIds);
                if (latestEnd && startInput && !startInput.disabled) {
                    startInput.value = toLocalInputValue(latestEnd);
                }
                applyDurationFromRange(startInput, endInput, minutesInput);
            });
        }

        [startInput, endInput].forEach((input) => {
            if (input) {
                input.addEventListener("change", () => applyDurationFromRange(startInput, endInput, minutesInput));
            }
        });
    }

    wireTaskDateAutoFill("newTaskDependencies", "newTaskStart", "newTaskEnd", "newTaskMinutes");
    wireTaskDateAutoFill("editTaskDependencies", "editTaskStart", "editTaskEnd", "editTaskMinutes");

    /** "Kesintili adim" checkbox'i isaretlenince planlanan kesinti alanini gosterir/gizler. */
    function wireOutageToggle(checkboxId, wrapId) {
        const checkbox = document.getElementById(checkboxId);
        const wrap = document.getElementById(wrapId);
        if (!checkbox || !wrap) {
            return;
        }

        checkbox.addEventListener("change", () => { wrap.hidden = !checkbox.checked; });
    }

    wireOutageToggle("newTaskOutage", "newTaskOutageMinutesWrap");
    wireOutageToggle("editTaskOutage", "editTaskOutageMinutesWrap");

    // ------------------------------------------------------------ gorev ekleme

    const addTaskButton = document.getElementById("btnAddTask");
    if (addTaskButton) {
        addTaskButton.addEventListener("click", async () => {
            const title = document.getElementById("newTaskTitle").value.trim();
            if (title.length < 2) {
                toast("Gorev basligi en az 2 karakter olmali.", "warning");
                return;
            }

            const minutes = document.getElementById("newTaskMinutes").value;
            const isOutage = document.getElementById("newTaskOutage").checked;
            const outageMinutes = document.getElementById("newTaskOutageMinutes").value;
            const dependsOnTaskIds = Array.from(document.querySelectorAll(".br-new-task-depends:checked"))
                .map((el) => el.value);

            try {
                const created = await post("AddTask", {
                    title: title,
                    description: document.getElementById("newTaskDescription").value || null,
                    colorHex: document.getElementById("newTaskColor").value,
                    priority: document.getElementById("newTaskPriority").value,
                    estimatedMinutes: minutes ? parseInt(minutes, 10) : null,
                    plannedStart: toIsoOrNull(document.getElementById("newTaskStart").value),
                    plannedEnd: toIsoOrNull(document.getElementById("newTaskEnd").value),
                    dependsOnTaskIds: dependsOnTaskIds,
                    rollbackNotes: document.getElementById("newTaskRollback").value || null,
                    isOutageStep: isOutage,
                    plannedOutageMinutes: isOutage && outageMinutes ? parseInt(outageMinutes, 10) : null
                }, { id: config.runbookId });

                if (selection.newTask.length > 0) {
                    // Tum sorumlular tek istekte atanir; boylece kac kisi/takim
                    // secilirse secilsin TEK bir bildirim e-postasi gider.
                    const requests = selection.newTask.map((assignee) => ({
                        assigneeType: assignee.kind === "user" ? "User" : "Group",
                        userId: assignee.kind === "user" ? assignee.id : null,
                        groupId: assignee.kind === "group" ? assignee.id : null,
                        note: null,
                        notify: true
                    }));

                    try {
                        await post("AssignBatch", requests, { taskId: created.id });
                    } catch (assignError) {
                        toast("Gorev olusturuldu ama atama yapilamadi: " + assignError.message, "warning");
                    }
                }

                selection.newTask = [];
                reload();
            } catch (error) {
                showActionError(error);
            }
        });
    }

    // ------------------------------------------------------------- geri donus plani

    const addRollbackStepButton = document.getElementById("btnAddRollbackStep");
    if (addRollbackStepButton) {
        addRollbackStepButton.addEventListener("click", async () => {
            const title = document.getElementById("rollbackStepTitle").value.trim();
            if (title.length < 2) {
                toast("Adim basligi en az 2 karakter olmali.", "warning");
                return;
            }

            const minutes = document.getElementById("rollbackStepMinutes").value;

            try {
                await post("AddTask", {
                    title: title,
                    description: document.getElementById("rollbackStepDescription").value || null,
                    priority: document.getElementById("rollbackStepPriority").value,
                    estimatedMinutes: minutes ? parseInt(minutes, 10) : null,
                    isRollbackStep: true
                }, { id: config.runbookId });

                reload();
            } catch (error) {
                showActionError(error);
            }
        });
    }

    const startRollbackButton = document.getElementById("btnStartRollback");
    if (startRollbackButton) {
        startRollbackButton.addEventListener("click", async () => {
            if (!confirm("Geri donus plani baslatilacak: ana akistaki kapanmamis adimlar 'Atlandi' " +
                "olarak isaretlenecek ve geri donus adimlarinin tarihleri hesaplanacak. Bu islem geri alinamaz. Emin misiniz?")) {
                return;
            }

            try {
                await post("StartRollback", undefined, { id: config.runbookId });
                reload();
            } catch (error) {
                showActionError(error);
            }
        });
    }

    // -------------------------------------------------------------- senaryo plani

    const addScenarioStepButton = document.getElementById("btnAddScenarioStep");
    if (addScenarioStepButton) {
        addScenarioStepButton.addEventListener("click", async () => {
            const scenarioGroup = document.getElementById("scenarioStepGroup").value.trim();
            const title = document.getElementById("scenarioStepTitle").value.trim();
            if (scenarioGroup.length < 1) {
                toast("Senaryo adi girilmeli.", "warning");
                return;
            }
            if (title.length < 2) {
                toast("Adim basligi en az 2 karakter olmali.", "warning");
                return;
            }

            const minutes = document.getElementById("scenarioStepMinutes").value;

            try {
                await post("AddTask", {
                    title: title,
                    description: document.getElementById("scenarioStepDescription").value || null,
                    priority: document.getElementById("scenarioStepPriority").value,
                    estimatedMinutes: minutes ? parseInt(minutes, 10) : null,
                    scenarioGroup: scenarioGroup
                }, { id: config.runbookId });

                reload();
            } catch (error) {
                showActionError(error);
            }
        });
    }

    document.querySelectorAll(".br-switch-scenario-btn").forEach((button) => {
        button.addEventListener("click", async () => {
            const scenarioGroup = button.dataset.scenarioGroup;
            if (!confirm(`"${scenarioGroup}" senaryosuna gecilecek: ana akistaki kapanmamis adimlar 'Atlandi' ` +
                "olarak isaretlenecek ve senaryo adimlarinin tarihleri hesaplanacak. Bu islem geri alinamaz. Emin misiniz?")) {
                return;
            }

            try {
                await post("SwitchScenario", undefined, { id: config.runbookId, scenarioGroup: scenarioGroup });
                reload();
            } catch (error) {
                showActionError(error);
            }
        });
    });

    // ------------------------------------------------------------ gorev tamamlama

    const completeTaskModalElement = document.getElementById("completeTaskModal");
    const completeTaskModal = completeTaskModalElement ? new bootstrap.Modal(completeTaskModalElement) : null;

    const confirmCompleteTaskButton = document.getElementById("btnConfirmCompleteTask");
    if (confirmCompleteTaskButton) {
        confirmCompleteTaskButton.addEventListener("click", async () => {
            const minutes = document.getElementById("completeTaskMinutes").value;
            const outageMinutes = document.getElementById("completeTaskOutageMinutes").value;
            const note = document.getElementById("completeTaskNote").value.trim();

            // URLSearchParams her degeri String()'e cevirir - "undefined" gibi
            // gecersiz bir metin gondermemek icin bos alanlar sozlukten tamamen cikarilir.
            const query = { taskId: document.getElementById("completeTaskId").value, status: "Completed" };
            if (note) {
                query.note = note;
            }
            if (minutes) {
                query.actualMinutes = parseInt(minutes, 10);
            }
            if (outageMinutes) {
                query.actualOutageMinutes = parseInt(outageMinutes, 10);
            }

            try {
                await post("ChangeTaskStatus", undefined, query);
                completeTaskModal.hide();
                reload();
            } catch (error) {
                showActionError(error);
            }
        });
    }

    // ------------------------------------------------------------ gorev duzenleme

    const editTaskModalElement = document.getElementById("editTaskModal");
    const editTaskModal = editTaskModalElement ? new bootstrap.Modal(editTaskModalElement) : null;

    /** Duzenlenen gorevin oncul listesini config.tasks'tan cizer (kendisi haric). */
    function renderEditTaskDependencies(task) {
        const holder = document.getElementById("editTaskDependencies");
        if (!holder) {
            return;
        }

        // Ana akis, geri donus adimlari ve her senaryo grubu birbirinden ayri
        // bagimlilik graflarina sahiptir; oncul secenekleri yalnizca ayni gruptan gelir.
        const others = (config.tasks || [])
            .filter((item) => item.id !== task.id
                && !!item.isRollbackStep === !!task.isRollbackStep
                && (item.scenarioGroup || null) === (task.scenarioGroup || null));
        if (others.length === 0) {
            holder.innerHTML = '<div class="br-muted small">Runbook\'ta baska gorev yok.</div>';
            return;
        }

        const current = new Set(task.dependsOnTaskIds || []);
        holder.innerHTML = others.map((item) => {
            const checked = current.has(item.id) ? " checked" : "";
            return '<div class="form-check">' +
                '<input class="form-check-input br-edit-task-depends" type="checkbox" value="' + item.id + '"' +
                ' id="editTaskDep-' + item.id + '"' + checked + ' />' +
                '<label class="form-check-label small" for="editTaskDep-' + item.id + '">' +
                escapeHtml(item.order + ". " + item.title) + "</label></div>";
        }).join("");
    }

    document.querySelectorAll(".br-edit-task-btn").forEach((button) => {
        button.addEventListener("click", () => {
            const task = (config.tasks || []).find((item) => item.id === button.dataset.taskId);
            if (!task || !editTaskModal) {
                return;
            }

            document.getElementById("editTaskId").value = task.id;
            document.getElementById("editTaskScriptId").value = task.scriptId || "";
            document.getElementById("editTaskTitle").value = task.title;
            document.getElementById("editTaskDescription").value = task.description || "";
            document.getElementById("editTaskPriority").value = task.priority;
            document.getElementById("editTaskMinutes").value = task.estimatedMinutes || "";
            document.getElementById("editTaskRollback").value = task.rollbackNotes || "";

            const outageCheckbox = document.getElementById("editTaskOutage");
            const outageWrap = document.getElementById("editTaskOutageMinutesWrap");
            outageCheckbox.checked = !!task.isOutageStep;
            outageWrap.hidden = !task.isOutageStep;
            document.getElementById("editTaskOutageMinutes").value = task.plannedOutageMinutes || "";

            const startInput = document.getElementById("editTaskStart");
            const endInput = document.getElementById("editTaskEnd");
            startInput.value = toLocalInputValue(task.plannedStart);
            endInput.value = toLocalInputValue(task.plannedEnd);
            applyPlannedRangeConstraint(startInput, endInput);

            renderEditTaskDependencies(task);
            editTaskModal.show();
        });
    });

    const saveTaskEditButton = document.getElementById("btnSaveTaskEdit");
    if (saveTaskEditButton) {
        saveTaskEditButton.addEventListener("click", async () => {
            const title = document.getElementById("editTaskTitle").value.trim();
            if (title.length < 2) {
                toast("Gorev basligi en az 2 karakter olmali.", "warning");
                return;
            }

            const minutes = document.getElementById("editTaskMinutes").value;
            const isOutage = document.getElementById("editTaskOutage").checked;
            const outageMinutes = document.getElementById("editTaskOutageMinutes").value;
            const dependsOnTaskIds = Array.from(document.querySelectorAll(".br-edit-task-depends:checked"))
                .map((el) => el.value);

            // UpdateTask butun alanlari degistirir; bu form geri donus bayragini/
            // senaryo grubunu gostermez, o yuzden mevcut degerleri config.tasks'tan
            // korunarak gonderilir (aksi halde duzenlenince sessizce silinir).
            const editingTask = (config.tasks || []).find((item) => item.id === document.getElementById("editTaskId").value);
            const isRollbackStep = !!(editingTask && editingTask.isRollbackStep);
            const scenarioGroup = editingTask ? editingTask.scenarioGroup || null : null;

            try {
                await post("UpdateTask", {
                    title: title,
                    description: document.getElementById("editTaskDescription").value || null,
                    priority: document.getElementById("editTaskPriority").value,
                    estimatedMinutes: minutes ? parseInt(minutes, 10) : null,
                    plannedStart: toIsoOrNull(document.getElementById("editTaskStart").value),
                    plannedEnd: toIsoOrNull(document.getElementById("editTaskEnd").value),
                    dependsOnTaskIds: dependsOnTaskIds,
                    rollbackNotes: document.getElementById("editTaskRollback").value || null,
                    scriptId: document.getElementById("editTaskScriptId").value || null,
                    isOutageStep: isOutage,
                    plannedOutageMinutes: isOutage && outageMinutes ? parseInt(outageMinutes, 10) : null,
                    isRollbackStep: isRollbackStep,
                    scenarioGroup: scenarioGroup
                }, { taskId: document.getElementById("editTaskId").value });

                editTaskModal.hide();
                reload();
            } catch (error) {
                showActionError(error);
            }
        });
    }

    // ------------------------------------------------------------ durum / silme

    document.addEventListener("click", async (event) => {
        const statusButton = event.target.closest(".br-status-btn");
        if (statusButton) {
            // "Tamamlandi" gercek sure/not istedigi icin once bir modal acilir;
            // diger durum degisiklikleri (Devam ediyor, Bloke, vb.) hemen gonderilir.
            if (statusButton.dataset.status === "Completed" && completeTaskModal) {
                const task = (config.tasks || []).find((item) => item.id === statusButton.dataset.taskId);
                document.getElementById("completeTaskId").value = statusButton.dataset.taskId;
                document.getElementById("completeTaskMinutes").value = "";
                document.getElementById("completeTaskNote").value = "";
                document.getElementById("completeTaskOutageMinutes").value = "";
                document.getElementById("completeTaskOutageWrap").hidden = !(task && task.isOutageStep);
                completeTaskModal.show();
                return;
            }

            // "Baslamadi"ya sifirlama gerceklesen tarih/sure/notu siler - geri
            // alinamaz, bu yuzden ayrica onay istenir.
            if (statusButton.dataset.status === "NotStarted" &&
                !confirm("Gorev baslangic durumuna sifirlanacak; gerceklesen tarih/sure ve tamamlama notu silinecek. Emin misiniz?")) {
                return;
            }

            try {
                await post("ChangeTaskStatus", undefined, {
                    taskId: statusButton.dataset.taskId,
                    status: statusButton.dataset.status
                });
                reload();
            } catch (error) {
                showActionError(error);
            }
            return;
        }

        const deleteButton = event.target.closest(".br-delete-task-btn");
        if (deleteButton) {
            if (!confirm("Bu gorev silinecek. Emin misiniz?")) {
                return;
            }
            try {
                await post("DeleteTask", undefined, { taskId: deleteButton.dataset.taskId });
                reload();
            } catch (error) {
                showActionError(error);
            }
            return;
        }

        const unassignButton = event.target.closest(".br-unassign-btn");
        if (unassignButton) {
            if (!confirm("Bu atama kaldirilacak. Emin misiniz?")) {
                return;
            }
            try {
                await post("RemoveAssignment", undefined, {
                    taskId: unassignButton.dataset.taskId,
                    assignmentId: unassignButton.dataset.assignmentId
                });
                reload();
            } catch (error) {
                showActionError(error);
            }
            return;
        }

        const removeCollaboratorButton = event.target.closest(".br-remove-collaborator-btn");
        if (removeCollaboratorButton) {
            if (!confirm("Bu editor kaldirilacak. Emin misiniz?")) {
                return;
            }
            try {
                await post("RemoveCollaborator", undefined, {
                    id: config.runbookId,
                    collaboratorId: removeCollaboratorButton.dataset.collaboratorId
                });
                reload();
            } catch (error) {
                showActionError(error);
            }
            return;
        }

        const scriptButton = event.target.closest(".br-script-btn");
        if (scriptButton) {
            try {
                const result = await post("RunScript", undefined, {
                    scriptId: scriptButton.dataset.scriptId,
                    taskId: scriptButton.dataset.taskId
                });
                toast("Script sonucu: " + result.status, result.status === "Succeeded" ? "success" : "warning");
            } catch (error) {
                showActionError(error);
            }
        }
    });

    // ---------------------------------------------------------------- yorumlar

    async function submitComment(taskId, input) {
        const body = input.value.trim();
        if (!body) {
            return;
        }

        try {
            const comment = await post("AddComment", { body: body }, { taskId: taskId });
            input.value = "";
            appendComment(taskId, comment);
        } catch (error) {
            showActionError(error);
        }
    }

    /** Yeni yorumu sayfayi yenilemeden listeye ekler. */
    function appendComment(taskId, comment) {
        const container = document.querySelector('[data-comments-for="' + taskId + '"]');
        if (!container || !comment || container.querySelector('[data-comment-id="' + comment.id + '"]')) {
            return;
        }

        const avatar = avatarHtml(comment.author, { size: "sm" });

        const element = document.createElement("div");
        element.className = "br-comment";
        element.dataset.commentId = comment.id;
        element.innerHTML = avatar +
            '<div class="br-comment-bubble">' +
            '  <div class="br-comment-meta"><strong class="text-body">' + escapeHtml(comment.author.displayName) +
            '  </strong> &middot; ' + new Date(comment.createdAt).toLocaleString("tr-TR") + '</div>' +
            '  <div style="white-space:pre-wrap;">' + escapeHtml(comment.body) + '</div>' +
            '</div>';

        container.appendChild(element);
    }

    document.querySelectorAll(".br-comment-input").forEach((input) => {
        input.addEventListener("keydown", (event) => {
            if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                submitComment(input.dataset.taskId, input);
            }
        });
    });

    document.querySelectorAll(".br-comment-btn").forEach((button) => {
        button.addEventListener("click", () => {
            const input = document.querySelector('.br-comment-input[data-task-id="' + button.dataset.taskId + '"]');
            if (input) {
                submitComment(button.dataset.taskId, input);
            }
        });
    });

    // ----------------------------------------------------------------- tarihce

    /** Akordiyon acildiginda tarihce sunucudan cekilir. */
    document.querySelectorAll('[id^="task-body-"]').forEach((panel) => {
        panel.addEventListener("show.bs.collapse", async () => {
            const holder = panel.querySelector("[data-history-for]");
            if (!holder || holder.dataset.loaded === "true") {
                return;
            }

            try {
                const activities = await get("TaskHistory", { taskId: panel.dataset.taskId });
                holder.dataset.loaded = "true";
                holder.innerHTML = (activities && activities.length)
                    ? activities.map(renderActivity).join("")
                    : '<div class="br-muted small">Henuz kayit yok.</div>';
            } catch (error) {
                holder.innerHTML = '<div class="text-danger small">' + escapeHtml(error.message) + "</div>";
            }
        });
    });

    function renderActivity(activity) {
        return '<div class="br-history-item">' +
            '  <div class="small"><strong>' + escapeHtml(activity.actorDisplayName) + "</strong> " +
            escapeHtml(activity.summary) + "</div>" +
            '  <div class="br-muted" style="font-size:.72rem;">' +
            new Date(activity.createdAt).toLocaleString("tr-TR") + " &middot; " + escapeHtml(activity.typeText) +
            "</div></div>";
    }

    // ------------------------------------------------------- AD arama kutulari

    /** Secilen kisi/grup; atama, devir ve yeni gorev formunda paylasilir. */
    const selection = { assign: null, handover: null, newTask: [], collaborator: null };

    /** Toplu atama/devir icin secili gorev kimlikleri; bos ise ata/devir modallari tekli gorev modunda calisir. */
    let bulkTaskIds = [];

    /** Yeni gorev formundaki secili sorumlu "cip"lerini cizer. */
    function renderNewTaskAssigneeChips() {
        const container = document.getElementById("newTaskAssigneeChips");
        if (!container) {
            return;
        }

        container.innerHTML = selection.newTask.map((item, index) => {
            const icon = item.kind === "user" ? "bi-person" : "bi-people";
            return '<span class="badge text-bg-light border d-inline-flex align-items-center gap-1" data-index="' + index + '">' +
                '<i class="bi ' + icon + '"></i>' + escapeHtml(item.label) +
                '<button type="button" class="btn-close btn-close-sm br-remove-new-task-assignee" ' +
                'style="font-size:.6rem;" data-index="' + index + '" aria-label="Kaldir"></button></span>';
        }).join("");
    }

    document.addEventListener("click", (event) => {
        const removeChip = event.target.closest(".br-remove-new-task-assignee");
        if (removeChip) {
            selection.newTask.splice(parseInt(removeChip.dataset.index, 10), 1);
            renderNewTaskAssigneeChips();
        }
    });

    function wireSearch(inputId, suggestId, kind, scope) {
        const input = document.getElementById(inputId);
        const suggest = document.getElementById(suggestId);
        if (!input || !suggest) {
            return;
        }

        let timer = null;

        input.addEventListener("input", () => {
            clearTimeout(timer);
            const term = input.value.trim();

            if (term.length < 2) {
                suggest.classList.remove("show");
                return;
            }

            // Her tusa basista AD'ye gitmemek icin kisa bir gecikme uygulanir.
            timer = setTimeout(async () => {
                try {
                    const action = kind === "user" ? "SearchUsers" : "SearchGroups";
                    const items = await get(action, { term: term });
                    renderSuggestions(items || []);
                } catch (error) {
                    suggest.innerHTML = '<div class="p-2 small text-danger">' + escapeHtml(error.message) + "</div>";
                    suggest.classList.add("show");
                }
            }, 300);
        });

        function renderSuggestions(items) {
            if (!items.length) {
                suggest.innerHTML = '<div class="p-2 small br-muted">Sonuc bulunamadi.</div>';
                suggest.classList.add("show");
                return;
            }

            suggest.innerHTML = items.map((item) => {
                const label = kind === "user" ? item.displayName : (item.displayName || item.name);
                const subtitle = kind === "user"
                    ? [item.title, item.department].filter(Boolean).join(" - ")
                    : "Takim";

                const avatar = avatarHtml(item, { size: "sm", isGroup: kind === "group" });

                return '<div class="br-suggest-item" data-id="' + item.id + '" data-label="' + escapeHtml(label) + '">' +
                    avatar + "<span><span>" + escapeHtml(label) + "</span>" +
                    '<small class="d-block br-muted">' + escapeHtml(subtitle) + "</small></span></div>";
            }).join("");

            suggest.classList.add("show");
        }

        suggest.addEventListener("click", (event) => {
            const item = event.target.closest(".br-suggest-item");
            if (!item) {
                return;
            }

            const picked = { kind: kind, id: item.dataset.id, label: item.dataset.label };
            suggest.classList.remove("show");

            if (scope === "newTask") {
                // Birden fazla sorumlu eklenebilir: secim listeye eklenir, gorev
                // "Ekle" ile olusturulunca hepsine sirayla atama yapilir.
                if (!selection.newTask.some((existing) => existing.kind === picked.kind && existing.id === picked.id)) {
                    selection.newTask.push(picked);
                    renderNewTaskAssigneeChips();
                }
                input.value = "";
                return;
            }

            selection[scope] = picked;
            input.value = item.dataset.label;

            if (scope === "assign") {
                confirmAssign();
            } else if (scope === "handover") {
                confirmHandover();
            } else if (scope === "collaborator") {
                confirmAddCollaborator();
            }
        });

        document.addEventListener("click", (event) => {
            if (!suggest.contains(event.target) && event.target !== input) {
                suggest.classList.remove("show");
            }
        });
    }

    wireSearch("assignUserSearch", "assignUserSuggest", "user", "assign");
    wireSearch("assignGroupSearch", "assignGroupSuggest", "group", "assign");
    wireSearch("handoverUserSearch", "handoverUserSuggest", "user", "handover");
    wireSearch("handoverGroupSearch", "handoverGroupSuggest", "group", "handover");
    wireSearch("newTaskAssigneeUserSearch", "newTaskAssigneeUserSuggest", "user", "newTask");
    wireSearch("newTaskAssigneeGroupSearch", "newTaskAssigneeGroupSuggest", "group", "newTask");
    wireSearch("collaboratorSearch", "collaboratorSuggest", "user", "collaborator");

    const newTaskPanel = document.getElementById("newTaskPanel");
    if (newTaskPanel) {
        newTaskPanel.addEventListener("hidden.bs.collapse", () => {
            selection.newTask = [];
            renderNewTaskAssigneeChips();
            document.querySelectorAll(".br-new-task-depends:checked").forEach((el) => { el.checked = false; });
            document.getElementById("newTaskOutage").checked = false;
            document.getElementById("newTaskOutageMinutesWrap").hidden = true;
        });
    }

    // -------------------------------------------------------------- atama

    const assignModalElement = document.getElementById("assignModal");
    const assignModal = assignModalElement ? new bootstrap.Modal(assignModalElement) : null;

    document.querySelectorAll(".br-assign-btn").forEach((button) => {
        button.addEventListener("click", () => {
            bulkTaskIds = [];
            selection.assign = null;
            document.getElementById("assignTaskId").value = button.dataset.taskId;
            document.getElementById("assignUserSearch").value = "";
            document.getElementById("assignGroupSearch").value = "";
            document.getElementById("assignNote").value = "";
            assignModal.show();
        });
    });

    async function confirmAssign() {
        const target = selection.assign;
        if (!target) {
            return;
        }

        const payload = {
            assigneeType: target.kind === "user" ? "User" : "Group",
            userId: target.kind === "user" ? target.id : null,
            groupId: target.kind === "group" ? target.id : null,
            note: document.getElementById("assignNote").value || null,
            notify: document.getElementById("assignNotify").checked
        };

        try {
            if (bulkTaskIds.length > 0) {
                const results = await Promise.allSettled(
                    bulkTaskIds.map((id) => post("Assign", payload, { taskId: id })));
                const failed = results.filter((r) => r.status === "rejected").length;
                if (failed > 0) {
                    toast(failed + " goreve atama yapilamadi.", "warning");
                }
                bulkTaskIds = [];
            } else {
                await post("Assign", payload, { taskId: document.getElementById("assignTaskId").value });
            }

            assignModal.hide();
            reload();
        } catch (error) {
            showActionError(error);
        }
    }

    // -------------------------------------------------------------- devir

    const handoverModalElement = document.getElementById("handoverModal");
    const handoverModal = handoverModalElement ? new bootstrap.Modal(handoverModalElement) : null;

    document.querySelectorAll(".br-handover-btn").forEach((button) => {
        button.addEventListener("click", () => {
            bulkTaskIds = [];
            selection.handover = null;
            document.getElementById("handoverTaskId").value = button.dataset.taskId;
            document.getElementById("handoverAssignmentId").value = button.dataset.assignmentId;
            document.getElementById("handoverFromText").textContent =
                "Devreden: " + button.dataset.from;
            document.getElementById("handoverUserSearch").value = "";
            document.getElementById("handoverGroupSearch").value = "";
            handoverModal.show();
        });
    });

    async function confirmHandover() {
        const target = selection.handover;
        if (!target) {
            return;
        }

        const note = document.getElementById("handoverNote").value.trim();
        if (note.length < 3) {
            toast("Devir notu en az 3 karakter olmali.", "warning");
            return;
        }

        const targetPayload = {
            targetType: target.kind === "user" ? "User" : "Group",
            targetUserId: target.kind === "user" ? target.id : null,
            targetGroupId: target.kind === "group" ? target.id : null,
            note: note
        };

        try {
            if (bulkTaskIds.length > 0) {
                const calls = [];
                bulkTaskIds.forEach((id) => {
                    const task = (config.tasks || []).find((item) => item.id === id);
                    (task && task.assignmentIds ? task.assignmentIds : []).forEach((assignmentId) => {
                        calls.push(post("Handover",
                            Object.assign({ fromAssignmentId: assignmentId }, targetPayload),
                            { taskId: id }));
                    });
                });

                if (calls.length === 0) {
                    toast("Secili gorevlerin aktif atamasi yok, devredilecek bir sey bulunamadi.", "warning");
                    bulkTaskIds = [];
                    return;
                }

                const results = await Promise.allSettled(calls);
                const failed = results.filter((r) => r.status === "rejected").length;
                if (failed > 0) {
                    toast(failed + " devir islemi basarisiz oldu.", "warning");
                }
                bulkTaskIds = [];
            } else {
                await post("Handover",
                    Object.assign({ fromAssignmentId: document.getElementById("handoverAssignmentId").value }, targetPayload),
                    { taskId: document.getElementById("handoverTaskId").value });
            }

            handoverModal.hide();
            reload();
        } catch (error) {
            showActionError(error);
        }
    }

    // -------------------------------------------------------------- toplu islem

    function selectedBulkTaskIds() {
        return Array.from(document.querySelectorAll(".br-task-select:checked")).map((el) => el.dataset.taskId);
    }

    function updateBulkToolbar() {
        const toolbar = document.getElementById("bulkTaskToolbar");
        if (!toolbar) {
            return;
        }

        const ids = selectedBulkTaskIds();
        const countEl = document.getElementById("bulkTaskCount");
        if (countEl) {
            countEl.textContent = String(ids.length);
        }

        toolbar.classList.toggle("d-none", ids.length === 0);
        toolbar.classList.toggle("d-flex", ids.length > 0);
    }

    if (taskList) {
        taskList.addEventListener("change", (event) => {
            if (event.target.classList.contains("br-task-select")) {
                updateBulkToolbar();
            }
        });
    }

    const btnBulkClear = document.getElementById("btnBulkClear");
    if (btnBulkClear) {
        btnBulkClear.addEventListener("click", () => {
            document.querySelectorAll(".br-task-select:checked").forEach((el) => { el.checked = false; });
            updateBulkToolbar();
        });
    }

    const btnBulkDelete = document.getElementById("btnBulkDelete");
    if (btnBulkDelete) {
        btnBulkDelete.addEventListener("click", async () => {
            const ids = selectedBulkTaskIds();
            if (!ids.length || !confirm(ids.length + " gorev silinecek. Emin misiniz?")) {
                return;
            }

            const results = await Promise.allSettled(ids.map((id) => post("DeleteTask", undefined, { taskId: id })));
            const failed = results.filter((r) => r.status === "rejected").length;
            if (failed > 0) {
                toast(failed + " gorev silinemedi.", "warning");
            }
            reload();
        });
    }

    const btnBulkColor = document.getElementById("btnBulkColor");
    if (btnBulkColor) {
        btnBulkColor.addEventListener("click", async () => {
            const ids = selectedBulkTaskIds();
            if (!ids.length) {
                return;
            }

            const color = document.getElementById("bulkColorInput").value;

            // UpdateTask butun alanlari degistirir; bu yuzden her gorevin mevcut
            // verisi config.tasks'tan okunup yalnizca renk degistirilerek geri gonderilir.
            const results = await Promise.allSettled(ids.map((id) => {
                const task = (config.tasks || []).find((item) => item.id === id);
                if (!task) {
                    return Promise.resolve();
                }

                return post("UpdateTask", {
                    title: task.title,
                    description: task.description || null,
                    colorHex: color,
                    priority: task.priority,
                    estimatedMinutes: task.estimatedMinutes,
                    plannedStart: task.plannedStart,
                    plannedEnd: task.plannedEnd,
                    dependsOnTaskIds: task.dependsOnTaskIds || [],
                    rollbackNotes: task.rollbackNotes || null,
                    scriptId: task.scriptId || null,
                    isOutageStep: task.isOutageStep,
                    plannedOutageMinutes: task.plannedOutageMinutes,
                    isRollbackStep: task.isRollbackStep,
                    scenarioGroup: task.scenarioGroup || null
                }, { taskId: id });
            }));

            const failed = results.filter((r) => r.status === "rejected").length;
            if (failed > 0) {
                toast(failed + " gorevin rengi degistirilemedi.", "warning");
            }
            reload();
        });
    }

    const btnBulkAssign = document.getElementById("btnBulkAssign");
    if (btnBulkAssign && assignModal) {
        btnBulkAssign.addEventListener("click", () => {
            const ids = selectedBulkTaskIds();
            if (!ids.length) {
                return;
            }

            bulkTaskIds = ids;
            selection.assign = null;
            document.getElementById("assignTaskId").value = "";
            document.getElementById("assignUserSearch").value = "";
            document.getElementById("assignGroupSearch").value = "";
            document.getElementById("assignNote").value = "";
            assignModal.show();
        });
    }

    const btnBulkHandover = document.getElementById("btnBulkHandover");
    if (btnBulkHandover && handoverModal) {
        btnBulkHandover.addEventListener("click", () => {
            const ids = selectedBulkTaskIds();
            if (!ids.length) {
                return;
            }

            bulkTaskIds = ids;
            selection.handover = null;
            document.getElementById("handoverTaskId").value = "";
            document.getElementById("handoverAssignmentId").value = "";
            document.getElementById("handoverFromText").textContent =
                ids.length + " gorevin tum aktif atamalari devredilecek.";
            document.getElementById("handoverUserSearch").value = "";
            document.getElementById("handoverGroupSearch").value = "";
            handoverModal.show();
        });
    }

    // -------------------------------------------------------------- editorler

    const collaboratorsModalElement = document.getElementById("collaboratorsModal");
    if (collaboratorsModalElement) {
        collaboratorsModalElement.addEventListener("show.bs.modal", () => {
            selection.collaborator = null;
            const searchInput = document.getElementById("collaboratorSearch");
            if (searchInput) {
                searchInput.value = "";
            }
        });
    }

    async function confirmAddCollaborator() {
        const target = selection.collaborator;
        if (!target) {
            return;
        }

        try {
            await post("AddCollaborator", { userId: target.id }, { id: config.runbookId });
            reload();
        } catch (error) {
            showActionError(error);
        }
    }

    // ----------------------------------------------------- surukle-birak sirala

    /**
     * Bir gorev listesi kabini (ana akis veya geri donus plani, ayri ayri)
     * icin surukle-birak siralamayi kurar. Ikisi ayni Order alanini paylassa
     * da her biri yalnizca KENDI grubundaki gorevleri gonderir (bkz.
     * TaskService.ReorderAsync grup kontrolu).
     */
    function wireSortableTaskList(container) {
        if (!container || container.dataset.sortable !== "true") {
            return;
        }

        let dragged = null;

        container.addEventListener("dragstart", (event) => {
            dragged = event.target.closest(".br-task");
            if (dragged) {
                dragged.classList.add("dragging");
            }
        });

        container.addEventListener("dragend", async () => {
            if (!dragged) {
                return;
            }

            dragged.classList.remove("dragging");
            dragged = null;

            const ids = Array.from(container.querySelectorAll(".br-task")).map((el) => el.dataset.taskId);

            try {
                await post("ReorderTasks", { taskIdsInOrder: ids }, { id: config.runbookId });
                reload();
            } catch (error) {
                showActionError(error);
                reload();
            }
        });

        container.addEventListener("dragover", (event) => {
            event.preventDefault();
            if (!dragged) {
                return;
            }

            const target = event.target.closest(".br-task");
            if (!target || target === dragged) {
                return;
            }

            const rect = target.getBoundingClientRect();
            const after = (event.clientY - rect.top) > rect.height / 2;
            container.insertBefore(dragged, after ? target.nextSibling : target);
        });
    }

    wireSortableTaskList(taskList);
    wireSortableTaskList(document.getElementById("rollbackTaskList"));
    document.querySelectorAll(".br-scenario-task-list").forEach(wireSortableTaskList);

    // ------------------------------------------------------- canli guncelleme

    if (window.signalR && config.hubUrl) {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(config.hubUrl, { withCredentials: true })
            .withAutomaticReconnect()
            .build();

        const indicator = document.getElementById("liveIndicator");
        const indicatorText = document.getElementById("liveText");

        function setLive(online, text) {
            if (indicator) {
                indicator.classList.toggle("is-online", online);
            }
            if (indicatorText) {
                indicatorText.textContent = text;
            }
        }

        connection.on("TaskChanged", () => {
            setLive(true, "guncellendi");
            toast("Bu runbook baska bir kullanici tarafindan guncellendi.", "info");
        });

        connection.on("CommentAdded", (payload) => {
            appendComment(payload.taskId, payload.comment);
        });

        connection.on("RunbookChanged", () => {
            setLive(true, "guncellendi");
        });

        connection.onreconnecting(() => setLive(false, "yeniden baglaniyor..."));
        connection.onreconnected(() => setLive(true, "canli"));
        connection.onclose(() => setLive(false, "baglanti kapandi"));

        connection.start()
            .then(() => {
                setLive(true, "canli");
                return connection.invoke("JoinRunbook", config.runbookId);
            })
            .catch(() => setLive(false, "canli guncelleme yok"));
    }
})();
