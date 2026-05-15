const colors = ["#19c977", "#4f9dff", "#f4b63f", "#ff5f68", "#bd8cff", "#36d4c7"];
const defaultDeviceCount = 50;
const defaultPoiLimit = 25;
const minMapZoom = 15;
const maxMapZoom = 19;
const state = {
  map: null,
  mapCenter: null,
  mapZoom: 17,
  pois: [],
  tours: [],
  tourMode: false,
  activeTourId: null,
  tourDetail: null,
  tourStops: [],
  tourStopByPoi: new Map(),
  mapLayers: null,
  devices: [],
  deviceLimit: defaultDeviceCount,
  poiLimit: defaultPoiLimit,
  testCase: "round-robin",
  autoTimer: null,
  clientLines: [],
  serverLines: [],
  convergeMode: false
};

const el = {
  testCase: document.querySelector("#testCase"),
  tourSelect: document.querySelector("#tourSelect"),
  tourMode: document.querySelector("#tourMode"),
  deviceLimit: document.querySelector("#deviceLimit"),
  poiLimit: document.querySelector("#poiLimit"),
  applyScenario: document.querySelector("#applyScenario"),
  stepMeters: document.querySelector("#stepMeters"),
  intervalMs: document.querySelector("#intervalMs"),
  toggleWalk: document.querySelector("#toggleWalk"),
  converge: document.querySelector("#converge"),
  devicePanel: document.querySelector("#devicePanel"),
  clientLogPanel: document.querySelector("#clientLogPanel"),
  toggleDevices: document.querySelector("#toggleDevices"),
  toggleClientLog: document.querySelector("#toggleClientLog"),
  deviceRows: document.querySelector("#deviceRows"),
  clientLog: document.querySelector("#clientLog"),
  serverLog: document.querySelector("#serverLog"),
  summary: document.querySelector("#summary"),
  backendBadge: document.querySelector("#backendBadge"),
  clearClient: document.querySelector("#clearClient"),
  clearServer: document.querySelector("#clearServer"),
  saveLog: document.querySelector("#saveLog"),
  downloadLog: document.querySelector("#downloadLog"),
  viewLog: document.querySelector("#viewLog"),
  dbTotal: document.querySelector("#dbTotal"),
  logDialog: document.querySelector("#logDialog"),
  logText: document.querySelector("#logText"),
  closeDialog: document.querySelector("#closeDialog"),
  mapResizeHandle: document.querySelector("#mapResizeHandle")
};

boot();

async function boot() {
  const config = await fetchJson("/api/config");
  el.backendBadge.textContent = config.backendUrl;

  const response = await fetchJson("/api/pois");
  state.pois = response.pois
    .filter(poi => !poi.status || poi.status === "dang_hoat_dong")
    .map((poi, index) => ({
      ...poi,
      isSynthetic: Boolean(poi.isSynthetic) || String(poi.name || "").startsWith("SIM Gian Hang"),
      radiusMeters: poi.radiusMeters || 28,
      priority: Number(poi.monthlyFee || 0) > 0 ? Math.max(1, Math.round(Number(poi.monthlyFee) / 100000)) : 1
    }));
  state.poiLimit = Math.min(defaultPoiLimit, state.pois.length);
  state.deviceLimit = defaultDeviceCount;
  el.poiLimit.max = String(Math.max(1, state.pois.length));
  el.poiLimit.value = String(state.poiLimit);
  el.deviceLimit.value = String(state.deviceLimit);
  await loadTours();

  initMap();
  rebuildDevices();
  bindEvents();
  renderAll();
  pollServerLog();
  setInterval(pollServerLog, 1000);

  logClient(`[SIM] Geofence multi-device simulator ready (session=${Math.random().toString(36).slice(2, 10)})`);
  logClient(`[SIM] loaded ${state.pois.length} POIs from ${response.isFallback ? "fallback data" : "backend API"}`);
  logClient(`[SIM] spawned ${state.devices.length} device(s)`);
  logClient(`[SIM] cluster: ${state.pois.slice(0, 6).map(x => `${x.name}(p=${x.priority})`).join(", ")}`);
}

async function loadTours() {
  try {
    const tours = await fetchJson("/api/tours");
    state.tours = Array.isArray(tours) ? tours.map(normalizeTour) : [];
    renderTourOptions();
    logClient(`[TOUR] loaded ${state.tours.length} active tour(s)`);
  } catch (error) {
    state.tours = [];
    renderTourOptions();
    logClient(`[TOUR] cannot load tours: ${error.message}`);
  }
}

function renderTourOptions() {
  el.tourSelect.innerHTML = `
    <option value="">Khong test tour</option>
    ${state.tours.map(tour => `
      <option value="${tour.id}">#${tour.id} ${escapeHtml(tour.name)} (${tour.stopCount} stops)</option>
    `).join("")}
  `;
}

