/* Service Manager kayit numarasi icin oneri kutusu.
   Kayitlar SCSM veritabanindan salt-okunur olarak gelir. */
(function () {
    "use strict";

    const input = document.getElementById("scsmInput");
    const suggest = document.getElementById("scsmSuggest");
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

        timer = setTimeout(async () => {
            try {
                const response = await fetch("/Runbooks/SearchWorkItems?term=" + encodeURIComponent(term));
                const payload = await response.json();

                if (!payload.ok || !payload.data || !payload.data.length) {
                    suggest.classList.remove("show");
                    return;
                }

                suggest.innerHTML = payload.data.map((item) =>
                    '<div class="br-suggest-item" data-id="' + item.id + '">' +
                    "<span><strong>" + item.id + "</strong>" +
                    '<small class="d-block br-muted">' + (item.title || "") + "</small></span></div>"
                ).join("");

                suggest.classList.add("show");
            } catch {
                suggest.classList.remove("show");
            }
        }, 300);
    });

    suggest.addEventListener("click", (event) => {
        const item = event.target.closest(".br-suggest-item");
        if (item) {
            input.value = item.dataset.id;
            suggest.classList.remove("show");
        }
    });

    document.addEventListener("click", (event) => {
        if (!suggest.contains(event.target) && event.target !== input) {
            suggest.classList.remove("show");
        }
    });
})();
