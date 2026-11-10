// main.js — Dashboard entry point
import { db } from "./firebase.js";
import {
  collection,
  getDocs,
  onSnapshot,
  query,
  orderBy,
  limit
} from "firebase/firestore";

// ── DOM refs ─────────────────────────────────────────────────────────────────
const devicesContainer  = document.getElementById("devices");
const totalDevices      = document.getElementById("total-devices");
const totalSessions     = document.getElementById("total-sessions");
const statusEl          = document.getElementById("status");

// ── Real-time listener ────────────────────────────────────────────────────────
function startListening() {
  statusEl.textContent = "Connecting…";

  // Listen to all devices in real-time
  const devicesRef = collection(db, "devices");

  onSnapshot(devicesRef, async (snapshot) => {
    statusEl.textContent = `🟢 Live — ${new Date().toLocaleTimeString()}`;
    totalDevices.textContent = snapshot.size;

    devicesContainer.innerHTML = "";
    let totalSessionCount = 0;

    for (const deviceDoc of snapshot.docs) {
      const device = deviceDoc.data();

      // Load sessions for this device
      const sessionsRef  = collection(db, "devices", deviceDoc.id, "sessions");
      const sessionsSnap = await getDocs(sessionsRef);
      totalSessionCount += sessionsSnap.size;

      // Build device card
      const card = buildDeviceCard(deviceDoc.id, device, sessionsSnap);
      devicesContainer.appendChild(card);
    }

    totalSessions.textContent = totalSessionCount;
  }, (err) => {
    statusEl.textContent = `🔴 Error: ${err.message}`;
    console.error(err);
  });
}

// ── Build UI ──────────────────────────────────────────────────────────────────
function buildDeviceCard(deviceId, device, sessionsSnap) {
  const isActive = device.monitoringStatus === "ACTIVE";

  const card = document.createElement("div");
  card.className = `device-card ${isActive ? "active" : ""}`;

  const lastSeen = device.lastSeenAt?.toDate?.()
    ? timeAgo(device.lastSeenAt.toDate())
    : (device.lastSeenAt ?? "—");

  card.innerHTML = `
    <div class="device-header">
      <div class="device-name">
        <span class="dot ${isActive ? "dot-green" : "dot-gray"}"></span>
        ${device.computerName ?? deviceId}
      </div>
      <span class="badge ${isActive ? "badge-active" : "badge-stopped"}">
        ${device.monitoringStatus ?? "UNKNOWN"}
      </span>
    </div>
    <div class="device-meta">
      <span>🖥️ ${device.operatingSystem ?? "—"}</span>
      <span>📦 v${device.applicationVersion ?? "—"}</span>
      <span>🕒 ${lastSeen}</span>
    </div>
    <div class="device-id">ID: ${deviceId}</div>
    <div class="sessions-label">Sessions (${sessionsSnap.size})</div>
    <div class="sessions-list">
      ${buildSessionsList(sessionsSnap)}
    </div>
  `;

  return card;
}

function buildSessionsList(sessionsSnap) {
  if (sessionsSnap.empty) return '<div class="no-sessions">No sessions yet</div>';

  // Sort by startedAt descending, show latest 5
  const sessions = sessionsSnap.docs
    .map(d => ({ id: d.id, ...d.data() }))
    .sort((a, b) => {
      const ta = a.startedAt?.toDate?.() ?? new Date(a.startedAt ?? 0);
      const tb = b.startedAt?.toDate?.() ?? new Date(b.startedAt ?? 0);
      return tb - ta;
    })
    .slice(0, 5);

  return sessions.map(s => {
    const started = s.startedAt?.toDate?.()
      ? s.startedAt.toDate().toLocaleString()
      : (s.startedAt ?? "—");
    return `
      <div class="session-row">
        <span class="session-id">${s.id}</span>
        <span class="session-status ${s.status === "ACTIVE" ? "s-active" : "s-done"}">
          ${s.status ?? "—"}
        </span>
        <span class="session-time">${started}</span>
        <span class="session-events">${s.eventCount ?? 0} events</span>
      </div>
    `;
  }).join("");
}

// ── Helpers ───────────────────────────────────────────────────────────────────
function timeAgo(date) {
  const secs = Math.floor((Date.now() - date) / 1000);
  if (secs < 60)  return `${secs}s ago`;
  if (secs < 3600) return `${Math.floor(secs / 60)}m ago`;
  if (secs < 86400) return `${Math.floor(secs / 3600)}h ago`;
  return `${Math.floor(secs / 86400)}d ago`;
}

// ── Start ─────────────────────────────────────────────────────────────────────
startListening();