function initMap() {
  if (!window.L) {
    document.querySelector("#map").innerHTML = `<div class="map-error">Khong tai duoc Leaflet map library.</div>`;
    return;
  }

  const center = averagePoint(getScenarioPois());
  state.mapCenter = center;
  document.querySelector("#map").innerHTML = "";

  state.map = L.map("map", {
    zoomControl: true,
    preferCanvas: true,
    minZoom: minMapZoom,
    maxZoom: maxMapZoom,
    zoomSnap: 0.25,
    wheelPxPerZoomLevel: 90
  }).setView([center.lat, center.lon], state.mapZoom);

  L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
    maxZoom: maxMapZoom,
    attribution: "&copy; OpenStreetMap"
  }).addTo(state.map);

  state.mapLayers = {
    route: L.layerGroup().addTo(state.map),
    geofences: L.layerGroup().addTo(state.map),
    pois: L.layerGroup().addTo(state.map),
    devices: L.layerGroup().addTo(state.map)
  };

  state.map.on("moveend zoomend", () => {
    const nextCenter = state.map.getCenter();
    state.mapCenter = { lat: nextCenter.lat, lon: nextCenter.lng };
    state.mapZoom = state.map.getZoom();
  });

  renderPoiLegend();
  renderMapOverlay();
  window.addEventListener("resize", () => state.map?.invalidateSize());
}

function rebuildDevices() {
  const pois = getScenarioPois();
  const center = averagePoint(pois);
  const offsets = Array.from({ length: state.deviceLimit }, (_, index) => {
    const ring = 1 + Math.floor(index / 20);
    const slot = index % 20;
    const angle = (slot * 18 + ring * 7) * Math.PI / 180;
    const meters = 95 + ring * 24 + (index % 5) * 8;

    return {
      north: Math.sin(angle) * meters,
      east: Math.cos(angle) * meters
    };
  });

  state.devices = offsets.map((offset, index) => {
    const position = offsetPoint(center.lat, center.lon, offset.north, offset.east);
    const targetPoi = pois[index % pois.length];
    const color = colors[index % colors.length];

    return {
      id: `DEV-${String(index + 1).padStart(3, "0")}`,
      index,
      color,
      lat: position.lat,
      lon: position.lon,
      targetIndex: index % pois.length,
      routePhase: 0,
      inside: new Set(),
      lastVisitAt: new Map(),
      queue: [],
      enters: 0,
      cooldown: 0,
      apiOk: 0,
      apiFail: 0,
      apiMs: [],
      currentPoi: null,
      destination: targetPoi.name,
      marker: {
        setLatLng: ([lat, lon]) => {
          state.devices[index].lat = lat;
          state.devices[index].lon = lon;
        }
      }
    };
  });

  renderMapOverlay();
}

function renderPoiLegend() {
  const shell = document.querySelector(".map-shell");
  if (!shell) return;
  const old = shell.querySelector(".poi-legend");
  if (old) old.remove();

  const pois = getScenarioPois();
  const div = document.createElement("div");
  div.className = "poi-legend";
  div.innerHTML = `
    <div class="poi-legend-title">POI gian hang</div>
    ${pois.map((poi, index) => `
      <button type="button" data-poi="${index}">
        <i style="background:${colors[index % colors.length]}"></i>
        <span>#${poi.id} ${escapeHtml(poi.name)} - ${poi.visits ?? 0} visits</span>
      </button>
    `).join("")}
  `;

  div.querySelectorAll("button").forEach(button => {
    button.addEventListener("click", () => {
      const poi = pois[Number(button.dataset.poi)];
      state.map.setView([poi.lat, poi.lon], Math.max(state.map.getZoom(), 17));
      openPoiEditor(poi);
      logClient(`[MAP] focus #${poi.id} ${poi.name}`);
    });
  });

  shell.appendChild(div);
}

