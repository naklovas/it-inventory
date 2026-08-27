/* ==========================================================================
   Avatar fotografi yedegi.

   Rozetin icinde bas harfler zaten duruyor; fotograf yuklenemezse <img>
   kaldirilir ve bas harfler kendiliginden gorunur. Atanan rozetlerinde
   fotografin yanindaki bas harf etiketi de kaldirilir; aksi halde ayni
   harfler iki kez gorunurdu.

   Bu dosya <head> icinde, ertelenmeden yuklenir: "error" olayi kabarmadigi
   icin dinleyicinin yakalama (capture) asamasinda ve resimler yuklenmeye
   baslamadan once kayitli olmasi gerekir. Tek dinleyici hem sunucudan gelen
   hem de JavaScript ile sonradan eklenen rozetleri kapsar.
   ========================================================================== */
(function () {
    "use strict";

    /** Fotografi kaldirir; atanan rozetindeki tekrar eden bas harf etiketini de siler. */
    function fallbackToInitials(image) {
        const chip = image.closest(".br-assignee");
        image.remove();

        if (chip) {
            const label = chip.querySelector(".br-assignee-initials");
            if (label) {
                label.remove();
            }
        }
    }

    document.addEventListener("error", function (event) {
        const image = event.target;

        if (image instanceof HTMLImageElement && image.classList.contains("br-avatar-photo")) {
            fallbackToInitials(image);
        }
    }, true);

    // Onbellekten gelen ve dinleyici kurulmadan once basarisiz olan resimler icin
    // sayfa hazir oldugunda bir tarama daha yapilir.
    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("img.br-avatar-photo").forEach(function (image) {
            if (image.complete && image.naturalWidth === 0) {
                fallbackToInitials(image);
            }
        });
    });
})();
