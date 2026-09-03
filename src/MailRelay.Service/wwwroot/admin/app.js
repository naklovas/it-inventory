// Basit admin panel istemcisi. Admin API anahtari yalnizca bu tarayicinin sessionStorage'inda
// tutulur (sunucuya baska hicbir yere gonderilmez) - sekme kapatilinca silinir.

const ADMIN_KEY_STORAGE = "mailrelay.adminKey";

function getAdminKey() {
  return sessionStorage.getItem(ADMIN_KEY_STORAGE) || "";
}

function setAdminKey(key) {
  sessionStorage.setItem(ADMIN_KEY_STORAGE, key);
}

async function apiFetch(path, options = {}) {
  const headers = Object.assign({}, options.headers, {
    "X-Admin-Key": getAdminKey(),
  });
  if (options.body && !headers["Content-Type"]) {
    headers["Content-Type"] = "application/json";
  }

  const response = await fetch(path, Object.assign({}, options, { headers }));
  if (response.status === 401) {
    throw new Error("Admin anahtari gecersiz. Lutfen dogru anahtari girin.");
  }
  if (!response.ok) {
    let message = `Istek basarisiz (HTTP ${response.status})`;
    try {
      const body = await response.json();
      if (body?.error) message = body.error;
      else if (body?.title) message = body.title;
    } catch { /* govde JSON degilse varsayilan mesaj kalir */ }
    throw new Error(message);
  }
  if (response.status === 204) return null;
  return response.json();
}

function fmtDate(value) {
  if (!value) return "-";
  const d = new Date(value);
  return d.toLocaleString("tr-TR");
}

function showStatus(el, message, kind) {
  el.textContent = message;
  el.className = "status-bar" + (kind ? ` ${kind}` : "");
}

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, (c) => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;",
  })[c]);
}