function renderMapOverlay() {
  if (!state.map || !state.mapLayers) return;
  const pois = getScenarioPois();

  state.mapLayers.route.clearLayers();
  state.mapLayers.geofences.clearLayers();
  state.mapLayers.pois.clearLayers();
  state.mapLayers.devices.clearLayers();

  const tourRoute = state.tourMode && state.tourStops.length > 1
    ? state.tourStops
    : [];

  if (tourRoute.length > 1) {
    const routeLatLngs = tourRoute.map(stop => [stop.lat, stop.lon]);
    L.polyline(routeLatLngs, {
      color: "#2f80ed",
      weight: 6,
      opacity: 0.78,
      lineCap: "round",
      lineJoin: "round"
    }).addTo(state.mapLayers.route);
  } else if (state.testCase === "corridor" && pois.length > 1) {
    const routeLatLngs = pois.slice(0, Math.min(8, pois.length)).map(poi => [poi.lat, poi.lon]);
    L.polyline(routeLatLngs, {
      color: "#2de39f",
      weight: 5,
      opacity: 0.84,
      lineCap: "round",
      lineJoin: "round"
    }).addTo(state.mapLayers.route);
  }

  for (const [index, poi] of pois.entries()) {
    const color = colors[index % colors.length];
    const showName = !poi.isSynthetic || index < 8;

    L.circle([poi.lat, poi.lon], {
      radius: poi.radiusMeters,
      color,
      weight: 3,
      opacity: 0.86,
      fillColor: color,
      fillOpacity: 0.16,
      interactive: false
    }).addTo(state.mapLayers.geofences);

    const markerHtml = `
      <div class="poi-pin" style="--poi-color:${color}">
        <span>${poi.isSynthetic ? "S" : index + 1}</span>
      </div>
      ${showName ? `
        <div class="poi-name" style="--poi-color:${color}">
          <strong>#${poi.id}</strong> ${escapeHtml(poi.name)}
          <em>${poi.visits ?? 0}</em>
        </div>
      ` : ""}
    `;
    const marker = L.marker([poi.lat, poi.lon], {
      icon: L.divIcon({
        className: `poi-marker leaflet-poi-marker${poi.isSynthetic ? " synthetic" : ""}`,
        html: markerHtml,
        iconSize: showName ? [210, 52] : [32, 36],
        iconAnchor: [15, 31]
      }),
      title: poi.name,
      zIndexOffset: 300
    }).addTo(state.mapLayers.pois);
    marker.on("click", () => openPoiEditor(poi));
  }

  for (const [index, device] of state.devices.entries()) {
    const markerHtml = `
      <span style="background:${device.color}"></span>
      <strong>${device.id}</strong>
    `;
    L.marker([device.lat, device.lon], {
      icon: L.divIcon({
        className: `device-marker leaflet-device-marker${index >= 12 ? " compact" : ""}`,
        html: markerHtml,
        iconSize: index >= 12 ? [12, 12] : [82, 24],
        iconAnchor: index >= 12 ? [6, 6] : [10, 10]
      }),
      zIndexOffset: 700 + index
    }).addTo(state.mapLayers.devices);
  }
}

function openPoiEditor(poi) {
  if (!state.map) return;

  L.popup({ closeButton: true, autoPanPadding: [24, 24], minWidth: 240 })
    .setLatLng([poi.lat, poi.lon])
    .setContent(createPoiEditor(poi))
    .openOn(state.map);
}

function createPoiEditor(poi) {
  const container = document.createElement("div");
  container.className = "poi-radius-editor";
  container.innerHTML = `
    <div class="poi-editor-title">#${poi.id} ${escapeHtml(poi.name)}</div>
    <div class="poi-editor-meta">${poi.visits ?? 0} visits</div>
    <label>
      <span>Ban kinh geofence (m)</span>
      <input type="number" min="0.1" step="0.1" value="${Number(poi.radiusMeters).toFixed(1)}">
    </label>
    <div class="poi-editor-error" aria-live="polite"></div>
    <button type="button" class="primary">Apply radius</button>
  `;

  const input = container.querySelector("input");
  const error = container.querySelector(".poi-editor-error");
  const button = container.querySelector("button");

  L.DomEvent.disableClickPropagation(container);
  L.DomEvent.disableScrollPropagation(container);

  const apply = () => {
    const nextRadius = Number(input.value);
    if (!Number.isFinite(nextRadius) || nextRadius <= 0) {
      error.textContent = "Ban kinh phai lon hon 0.";
      input.focus();
      return;
    }

    poi.radiusMeters = Math.round(nextRadius * 10) / 10;
    error.textContent = "";
    renderMapOverlay();
    evaluateAll();
    renderAll();
    openPoiEditor(poi);
    logClient(`[MAP] updated #${poi.id} radius -> ${poi.radiusMeters}m`);
  };

  button.addEventListener("click", apply);
  input.addEventListener("keydown", event => {
    if (event.key === "Enter") apply();
  });

  return container;
}

