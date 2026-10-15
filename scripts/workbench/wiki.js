"use strict";
const $ = id => document.getElementById(id);
const state = { catalog: null, revision: null, category: "全部", detailKey: null };
const dialog = $("detail");
let etag = null;

function node(tag, className, text) {
  const element = document.createElement(tag);
  if (className) element.className = className;
  if (text !== undefined) element.textContent = text;
  return element;
}

function artwork(item) {
  const image = node("img");
  image.src = item.image;
  image.alt = `${item.name}的手绘便签`;
  image.width = 493;
  image.height = 441;
  image.loading = "lazy";
  image.addEventListener("error", () => image.replaceWith(node("span", "image-fallback", "便签暂不可用")));
  return image;
}

function showDetail(key, changeUrl = true) {
  const item = state.catalog?.items.find(entry => entry.key === key);
  if (!item) return;
  state.detailKey = key;
  $("detail-title").textContent = item.name;
  $("detail-advice").textContent = item.advice;
  $("detail-description").textContent = item.description;
  $("detail-status").textContent = `${item.category} ／ ${item.enabled ? "当前启用" : "已暂停"}`;
  $("detail-paused").hidden = item.enabled;
  $("copy-status").textContent = "";
  $("detail-image").hidden = !item.image;
  if (item.image) $("detail-image").src = item.image;
  $("detail-image").alt = `${item.name}的手绘便签`;
  if (changeUrl) history.replaceState(null, "", `#${encodeURIComponent(key)}`);
  if (!dialog.open) dialog.showModal();
}

function card(item, index) {
  const article = node("article", `card${item.enabled ? "" : " paused"}`);
  article.id = `idol-${item.key}`;
  const button = node("button", "image-button");
  button.type = "button";
  button.setAttribute("aria-label", `放大查看${item.name}便签和说明`);
  button.append(item.image ? artwork(item) : node("span", "image-fallback", "便签待补充"));
  button.append(node("span", "zoom-hint", "查看便签 ↗"));
  button.addEventListener("click", () => showDetail(item.key));
  const copy = node("div", "card-copy");
  const meta = node("div", "card-meta");
  meta.append(node("span", "", `${String(index + 1).padStart(2, "0")} / ${item.category}`));
  meta.append(node("span", "badge", item.enabled ? "已启用" : "已暂停"));
  const title = node("h3");
  const link = node("a", "", item.name);
  link.href = `#${encodeURIComponent(item.key)}`;
  link.addEventListener("click", event => { event.preventDefault(); showDetail(item.key); });
  title.append(link);
  copy.append(meta, title, node("p", "advice", item.advice), node("p", "description", item.description));
  if (!item.enabled) copy.append(node("p", "paused-notice", "暂停收录 · 当前不会生成"));
  article.append(button, copy);
  return article;
}

function render() {
  if (!state.catalog) return;
  const query = $("search").value.trim().toLocaleLowerCase();
  const status = $("status").value;
  const items = state.catalog.items.filter(item =>
    (state.category === "全部" || item.category === state.category) &&
    (status === "all" || item.enabled === (status === "enabled")) &&
    `${item.name} ${item.advice} ${item.description} ${item.category} ${item.key}`.toLocaleLowerCase().includes(query));
  $("grid").replaceChildren(...items.map(item => card(item, state.catalog.items.indexOf(item))));
  $("result-count").textContent = `${items.length} / ${state.catalog.items.length}`;
  $("empty").hidden = items.length !== 0;
  $("index").replaceChildren(...items.map(item => {
    const link = node("a");
    link.href = `#idol-${item.key}`;
    link.append(node("span", "", String(state.catalog.items.indexOf(item) + 1).padStart(2, "0")), document.createTextNode(item.name));
    return link;
  }));
  const categories = ["全部", ...new Set(state.catalog.items.map(item => item.category))];
  $("categories").replaceChildren(...categories.map(category => {
    const button = node("button", "", category);
    button.type = "button";
    button.setAttribute("aria-pressed", String(category === state.category));
    button.addEventListener("click", () => {
      state.category = category;
      render();
      // Keep keyboard focus after rebuilding the filter buttons.
      [...$("categories").children].find(child => child.textContent === category)?.focus();
    });
    return button;
  }));
}

function readHash() {
  const key = location.hash.slice(1);
  if (key && !key.startsWith("idol-")) showDetail(key, false);
}

async function refresh() {
  try {
    const response = await fetch("/api/wiki", { headers: etag ? { "If-None-Match": etag } : {} });
    if (response.status === 304) {
      $("connection").textContent = "";
      return;
    }
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const catalog = await response.json();
    if (catalog.schema !== "ces-idol-wiki-v1" || !Array.isArray(catalog.items)) throw new Error("图鉴数据格式不符");
    etag = response.headers.get("ETag");
    state.catalog = catalog;
    $("version").textContent = `v${catalog.version}`;
    const enabled = catalog.items.filter(item => item.enabled).length;
    $("collection-count").textContent = `${catalog.items.length} 种收录 · ${enabled} 种当前启用 · ${catalog.items.length - enabled} 种暂停`;
    const featured = catalog.items.find(item => item.key === "duck_step" && item.image) || catalog.items.find(item => item.image);
    $("featured-image").hidden = !featured;
    if (featured) {
      $("featured-image").src = featured.image;
      $("featured-caption").textContent = featured.advice;
    }
    if (state.revision !== catalog.revision) {
      state.revision = catalog.revision;
      if (state.category !== "全部" && !catalog.items.some(item => item.category === state.category)) state.category = "全部";
      render();
      if (dialog.open) showDetail(state.detailKey, false);
      else readHash();
    }
    $("connection").classList.remove("error");
    $("connection").textContent = "";
  } catch {
    $("connection").classList.add("error");
    $("connection").textContent = state.catalog ? "连接暂时中断，继续展示已读取的图鉴，稍后自动重试。" : "暂时无法读取图鉴，请确认工作台已启动，稍后自动重试。";
  } finally {
    window.setTimeout(refresh, 5000);
  }
}

$("search").addEventListener("input", render);
$("status").addEventListener("change", render);
$("reset").addEventListener("click", () => { $("search").value = ""; $("status").value = "all"; state.category = "全部"; render(); $("search").focus(); });
$("close-detail").addEventListener("click", () => dialog.close());
dialog.addEventListener("click", event => {
  const box = dialog.getBoundingClientRect();
  if (event.target === dialog && (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom)) dialog.close();
});
dialog.addEventListener("close", () => {
  if (location.hash.slice(1) === state.detailKey) history.replaceState(null, "", location.pathname + location.search);
  state.detailKey = null;
});
$("copy-link").addEventListener("click", async () => {
  try {
    await navigator.clipboard.writeText(new URL(`/wiki/#${encodeURIComponent(state.detailKey)}`, location.origin).href);
    $("copy-status").textContent = "链接已复制";
  } catch {
    $("copy-status").textContent = "可复制地址栏链接";
  }
});
window.addEventListener("hashchange", readHash);
refresh();
