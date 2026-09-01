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

        const payload = await response.json().catch(() => ({ ok: false, error: "Yanit okunamadi." }));
        if (!payload.ok) {
            throw new Error(payload.error || "Islem basarisiz.");
        }
        return payload.data;
    }

    async function get(action, query) {
        const url = "/Runbooks/" + action + (query ? "?" + new URLSearchParams(query) : "");
        const response = await fetch(url, { headers: { "Accept": "application/json" } });
        const payload = await response.json().catch(() => ({ ok: false, error: "Yanit okunamadi." }));
        if (!payload.ok) {
            throw new Error(payload.error || "Islem basarisiz.");
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

            try {
                const created = await post("AddTask", {
                    title: title,
                    description: document.getElementById("newTaskDescription").value || null,
                    colorHex: document.getElementById("newTaskColor").value,
                    priority: document.getElementById("newTaskPriority").value,
                    estimatedMinutes: minutes ? parseInt(minutes, 10) : null,
                    plannedStart: toIsoOrNull(document.getElementById("newTaskStart").value),
                    plannedEnd: toIsoOrNull(document.getElementById("newTaskEnd").value),
                    rollbackNotes: document.getElementById("newTaskRollback").value || null
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
                toast(error.message, "danger");
            }
        });
    }

    // ------------------------------------------------------------ durum / silme

    document.addEventListener("click", async (event) => {
        const statusButton = event.target.closest(".br-status-btn");
        if (statusButton) {
            try {
                await post("ChangeTaskStatus", undefined, {
                    taskId: statusButton.dataset.taskId,
                    status: statusButton.dataset.status
                });
                reload();
            } catch (error) {
                toast(error.message, "danger");
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
                toast(error.message, "danger");
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
                toast(error.message, "danger");
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
                toast(error.message, "danger");
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
                toast(error.message, "danger");
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
            toast(error.message, "danger");
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
        });
    }

    // -------------------------------------------------------------- atama

    const assignModalElement = document.getElementById("assignModal");
    const assignModal = assignModalElement ? new bootstrap.Modal(assignModalElement) : null;

    document.querySelectorAll(".br-assign-btn").forEach((button) => {
        button.addEventListener("click", () => {
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

        const taskId = document.getElementById("assignTaskId").value;

        try {
            await post("Assign", {
                assigneeType: target.kind === "user" ? "User" : "Group",
                userId: target.kind === "user" ? target.id : null,
                groupId: target.kind === "group" ? target.id : null,
                note: document.getElementById("assignNote").value || null,
                notify: document.getElementById("assignNotify").checked
            }, { taskId: taskId });

            assignModal.hide();
            reload();
        } catch (error) {
            toast(error.message, "danger");
        }
    }

    // -------------------------------------------------------------- devir

    const handoverModalElement = document.getElementById("handoverModal");
    const handoverModal = handoverModalElement ? new bootstrap.Modal(handoverModalElement) : null;

    document.querySelectorAll(".br-handover-btn").forEach((button) => {
        button.addEventListener("click", () => {
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

        try {
            await post("Handover", {
                fromAssignmentId: document.getElementById("handoverAssignmentId").value,
                targetType: target.kind === "user" ? "User" : "Group",
                targetUserId: target.kind === "user" ? target.id : null,
                targetGroupId: target.kind === "group" ? target.id : null,
                note: note
            }, { taskId: document.getElementById("handoverTaskId").value });

            handoverModal.hide();
            reload();
        } catch (error) {
            toast(error.message, "danger");
        }
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
            toast(error.message, "danger");
        }
    }

    // ----------------------------------------------------- surukle-birak sirala

    if (taskList && taskList.dataset.sortable === "true") {
        let dragged = null;

        taskList.addEventListener("dragstart", (event) => {
            dragged = event.target.closest(".br-task");
            if (dragged) {
                dragged.classList.add("dragging");
            }
        });

        taskList.addEventListener("dragend", async () => {
            if (!dragged) {
                return;
            }

            dragged.classList.remove("dragging");
            dragged = null;

            const ids = Array.from(taskList.querySelectorAll(".br-task")).map((el) => el.dataset.taskId);

            try {
                await post("ReorderTasks", { taskIdsInOrder: ids }, { id: config.runbookId });
                reload();
            } catch (error) {
                toast(error.message, "danger");
                reload();
            }
        });

        taskList.addEventListener("dragover", (event) => {
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
            taskList.insertBefore(dragged, after ? target.nextSibling : target);
        });
    }

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