function bindEvents() {
  el.applyScenario.addEventListener("click", () => applyScenario());
  el.toggleWalk.addEventListener("click", toggleWalk);
  bindMapResize();
  bindMapNavigation();
  bindCollapsiblePanels();
  el.converge.addEventListener("click", () => {
    state.convergeMode = !state.convergeMode;
    el.converge.textContent = state.convergeMode ? "Spread" : "Converge";
    logClient(`[SIM] converge mode ${state.convergeMode ? "ON" : "OFF"}`);
  });

  document.querySelectorAll("[data-move]").forEach(button => {
    button.addEventListener("click", () => moveAll(button.dataset.move));
  });

  el.clearClient.addEventListener("click", () => {
    state.clientLines = [];
    el.clientLog.textContent = "";
  });

  el.clearServer.addEventListener("click", async () => {
    await fetch("/api/server-log/clear", { method: "POST" });
    await pollServerLog();
  });

  el.saveLog.addEventListener("click", saveLog);
  el.downloadLog.addEventListener("click", downloadLog);
  el.viewLog.addEventListener("click", viewLog);
  el.closeDialog.addEventListener("click", () => el.logDialog.close());
}

function bindCollapsiblePanels() {
  bindPanelToggle(el.devicePanel, el.toggleDevices);
  bindPanelToggle(el.clientLogPanel, el.toggleClientLog);
}

function bindPanelToggle(panel, button) {
  if (!panel || !button) return;

  const setExpanded = expanded => {
    panel.classList.toggle("is-collapsed", !expanded);
    panel.classList.toggle("is-expanded", expanded);
    button.textContent = expanded ? "Collapse" : "Expand";
    button.setAttribute("aria-expanded", String(expanded));
  };

  button.addEventListener("click", event => {
    event.stopPropagation();
    setExpanded(panel.classList.contains("is-collapsed"));
  });

  panel.querySelector(".section-title")?.addEventListener("click", event => {
    if (event.target.closest("button")) return;
    setExpanded(panel.classList.contains("is-collapsed"));
  });

  setExpanded(false);
}

async function applyScenario() {
  if (state.autoTimer) {
    clearInterval(state.autoTimer);
    state.autoTimer = null;
    el.toggleWalk.textContent = "Start Walk";
  }

  state.testCase = el.testCase.value;
  await configureTourMode();
  state.deviceLimit = clamp(Number(el.deviceLimit.value) || defaultDeviceCount, 1, 100);
  state.poiLimit = clamp(Number(el.poiLimit.value) || defaultPoiLimit, 1, Math.max(1, state.pois.length));
  el.deviceLimit.value = String(state.deviceLimit);
  el.poiLimit.value = String(state.poiLimit);

  const center = averagePoint(state.tourMode && state.tourStops.length ? state.tourStops : getScenarioPois());
  setMapCenter(center.lat, center.lon, true);
  rebuildDevices();
  renderPoiLegend();
  evaluateAll();
  renderAll();
  renderMapOverlay();
  logClient(`[SIM] applied test case ${state.testCase}; devices=${state.deviceLimit}; POI=${state.poiLimit}; tour=${state.tourMode ? state.activeTourId : "off"}`);
}

