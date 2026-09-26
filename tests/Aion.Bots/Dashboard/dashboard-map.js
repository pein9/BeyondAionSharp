// Matches aion-portal's calibrated-game-y-x projection; image origin is top left.
function projectMapPoint(position, calibration) {
  return { x: (position.y - calibration.offsetX) / calibration.mapWidth,
    y: (position.x - calibration.offsetY) / calibration.mapHeight };
}

if (typeof module !== "undefined") module.exports = { projectMapPoint };
if (typeof document !== "undefined") (() => {
  const el = id => document.getElementById(id);
  const canvas = el("journey-map"), ctx = canvas.getContext("2d");
  const colors = { mob: "#ff8992", npc: "#f4c76c", gatherable: "#93db89", player: "#bfa2ff", static: "#f4c76c" };
  let maps = [], map = null, image = null, bots = [], bot = null, key = "", trail = [], hitPoints = [];
  let width = 1, height = 1, scale = 1, zoom = 1, center = { x: .5, y: .5 }, drag = null;
  let connected = false, preview = false, imageFailed = false, catalogFailed = false;
  const checked = name => el(`map-${name}`).checked;
  const screen = point => ({ x: width / 2 + (point.x - center.x) * scale, y: height / 2 + (point.y - center.y) * scale });
  const project = position => projectMapPoint(position, map.calibration);

  function fit() {
    center = { x: .5, y: .5 };
    zoom = 1;
    // Include spawns outside the cropped artwork, instead of pinning them to its edge.
    if (map) {
      const points = [{ x: 0, y: 0 }, { x: 1, y: 1 }, ...map.spawns.map(project)];
      if (bot?.position) points.push(project(bot.position));
      const minX = Math.min(...points.map(p => p.x)), maxX = Math.max(...points.map(p => p.x));
      const minY = Math.min(...points.map(p => p.y)), maxY = Math.max(...points.map(p => p.y));
      center = { x: (minX + maxX) / 2, y: (minY + maxY) / 2 };
      zoom = Math.min(width / (maxX - minX), height / (maxY - minY)) / Math.min(width, height) * .94;
    }
    draw();
  }

  function chooseMap(id) {
    const next = maps.find(m => m.mapId === id) ?? null;
    if (next === map) return;
    map = next;
    image = null;
    imageFailed = false;
    trail = [];
    el("map-title").textContent = map ? `${map.name}${map.assetKind === "grid-fallback" ? " · coordinate grid" : ""}` : `Map ${id ?? "unknown"}`;
    if (map) {
      const loading = new Image();
      loading.onload = () => { if (map === next) { image = loading; draw(); } };
      loading.onerror = () => { if (map === next) { imageFailed = true; draw(); } };
      loading.src = map.image;
    }
    fit();
  }

  function selectBot() {
    bot = bots.find(b => b.bot === el("map-bot").value) ?? bots[0] ?? null;
    const nextKey = bot ? `${bot.bot}/${bot.characterId}/${bot.connectionGeneration}/${bot.mapId}` : "";
    if (key !== nextKey) { trail = []; key = nextKey; el("map-tooltip").hidden = true; }
    chooseMap(bot ? bot.mapId : 220010000);
    if (map && bot?.position) {
      const point = project(bot.position), previous = trail[trail.length - 1];
      if (!previous || point.x !== previous.x || point.y !== previous.y) {
        // Teleports/revives are a break in the trail, not a walked line across the map.
        const gap = previous && Math.hypot((point.x - previous.x) * map.calibration.mapWidth,
          (point.y - previous.y) * map.calibration.mapHeight) > 150;
        trail.push({ ...point, gap });
        if (trail.length > 600) trail.shift();
      }
      if (checked("follow")) center = point;
    }
    draw();
  }

  function marker(point, color, radius, solid, info, corpse = false) {
    const p = screen(point);
    if (p.x < -10 || p.y < -10 || p.x > width + 10 || p.y > height + 10) return;
    ctx.beginPath();
    ctx.strokeStyle = color;
    ctx.fillStyle = color;
    ctx.lineWidth = solid ? 1.5 : 1;
    if (corpse) {
      ctx.moveTo(p.x - radius, p.y - radius); ctx.lineTo(p.x + radius, p.y + radius);
      ctx.moveTo(p.x + radius, p.y - radius); ctx.lineTo(p.x - radius, p.y + radius);
    } else ctx.arc(p.x, p.y, radius, 0, Math.PI * 2);
    if (solid && !corpse) ctx.fill();
    ctx.stroke();
    hitPoints.push({ ...p, info, radius });
  }

  function draw() {
    scale = Math.min(width, height) * zoom;
    ctx.clearRect(0, 0, width, height);
    ctx.fillStyle = "#0a1018"; ctx.fillRect(0, 0, width, height);
    hitPoints = [];
    if (!map) {
      el("map-status").textContent = catalogFailed ? "Map catalog unavailable; reload to retry." : "No portal artwork available for this map yet.";
      return;
    }
    if (image) {
      const origin = screen({ x: 0, y: 0 });
      ctx.drawImage(image, origin.x, origin.y, scale, scale);
      ctx.fillStyle = "#060e1820"; ctx.fillRect(origin.x, origin.y, scale, scale);
    }
    const search = el("map-search").value.trim().toLowerCase();
    const matches = item => !search || `${item.name} ${item.templateId}`.toLowerCase().includes(search);
    const visible = kind => kind === "mob" ? checked("mobs") : kind === "gatherable" ? checked("gatherables") : checked("npcs");
    let references = 0, observed = 0;
    for (const spawn of map.spawns) {
      if (!visible(spawn.kind) || !matches(spawn)) continue;
      references++;
      marker(project(spawn), colors[spawn.kind], zoom > 2 ? 3 : 2, false,
        `${spawn.name} · ${spawn.templateId}${spawn.level ? ` · Lv ${spawn.level}` : ""}\nSpawn reference · ${spawn.x.toFixed(1)}, ${spawn.y.toFixed(1)}, ${spawn.z.toFixed(1)}${spawn.condition !== "ALL" ? `\n${spawn.condition}` : ""}${spawn.pool ? ` · pool ${spawn.pool}` : ""}`);
    }
    if (checked("trail") && trail.length > 1) {
      ctx.beginPath(); ctx.strokeStyle = "#56e9de"; ctx.lineWidth = 2;
      trail.forEach((point, i) => { const p = screen(point); if (!i || point.gap) ctx.moveTo(p.x, p.y); else ctx.lineTo(p.x, p.y); });
      ctx.stroke();
    }
    if (bot && checked("observed")) {
      for (const object of bot.observedObjects ?? []) {
        if (object.objectId === bot.characterId) continue;
        const template = map.templates.get(object.templateId);
        const kind = template?.kind ?? object.kind;
        const name = object.name || template?.name || `${object.kind} ${object.templateId ?? object.objectId}`;
        if (!visible(kind) || !matches({ ...object, name })) continue;
        observed++;
        const point = project(object.position);
        if (object.moveTarget && !object.isCorpse) {
          const from = screen(point), to = screen(project(object.moveTarget));
          ctx.beginPath(); ctx.setLineDash([3, 4]); ctx.strokeStyle = colors[kind] ?? "#ffffff";
          ctx.moveTo(from.x, from.y); ctx.lineTo(to.x, to.y); ctx.stroke(); ctx.setLineDash([]);
        }
        marker(point, object.isCorpse ? "#a6abb3" : colors[kind] ?? "#ffffff", 4, true,
          `${name} · ${object.templateId ?? object.objectId}\nObserved${object.isCorpse ? " corpse" : ""} · object ${object.objectId}\n${object.position.x.toFixed(1)}, ${object.position.y.toFixed(1)}, ${object.position.z.toFixed(1)}`, object.isCorpse);
      }
    }
    if (bot?.position) {
      const point = project(bot.position), p = screen(point);
      marker(point, bot.isDead ? "#ff657d" : "#66fff1", 8, true,
        `${bot.characterName} · Lv ${bot.level}\n${bot.action}\n${bot.position.x.toFixed(1)}, ${bot.position.y.toFixed(1)}, ${bot.position.z.toFixed(1)}`);
      ctx.beginPath(); ctx.strokeStyle = "#fff"; ctx.lineWidth = 2; ctx.arc(p.x, p.y, 11, 0, Math.PI * 2); ctx.stroke();
      ctx.font = "bold 13px system-ui"; ctx.textAlign = "center";
      ctx.lineWidth = 4; ctx.strokeStyle = "#071018"; ctx.strokeText(bot.characterName, p.x, p.y - 18);
      ctx.fillStyle = "#fff"; ctx.fillText(bot.characterName, p.x, p.y - 18);
    }
    const age = bot ? Math.max(0, (Date.now() - new Date(bot.updatedAt).getTime()) / 1000).toFixed(0) : null;
    el("map-status").textContent = `${imageFailed ? "Artwork failed to load · " : ""}${references} spawn references · ${observed} observed${bot ? ` · ${preview ? "Saved state" : connected ? "State" : "Disconnected; last state"} ${age}s old` : " · waiting for a character"}`;
  }

  function zoomAt(factor, x = width / 2, y = height / 2) {
    const before = { x: center.x + (x - width / 2) / scale, y: center.y + (y - height / 2) / scale };
    zoom = Math.max(.35, Math.min(24, zoom * factor));
    scale = Math.min(width, height) * zoom;
    center = { x: before.x - (x - width / 2) / scale, y: before.y - (y - height / 2) / scale };
    draw();
  }
  function pointer(event) { const r = canvas.getBoundingClientRect(); return { x: event.clientX - r.left, y: event.clientY - r.top }; }
  function inspect(point) {
    const hit = hitPoints.slice().reverse().find(p => Math.hypot(p.x - point.x, p.y - point.y) <= p.radius + 5);
    const tip = el("map-tooltip"); tip.hidden = !hit;
    if (hit) {
      tip.textContent = hit.info;
      tip.style.left = `${Math.max(8, Math.min(width - 285, point.x + 15))}px`;
      tip.style.top = `${Math.max(8, Math.min(height - 105, point.y + 15))}px`;
    }
  }
  canvas.addEventListener("pointerdown", event => { drag = { ...pointer(event), center: { ...center }, moved: false }; canvas.setPointerCapture(event.pointerId); });
  canvas.addEventListener("pointermove", event => {
    const p = pointer(event);
    if (drag) {
      if (Math.hypot(p.x - drag.x, p.y - drag.y) > 3) drag.moved = true;
      if (drag.moved) { el("map-follow").checked = false; center = { x: drag.center.x - (p.x - drag.x) / scale, y: drag.center.y - (p.y - drag.y) / scale }; el("map-tooltip").hidden = true; draw(); }
    } else inspect(p);
  });
  canvas.addEventListener("pointerup", event => { if (!drag?.moved) inspect(pointer(event)); drag = null; });
  canvas.addEventListener("pointercancel", () => { drag = null; });
  canvas.addEventListener("pointerleave", () => { el("map-tooltip").hidden = true; });
  canvas.addEventListener("wheel", event => { event.preventDefault(); el("map-follow").checked = false; const p = pointer(event); zoomAt(Math.exp(-event.deltaY * .0015), p.x, p.y); }, { passive: false });
  canvas.addEventListener("keydown", event => { if (["+", "=", "-"].includes(event.key)) { event.preventDefault(); zoomAt(event.key === "-" ? 1 / 1.5 : 1.5); } });
  el("map-plus").onclick = () => zoomAt(1.5);
  el("map-minus").onclick = () => zoomAt(1 / 1.5);
  el("map-fit").onclick = () => { el("map-follow").checked = false; fit(); };
  el("map-follow").onchange = selectBot;
  el("map-bot").onchange = selectBot;
  for (const name of ["mobs", "npcs", "gatherables", "observed", "trail"]) el(`map-${name}`).onchange = draw;
  el("map-search").oninput = draw;
  new ResizeObserver(() => {
    width = canvas.clientWidth; height = canvas.clientHeight;
    const ratio = window.devicePixelRatio || 1;
    canvas.width = Math.round(width * ratio); canvas.height = Math.round(height * ratio);
    ctx.setTransform(ratio, 0, 0, ratio, 0, 0); draw();
  }).observe(canvas);
  window.journeyMap = {
    update(next, isPreview = false) {
      connected = true; preview = isPreview; bots = next;
      const select = el("map-bot"), selected = select.value;
      const options = bots.map(b => `${b.bot}:${b.characterName}`).join("|");
      if (select.dataset.options !== options) {
        select.replaceChildren(...bots.map(b => new Option(b.characterName, b.bot)));
        if (!bots.length) select.add(new Option("Waiting for a bot", ""));
        select.value = bots.some(b => b.bot === selected) ? selected : bots[0]?.bot ?? "";
        select.dataset.options = options;
      }
      selectBot();
    },
    disconnected() { connected = false; draw(); },
  };
  fetch("/maps/catalog.json").then(response => {
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return response.json();
  }).then(catalog => {
    maps = catalog.maps;
    for (const m of maps) m.templates = new Map(m.spawns.map(s => [s.templateId, s]));
    selectBot();
  }).catch(() => { catalogFailed = true; draw(); });
})();
