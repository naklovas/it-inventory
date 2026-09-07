/* ==========================================================================
   BookRunner - tum "datetime-local" alanlarina uygulanan ortak davranis:
   - "gg.aa.yyyy ss:dd" (SM/TR bicimi) veya ISO metin yapistirilabilmesi.
   Sayfadaki HER datetime-local input'a otomatik uygulanir; ayrica JS ile
   sonradan DOM'a eklenen alanlar (modallar) icin bir MutationObserver kullanir.
   ========================================================================== */
(function () {
    "use strict";

    /** Yapistirilan metni datetime-local'in bekledigi "yyyy-MM-ddTHH:mm" bicimine cevirir; ayristiramazsa null doner. */
    function parsePastedDateTime(text) {
        const trimmed = (text || "").trim();

        // gg.aa.yyyy ss:dd[:ss] - Service Manager / Turkce bicimi.
        let m = trimmed.match(/^(\d{1,2})\.(\d{1,2})\.(\d{4})[ T](\d{1,2}):(\d{2})(?::\d{2})?$/);
        if (m) {
            const [, d, mo, y, h, mi] = m;
            return y + "-" + mo.padStart(2, "0") + "-" + d.padStart(2, "0") + "T" + h.padStart(2, "0") + ":" + mi;
        }

        // gg.aa.yyyy (saatsiz) - 00:00 varsayilir.
        m = trimmed.match(/^(\d{1,2})\.(\d{1,2})\.(\d{4})$/);
        if (m) {
            const [, d, mo, y] = m;
            return y + "-" + mo.padStart(2, "0") + "-" + d.padStart(2, "0") + "T00:00";
        }

        // ISO: yyyy-MM-dd(T| )HH:mm(:ss)(Z/offset gibi ekler yok sayilir).
        m = trimmed.match(/^(\d{4})-(\d{2})-(\d{2})[ T](\d{2}):(\d{2})/);
        if (m) {
            const [, y, mo, d, h, mi] = m;
            return y + "-" + mo + "-" + d + "T" + h + ":" + mi;
        }

        return null;
    }

    function wireSmartDateTimeInput(input) {
        if (input.dataset.smartDatetimeWired) {
            return;
        }
        input.dataset.smartDatetimeWired = "true";

        // step="1800" (yarim saatlik dilim) daha once denendi ama tarayicinin
        // saat tekerlegini yalnizca 00/30 dakikada durdurup kullaniciyi tam
        // dakika girmekten alikoydu (bazi tarayicilarda elle yazmak da
        // zorlasiyor). Adim kisitlamasi kaldirildi; input'un varsayilan
        // (dakika bazli, saniyesiz) davranisi kullanilir - istenen herhangi
        // bir dakika serbestce girilebilir/yapistirilabilir.

        input.addEventListener("paste", (event) => {
            const clipboard = event.clipboardData || window.clipboardData;
            if (!clipboard) {
                return;
            }

            const parsed = parsePastedDateTime(clipboard.getData("text"));
            if (!parsed) {
                // Ayristirilamadi: native yapistirmaya birakilir (tarayici zaten
                // kendi bicimine uymayan metni kabul etmez).
                return;
            }

            event.preventDefault();
            input.value = parsed;
            input.dispatchEvent(new Event("change", { bubbles: true }));
        });
    }

    function wireAll() {
        document.querySelectorAll('input[type="datetime-local"]').forEach(wireSmartDateTimeInput);
    }

    wireAll();

    // Modallarda sonradan render edilen (orn. gorev duzenle) datetime-local
    // alanlarini da yakalamak icin DOM degisikliklerini izler.
    new MutationObserver(wireAll).observe(document.body, { childList: true, subtree: true });
})();