async function configureTourMode() {
  const tourId = Number(el.tourSelect.value) || null;
  state.tourMode = Boolean(el.tourMode.checked && tourId);
  state.activeTourId = state.tourMode ? tourId : null;
  state.tourDetail = null;
  state.tourStops = [];
  state.tourStopByPoi = new Map();

  if (!state.tourMode) {
    logClient("[TOUR] tour mode OFF");
    return;
  }

  try {
    const detail = await fetchJson(`/api/tour/${tourId}`);
    state.tourDetail = normalizeTourDetail(detail);
    state.tourStops = state.tourDetail.stops.filter(stop => Number.isFinite(stop.lat) && Number.isFinite(stop.lon));
    state.tourStopByPoi = new Map(state.tourDetail.stops.map(stop => [stop.poiId, stop]));

    logClient(`[TOUR] tour mode ON: #${state.tourDetail.id} ${state.tourDetail.name}; ${state.tourDetail.stops.length} stop(s)`);
    logClient(`[TOUR] route: ${state.tourDetail.stops.map(stop => `${stop.order}.#${stop.poiId} ${stop.name}`).join(" -> ")}`);
  } catch (error) {
    state.tourMode = false;
    state.activeTourId = null;
    el.tourMode.checked = false;
    logClient(`[TOUR] cannot load selected tour #${tourId}: ${error.message}`);
  }
}

function bindMapResize() {
  let startY = 0;
  let startHeight = 0;
  let dragging = false;

  el.mapResizeHandle.addEventListener("pointerdown", event => {
    dragging = true;
    startY = event.clientY;
    startHeight = document.querySelector(".map-shell").getBoundingClientRect().height;
    el.mapResizeHandle.setPointerCapture(event.pointerId);
    event.preventDefault();
  });

  el.mapResizeHandle.addEventListener("pointermove", event => {
    if (!dragging) return;

    const nextHeight = Math.max(320, Math.min(window.innerHeight * 0.82, startHeight + event.clientY - startY));
    document.documentElement.style.setProperty("--map-height", `${Math.round(nextHeight)}px`);
    state.map?.invalidateSize();
  });

  el.mapResizeHandle.addEventListener("pointerup", event => {
    if (!dragging) return;

    dragging = false;
    el.mapResizeHandle.releasePointerCapture(event.pointerId);
    setTimeout(() => state.map?.invalidateSize(), 50);
  });
}

function bindMapNavigation() {
  document.querySelectorAll("[data-map-zoom]").forEach(button => {
    button.addEventListener("click", () => {
      if (!state.map) return;
      if (button.dataset.mapZoom === "in") {
        state.map.zoomIn();
      } else {
        state.map.zoomOut();
      }
    });
  });

  document.querySelectorAll("[data-map-pan]").forEach(button => {
    button.addEventListener("click", () => panMap(button.dataset.mapPan));
  });
}

function toggleWalk() {
  if (state.autoTimer) {
    clearInterval(state.autoTimer);
    state.autoTimer = null;
    el.toggleWalk.textContent = "Start Walk";
    logClient("[SIM] auto-walk OFF");
    return;
  }

  const interval = Math.max(100, Number(el.intervalMs.value) || 400);
  state.autoTimer = setInterval(autoStep, interval);
  el.toggleWalk.textContent = "Stop Walk";
  logClient("[SIM] auto-walk ON");
}

function autoStep() {
  const stepMeters = Math.max(0.2, Number(el.stepMeters.value) || 1);
  for (const device of state.devices) {
    const target = chooseTarget(device);
    moveDeviceToward(device, target.lat, target.lon, stepMeters);
  }

  evaluateAll();
  renderAll();
  renderMapOverlay();
}

function chooseTarget(device) {
  const pois = getScenarioPois();
  if (state.tourMode && state.tourStops.length) {
    const route = state.tourStops;
    const stop = route[device.targetIndex % route.length];
    if (distanceMeters(device.lat, device.lon, stop.lat, stop.lon) < 4) {
      device.targetIndex = (device.targetIndex + 1) % route.length;
    }

    const next = route[device.targetIndex % route.length];
    device.destination = `Tour ${next.order}. ${next.name}`;
    return { lat: next.lat, lon: next.lon };
  }

  if (state.convergeMode) {
    const center = averagePoint(pois.slice(0, Math.min(3, pois.length)));
    return { lat: center.lat, lon: center.lon };
  }

  if (state.testCase === "single-poi-burst") {
    const poi = pois[0];
    device.destination = `${poi.name} (burst)`;
    return poi;
  }

  if (state.testCase === "fanout") {
    const poi = pois[(device.index * 7 + device.targetIndex) % pois.length];
    if (distanceMeters(device.lat, device.lon, poi.lat, poi.lon) < 5) {
      device.targetIndex = (device.targetIndex + 1) % pois.length;
    }

    const next = pois[(device.index * 7 + device.targetIndex) % pois.length];
    device.destination = next.name;
    return next;
  }

  if (state.testCase === "corridor") {
    const route = pois.slice(0, Math.min(8, pois.length));
    const poi = route[(device.targetIndex + Math.floor(device.index / 8)) % route.length];
    if (distanceMeters(device.lat, device.lon, poi.lat, poi.lon) < 4) {
      device.targetIndex = (device.targetIndex + 1) % route.length;
    }

    const next = route[(device.targetIndex + Math.floor(device.index / 8)) % route.length];
    device.destination = `${next.name} (route)`;
    return next;
  }

  if (state.testCase === "boundary-crossing") {
    const poi = pois[device.index % pois.length];
    const distance = distanceMeters(device.lat, device.lon, poi.lat, poi.lon);
    const targetRadius = device.routePhase % 2 === 0 ? poi.radiusMeters * 0.62 : poi.radiusMeters * 1.18;
    if (Math.abs(distance - targetRadius) < 3) {
      device.routePhase += 1;
    }

    const angle = (device.index * 37 + device.routePhase * 61) * Math.PI / 180;
    const target = offsetPoint(poi.lat, poi.lon, Math.sin(angle) * targetRadius, Math.cos(angle) * targetRadius);
    device.destination = `${poi.name} (${device.routePhase % 2 === 0 ? "inside" : "outside"})`;
    return target;
  }

  const poi = pois[device.targetIndex % pois.length];
  if (distanceMeters(device.lat, device.lon, poi.lat, poi.lon) < 4) {
    device.targetIndex = (device.targetIndex + 1) % pois.length;
  }

  const next = pois[device.targetIndex % pois.length];
  device.destination = next.name;
  return next;
}

function moveAll(direction) {
  const step = Math.max(0.2, Number(el.stepMeters.value) || 1);
  const delta = {
    north: { n: step, e: 0 },
    south: { n: -step, e: 0 },
    east: { n: 0, e: step },
    west: { n: 0, e: -step }
  }[direction];

  for (const device of state.devices) {
    const next = offsetPoint(device.lat, device.lon, delta.n, delta.e);
    device.lat = next.lat;
    device.lon = next.lon;
  }

  logClient(`[SIM] manual move ${direction}`);
  evaluateAll();
  renderAll();
  renderMapOverlay();
}

function moveDeviceToward(device, lat, lon, meters) {
  const distance = distanceMeters(device.lat, device.lon, lat, lon);
  if (distance <= meters) {
    device.lat = lat;
    device.lon = lon;
  } else {
    const ratio = meters / distance;
    device.lat += (lat - device.lat) * ratio;
    device.lon += (lon - device.lon) * ratio;
  }

}

function evaluateAll() {
  const pois = getScenarioPois();
  for (const device of state.devices) {
    const inside = pois
      .filter(poi => distanceMeters(device.lat, device.lon, poi.lat, poi.lon) <= poi.radiusMeters)
      .sort(prioritizePoi);

    const currentInside = new Set(inside.map(poi => poi.id));
    const entered = [...currentInside].filter(id => !device.inside.has(id));
    const exited = [...device.inside].filter(id => !currentInside.has(id));

    if (entered.length > 0) {
      device.enters += entered.length;
      for (const poiId of entered) {
        const poi = pois.find(x => x.id === poiId);
        logClient(`[SIM] ${device.id} ENTER #${poi.id} ${poi.name}`);
        queueVisit(device, poi);
        handleTourEnter(device, poi);
      }
    }

    for (const poiId of exited) {
      const poi = pois.find(x => x.id === poiId);
      logClient(`[SIM] ${device.id} EXIT #${poi.id} ${poi.name}`);
    }

    device.inside = currentInside;
    device.currentPoi = inside[0]?.name ?? null;
    device.queue = inside.map(poi => poi.name);
  }
}

function prioritizePoi(a, b) {
  if (b.priority !== a.priority) return b.priority - a.priority;
  if (a.radiusMeters !== b.radiusMeters) return a.radiusMeters - b.radiusMeters;
  return a.id - b.id;
}

async function queueVisit(device, poi) {
  const key = `${device.id}:${poi.id}`;
  const now = Date.now();
  const last = device.lastVisitAt.get(key) || 0;
  if (now - last < 5000) {
    device.cooldown += 1;
    logClient(`[SIM] ${device.id} cooldown skip #${poi.id}`);
    renderAll();
    return;
  }

  device.lastVisitAt.set(key, now);
  const started = performance.now();

  try {
    const response = await fetch(`/api/visit/${poi.id}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ deviceId: device.id })
    });
    const data = await response.json();
    const ms = Math.round(performance.now() - started);
    device.apiMs.push(ms);

    if (response.ok && data.success) {
      device.apiOk += 1;
    } else {
      device.apiFail += 1;
    }

    logClient(`[API] ${device.id} POST /api/poi/${poi.id}/visit -> ${data.statusCode}; queued=${data.queued}; ${ms}ms`);
  } catch (error) {
    device.apiFail += 1;
    logClient(`[API] ${device.id} POST /api/poi/${poi.id}/visit failed: ${error.message}`);
  }

  renderAll();
}

async function handleTourEnter(device, poi) {
  if (!state.tourMode || !state.activeTourId || !state.tourDetail) return;

  const stop = state.tourStopByPoi.get(poi.id);
  if (!stop) {
    logClient(`[TOUR] ${device.id} ENTER #${poi.id} ${poi.name}; not in selected tour #${state.activeTourId}`);
    return;
  }

  try {
    const progress = await fetchJson(`/api/tour/${state.activeTourId}/progress?deviceId=${encodeURIComponent(device.id)}`);
    const currentStep = readNumber(progress, "stepHienTai", "StepHienTai") || 0;
    const completedAt = readAny(progress, "completedAt", "CompletedAt");

    if (completedAt) {
      logClient(`[TOUR] ${device.id} already completed tour #${state.activeTourId}; no audio for #${poi.id}`);
      return;
    }

    if (currentStep > 0 && stop.order < currentStep) {
      logClient(`[TOUR] ${device.id} re-entered passed stop ${stop.order}.#${poi.id}; no audio`);
      return;
    }

    const response = await fetch(`/api/tour/${state.activeTourId}/advance`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ deviceId: device.id, idGianHangVuaDen: poi.id })
    });
    const result = await response.json();
    const success = Boolean(readAny(result, "success", "Success"));
    const message = readAny(result, "message", "Message") || "";

    if (!response.ok || !success) {
      logClient(`[TOUR] ${device.id} stop ${stop.order}.#${poi.id} advance failed: ${message || response.status}`);
      return;
    }

    const audioUrl = stop.audioIntroUrl || stop.audioDefaultUrl;
    const nextStep = readNumber(result, "stepKeTiep", "StepKeTiep");
    const nextPoiId = readNumber(result, "idGianHangKeTiep", "IdGianHangKeTiep");
    const isCompleted = Boolean(readAny(result, "isCompleted", "IsCompleted"));

    logClient(`[TOUR] ${device.id} reached stop ${stop.order}.#${poi.id} ${stop.name}; progress ${currentStep || 0} -> ${nextStep ?? "-"}`);
    if (audioUrl) {
      logClient(`[AUDIO] PLAY ${device.id} #${poi.id} ${stop.name} -> ${audioUrl}`);
    } else {
      logClient(`[AUDIO] ${device.id} #${poi.id} ${stop.name}: no audio configured`);
    }

    if (isCompleted) {
      logClient(`[TOUR] ${device.id} completed tour #${state.activeTourId}`);
    } else {
      const nextStop = state.tourStopByPoi.get(nextPoiId);
      logClient(`[TOUR] ${device.id} next stop -> ${nextStop ? `${nextStop.order}.#${nextStop.poiId} ${nextStop.name}` : `#${nextPoiId ?? "-"}`}`);
    }
  } catch (error) {
    logClient(`[TOUR] ${device.id} tour handling failed for #${poi.id}: ${error.message}`);
  }
}

