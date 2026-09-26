const byId = id => document.getElementById(id);
const number = value => new Intl.NumberFormat().format(value ?? 0);
const percent = (value, maximum) => maximum > 0 ? Math.max(0, Math.min(100, value * 100 / maximum)) : 0;
const valueOrDash = value => value === null || value === undefined ? "—" : value;

function setMeter(card, name, value, maximum) {
  card.querySelector(`.${name}-bar`).style.width = `${percent(value, maximum)}%`;
  card.querySelector(`.${name}-text`).textContent = `${number(value)} / ${number(maximum)}`;
}

function rows(target, items, cells) {
  target.replaceChildren();
  if (!items.length) {
    const row = target.insertRow();
    const cell = row.insertCell();
    cell.colSpan = cells.length;
    cell.className = "empty-cell";
    cell.textContent = "None observed";
    return;
  }
  for (const item of items) {
    const row = target.insertRow();
    for (const render of cells) {
      const cell = row.insertCell();
      cell.textContent = render(item);
    }
  }
}

function renderBot(bot) {
  const card = byId("bot-template").content.firstElementChild.cloneNode(true);
  card.querySelector(".bot-id").textContent = `${bot.bot} · character ${valueOrDash(bot.characterId)}`;
  card.querySelector(".character").textContent = bot.characterName;
  card.querySelector(".identity").textContent = `${bot.account} · connection generation ${bot.connectionGeneration}`;
  card.querySelector(".state-pill").textContent = bot.connection;
  card.querySelector(".action").textContent = bot.action || "Waiting";
  card.querySelector(".step").textContent = `${bot.step} · ${bot.actionStatus}`;
  card.querySelector(".level").textContent = bot.level;
  card.querySelector(".map").textContent = `${valueOrDash(bot.mapId)} / ${valueOrDash(bot.channel)}`;
  card.querySelector(".position").textContent = bot.position ? `${bot.position.x.toFixed(1)}, ${bot.position.y.toFixed(1)}, ${bot.position.z.toFixed(1)}` : "—";
  card.querySelector(".kinah").textContent = number(bot.kinah);
  card.querySelector(".skills").textContent = `${bot.skillCount} (${bot.cooldownCount} cooling)`;
  card.querySelector(".nearby").textContent = `${bot.nearby.players}P · ${bot.nearby.npcs}N · ${bot.nearby.gatherables}G`;
  setMeter(card, "hp", bot.currentHp, bot.maxHp);
  setMeter(card, "mp", bot.currentMp, bot.maxMp);
  setMeter(card, "xp", bot.experience, bot.experienceNeeded);
  card.querySelector(".xp-text").textContent = `${number(bot.experience)} / ${number(bot.experienceNeeded)}`;
  card.querySelector(".quest-count").textContent = `(${bot.activeQuests.length} active, ${bot.completedQuestIds.length} complete)`;
  rows(card.querySelector(".quests"), bot.activeQuests, [q => q.questId, q => q.status, q => q.stepAndFlags, q => q.completeCount]);
  card.querySelector(".item-count").textContent = `(${bot.inventory.length} stacks)`;
  rows(card.querySelector(".inventory"), bot.inventory, [i => i.itemId, i => i.description || "—", i => number(i.count)]);
  card.querySelector(".packet").textContent = `Last packet: ${bot.lastPacket || "—"}`;
  card.querySelector(".message").textContent = bot.lastSystemMessage || (bot.isDead ? "Character is dead" : "");
  const decisions = bot.decisions || [];
  if (decisions.length) {
    const latest = decisions[decisions.length - 1];
    card.querySelector(".decision-summary").textContent =
      `#${latest.sequence}: ${latest.selectedAction}${latest.selectedQuestId ? ` Q${latest.selectedQuestId}` : ""} · ${latest.outcome} · ${latest.reason}`;
    const checks = card.querySelector(".decision-checks");
    for (const check of latest.globalChecks) {
      const badge = document.createElement("span");
      badge.className = `decision-check ${check.verdict}`;
      badge.textContent = `${check.rule}: ${check.verdict} — ${check.reason}`;
      checks.append(badge);
    }
    const quests = card.querySelector(".decision-quests");
    for (const quest of latest.quests) {
      const node = document.createElement("div");
      node.className = "decision-quest";
      const title = document.createElement("strong");
      title.textContent = `Q${quest.questId} · ${quest.verdict}`;
      node.append(title);
      for (const check of quest.checks) {
        const line = document.createElement("small");
        line.textContent = `${check.rule}: ${check.verdict} — ${check.reason}`;
        node.append(line);
      }
      quests.append(node);
    }
    const history = card.querySelector(".decision-history");
    for (const decision of decisions.slice().reverse()) {
      const entry = document.createElement("li");
      entry.textContent = `#${decision.sequence} ${decision.selectedAction} · ${decision.outcome}: ${decision.reason}`;
      history.append(entry);
    }
  }
  const age = Math.max(0, (Date.now() - new Date(bot.updatedAt).getTime()) / 1000);
  card.querySelector(".age").textContent = `State age: ${age.toFixed(1)}s`;
  return card;
}

async function refresh() {
  try {
    const response = await fetch("/api/state", { cache: "no-store" });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const state = await response.json();
    byId("run").textContent = state.run;
    byId("scenarios").textContent = state.scenarios.join(", ");
    byId("bot-count").textContent = state.bots.length;
    byId("updated").textContent = new Date(state.serverTime).toLocaleTimeString();
    byId("connection").textContent = "Live · refreshes every second";
    byId("pulse").className = "pulse live";
    byId("empty").hidden = state.bots.length > 0;
    const host = byId("bots");
    host.replaceChildren(...state.bots.map(renderBot));
  } catch (error) {
    byId("connection").textContent = `Disconnected · ${error.message}`;
    byId("pulse").className = "pulse error";
  }
}

refresh();
setInterval(refresh, 1000);