function renderAll() {
  const pois = getScenarioPois();
  el.deviceRows.innerHTML = state.devices.map(device => {
    const avg = device.apiMs.length
      ? Math.round(device.apiMs.reduce((sum, value) => sum + value, 0) / device.apiMs.length)
      : "-";

    return `
      <tr>
        <td><span class="device-dot" style="background:${device.color}"></span>${device.id}</td>
        <td class="num">${device.queue.length}</td>
        <td class="num">${device.enters}</td>
        <td class="num">${device.cooldown}</td>
        <td class="num ok">${device.apiOk}</td>
        <td class="num bad">${device.apiFail}</td>
        <td class="num">${avg}</td>
        <td>${escapeHtml(device.currentPoi || "-")}</td>
        <td>${escapeHtml(device.destination || "-")}</td>
      </tr>
    `;
  }).join("");

  const active = state.devices.filter(x => x.currentPoi).length;
  const syntheticPois = pois.filter(x => x.isSynthetic).length;
  el.summary.textContent = `${state.devices.length} thiet bi, ${pois.length} POI (${syntheticPois} SIM), ${active} dang trong geofence.`;
}

function getScenarioPois() {
  const limited = state.pois.slice(0, clamp(state.poiLimit || defaultPoiLimit, 1, Math.max(1, state.pois.length)));
  if (!state.tourMode || !state.tourStopByPoi.size) return limited;

  const byId = new Map(limited.map(poi => [poi.id, poi]));
  for (const stop of state.tourStops) {
    if (byId.has(stop.poiId)) continue;

    const source = state.pois.find(poi => poi.id === stop.poiId);
    byId.set(stop.poiId, source || {
      id: stop.poiId,
      name: stop.name,
      lat: stop.lat,
      lon: stop.lon,
      radiusMeters: 28,
      visits: 0,
      isSynthetic: false,
      priority: 1
    });
  }

  return [...byId.values()];
}

function setMapCenter(lat, lon, refreshFrame) {
  state.mapCenter = { lat, lon };
  if (state.map && refreshFrame) {
    state.map.setView([lat, lon], Math.max(state.map.getZoom(), 16), { animate: true });
  }
}

function panMap(direction) {
  const delta = {
    north: [0, -180],
    south: [0, 180],
    east: [180, 0],
    west: [-180, 0]
  }[direction] || [0, 0];
  state.map?.panBy(delta, { animate: true });
  logClient(`[MAP] pan ${direction}`);
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

async function pollServerLog() {
  try {
    const lines = await fetchJson("/api/server-log");
    state.serverLines = lines.map(line => `${formatTime(line.time)} | ${line.message}`);
    el.serverLog.textContent = state.serverLines.join("\n") || "Cho thiet bi enter geofence...";
    el.serverLog.scrollTop = el.serverLog.scrollHeight;
    el.dbTotal.textContent = `DB: ${lines.length} total`;
  } catch {
    el.serverLog.textContent = "Khong doc duoc server log.";
  }
}

async function saveLog() {
  const response = await fetch("/api/save-log", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ clientLog: state.clientLines.join("\n") })
  });
  const data = await response.json();
  logClient(`[SIM] saved log -> ${data.path}`);
}

function downloadLog() {
  const text = buildCombinedLog();
  const blob = new Blob([text], { type: "text/plain;charset=utf-8" });
  const link = document.createElement("a");
  link.href = URL.createObjectURL(blob);
  link.download = `geofence-session-${new Date().toISOString().replace(/[:.]/g, "-")}.log`;
  link.click();
  URL.revokeObjectURL(link.href);
}

function viewLog() {
  el.logText.value = buildCombinedLog();
  el.logDialog.showModal();
}

function buildCombinedLog() {
  return [
    "=== Client Log ===",
    state.clientLines.join("\n"),
    "",
    "=== Server Log ===",
    state.serverLines.join("\n")
  ].join("\n");
}

function logClient(message) {
  const line = `${new Date().toLocaleTimeString()} | ${message}`;
  state.clientLines.push(line);
  if (state.clientLines.length > 500) state.clientLines.shift();
  el.clientLog.textContent = state.clientLines.join("\n");
  el.clientLog.scrollTop = el.clientLog.scrollHeight;
}

async function fetchJson(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${url} -> ${response.status}`);
  return response.json();
}

function normalizeTour(tour) {
  return {
    id: readNumber(tour, "idTour", "IdTour") || 0,
    name: readAny(tour, "ten", "Ten") || "Tour",
    status: readAny(tour, "tinhTrang", "TinhTrang") || "",
    stopCount: readNumber(tour, "soStop", "SoStop") || 0
  };
}

function normalizeTourDetail(detail) {
  const tour = normalizeTour(readAny(detail, "tour", "Tour") || {});
  const rawStops = readAny(detail, "danhSachStop", "DanhSachStop") || [];
  const stops = Array.isArray(rawStops)
    ? rawStops.map(stop => ({
      tourStopId: readNumber(stop, "idTourDiem", "IdTourDiem") || 0,
      tourId: readNumber(stop, "idTour", "IdTour") || tour.id,
      poiId: readNumber(stop, "idGianHang", "IdGianHang") || 0,
      order: readNumber(stop, "thuTu", "ThuTu") || 0,
      name: readAny(stop, "tenGianHang", "TenGianHang") || `Gian hang ${readNumber(stop, "idGianHang", "IdGianHang") || ""}`,
      lat: readNumber(stop, "lat", "Lat"),
      lon: readNumber(stop, "lon", "Lon"),
      audioIntroUrl: readAny(stop, "audioIntroUrl", "AudioIntroUrl") || "",
      audioDefaultUrl: readAny(stop, "audioMacDinhUrl", "AudioMacDinhUrl") || "",
      isAvailable: Boolean(readAny(stop, "isAvailable", "IsAvailable"))
    })).filter(stop => stop.poiId > 0).sort((a, b) => a.order - b.order)
    : [];

  return {
    id: tour.id,
    name: tour.name,
    stops
  };
}

function readAny(object, ...names) {
  if (!object || typeof object !== "object") return undefined;
  for (const name of names) {
    if (Object.prototype.hasOwnProperty.call(object, name)) return object[name];
  }
  return undefined;
}

function readNumber(object, ...names) {
  const value = readAny(object, ...names);
  const number = Number(value);
  return Number.isFinite(number) ? number : null;
}

function averagePoint(points) {
  const safe = points.length ? points : [{ lat: 10.76285, lon: 106.66062 }];
  return {
    lat: safe.reduce((sum, point) => sum + point.lat, 0) / safe.length,
    lon: safe.reduce((sum, point) => sum + point.lon, 0) / safe.length
  };
}

function offsetPoint(lat, lon, northMeters, eastMeters) {
  const dLat = northMeters / 111320;
  const dLon = eastMeters / (111320 * Math.cos(lat * Math.PI / 180));
  return { lat: lat + dLat, lon: lon + dLon };
}

function distanceMeters(lat1, lon1, lat2, lon2) {
  const radius = 6371000;
  const dLat = toRad(lat2 - lat1);
  const dLon = toRad(lon2 - lon1);
  const rLat1 = toRad(lat1);
  const rLat2 = toRad(lat2);
  const a = Math.sin(dLat / 2) ** 2
    + Math.cos(rLat1) * Math.cos(rLat2) * Math.sin(dLon / 2) ** 2;
  return radius * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

function toRad(value) {
  return value * Math.PI / 180;
}

function formatTime(value) {
  return new Date(value).toLocaleTimeString();
}

function escapeHtml(value) {
  return String(value).replace(/[&<>"']/g, char => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    "\"": "&quot;",
    "'": "&#39;"
  }[char]));
}
