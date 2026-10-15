"use strict";
const $ = id => document.getElementById(id);
const messages = {
  "zh-CN": {
    pageTitle: "雕像图鉴 · Calypso's Expansion Set", skip: "跳到雕像列表",
    navigation: "页面导航", workbench: "需求工作台", language: "语言",
    eyebrow: "／ 便签里的小秘密", titleFirst: "认得这张便签，", titleNext: "就懂它的", titleAccent: "小把戏。",
    intro: "一张手绘，一个脾气。收录雕像的便签、箴言与玩法，下次遇见，心里有数。",
    handwritten: "拿起之前，先认一认。", contents: "翻阅目录", indexLabel: "雕像目录",
    indexNote: "箴言记特点，说明看玩法。", collection: "雕像档案",
    viewLabel: "便签预览大小", compact: "紧凑", large: "大图", backTop: "回到顶部 ↑",
    searchLabel: "搜索雕像、箴言或玩法", searchPlaceholder: "搜索名称、箴言或玩法…",
    statusLabel: "收录状态", allStatus: "全部收录", enabledStatus: "当前启用", pausedStatus: "已暂停",
    categoriesLabel: "按玩法分类", emptyTitle: "这一页还没有匹配的雕像。",
    emptyDescription: "试试其他名称，或换一个玩法分类。", reset: "清除筛选",
    footer: "手绘便签 · 雕像图鉴", close: "关闭详情", pausedDetail: "当前版本已暂停，不会在游戏中生成。",
    copyLink: "复制此雕像链接", copied: "链接已复制", copyFallback: "可复制地址栏链接",
    loading: "正在读取图鉴…", offline: "连接暂时中断，继续展示已读取的图鉴，稍后自动重试。",
    unavailable: "暂时无法读取图鉴，请确认工作台已启动，稍后自动重试。",
    preview: "查看便签 ↗", missingImage: "便签暂不可用", pendingImage: "便签待补充",
    enabled: "已启用", paused: "已暂停", pausedCard: "暂停收录 · 当前不会生成",
    imageAlt: name => `${name}的手绘便签`, openDetail: name => `放大查看${name}便签和说明`,
    count: (total, enabled) => `${total} 种收录 · ${enabled} 种当前启用 · ${total - enabled} 种暂停`,
    categories: { all: "全部", "移动": "移动", "声音": "声音", "画面": "画面", "外观": "外观", "其他": "其他" },
  },
  en: {
    pageTitle: "Idol Field Guide · Calypso's Expansion Set", skip: "Skip to the idols",
    navigation: "Page navigation", workbench: "Workbench", language: "Language",
    eyebrow: "/ A little note, a little secret", titleFirst: "Know the note. ", titleNext: "Know the ", titleAccent: "trick.",
    intro: "Every drawing has a personality. Browse the notes, hints and effects, and know what to expect next time.",
    handwritten: "A little look before you lift.", contents: "Browse the guide", indexLabel: "Idol index",
    indexNote: "Hints to remember. Effects to understand.", collection: "The collection",
    viewLabel: "Note preview size", compact: "Compact", large: "Large", backTop: "Back to top ↑",
    searchLabel: "Search idols, hints or effects", searchPlaceholder: "Search names, hints or effects…",
    statusLabel: "Availability", allStatus: "All entries", enabledStatus: "Enabled", pausedStatus: "Paused",
    categoriesLabel: "Filter by effect", emptyTitle: "No matching idols on this page.",
    emptyDescription: "Try another name or a different effect category.", reset: "Clear filters",
    footer: "Hand-drawn notes · Idol field guide", close: "Close details", pausedDetail: "Paused in this version. This idol will not spawn.",
    copyLink: "Copy link to this idol", copied: "Link copied", copyFallback: "Copy the address bar link",
    loading: "Opening the field guide…", offline: "Connection lost. Showing the last loaded guide and retrying shortly.",
    unavailable: "The guide is unavailable. Check that the workbench is running; retrying shortly.",
    preview: "View note ↗", missingImage: "Preview unavailable", pendingImage: "Preview coming soon",
    enabled: "Enabled", paused: "Paused", pausedCard: "Archived entry · Currently not spawning",
    imageAlt: name => `Hand-drawn note for ${name}`, openDetail: name => `View the note and details for ${name}`,
    count: (total, enabled) => `${total} entries · ${enabled} enabled · ${total - enabled} paused`,
    categories: { all: "All", "移动": "Movement", "声音": "Sound", "画面": "Vision", "外观": "Appearance", "其他": "Other" },
  },
};
function stored(key) { try { return localStorage.getItem(key); } catch { return null; } }
function remember(key, value) { try { localStorage.setItem(key, value); } catch { /* Preferences remain usable for this visit. */ } }
const requestedLanguage = new URLSearchParams(location.search).get("lang") || stored("ces-wiki-language");
const state = {
  catalog: null, revision: null, category: "all", detailKey: null, connection: "loading",
  language: requestedLanguage === "en" ? "en" : "zh-CN",
  density: stored("ces-wiki-density") === "large" ? "large" : "compact",
};
const dialog = $("detail");
let etag = null;
const words = () => messages[state.language];
const localized = item => state.language === "en" ? { ...item, ...item.english } : item;
const categoryName = category => words().categories[category] || (state.language === "en" ? "Other" : category);

function updateUrl(key = state.detailKey) {
  const url = new URL(location.href);
  url.searchParams.set("lang", state.language);
  if (key) url.hash = key;
  history.replaceState(null, "", url);
}

function connectionStatus() {
  $("connection").classList.toggle("error", state.connection === "error");
  $("connection").textContent = state.connection === "ok" ? "" :
    state.connection === "loading" ? words().loading : state.catalog ? words().offline : words().unavailable;
}

function applyLanguage() {
  document.documentElement.lang = state.language;
  document.title = words().pageTitle;
  $("language").value = state.language;
  if (!state.catalog) {
    $("version").textContent = state.language === "en" ? "Loading…" : "读取中…";
    $("collection-count").textContent = words().loading;
  }
  for (const element of document.querySelectorAll("[data-i18n]")) element.textContent = words()[element.dataset.i18n];
  for (const element of document.querySelectorAll("[data-i18n-aria]")) element.setAttribute("aria-label", words()[element.dataset.i18nAria]);
  for (const element of document.querySelectorAll("[data-i18n-placeholder]")) element.placeholder = words()[element.dataset.i18nPlaceholder];
  connectionStatus();
  render();
  if (dialog.open) showDetail(state.detailKey, false);
}

function setDensity(density) {
  state.density = density;
  document.body.dataset.density = density;
  $("compact-view").setAttribute("aria-pressed", String(density === "compact"));
  $("large-view").setAttribute("aria-pressed", String(density === "large"));
}

function node(tag, className, text) {
  const element = document.createElement(tag);
  if (className) element.className = className;
  if (text !== undefined) element.textContent = text;
  return element;
}

function artwork(item) {
  const image = node("img");
  image.src = item.image;
  image.alt = words().imageAlt(localized(item).name);
  [image.width, image.height] = state.catalog.imageSize || [814, 732];
  image.loading = "lazy";
  image.addEventListener("error", () => image.replaceWith(node("span", "image-fallback", words().missingImage)));
  return image;
}

function showDetail(key, changeUrl = true) {
  const item = state.catalog?.items.find(entry => entry.key === key);
  if (!item) return;
  const text = localized(item);
  state.detailKey = key;
  $("detail-title").textContent = text.name;
  $("detail-advice").textContent = text.advice;
  $("detail-description").textContent = text.description;
  $("detail-status").textContent = `${categoryName(item.category)} / ${item.enabled ? words().enabledStatus : words().pausedStatus}`;
  $("detail-paused").hidden = item.enabled;
  $("copy-status").textContent = "";
  $("detail-image").hidden = !item.image;
  if (item.image) $("detail-image").src = item.image;
  $("detail-image").alt = words().imageAlt(text.name);
  if (changeUrl) updateUrl(key);
  if (!dialog.open) dialog.showModal();
}

function card(item, index) {
  const text = localized(item);
  const article = node("article", `card${item.enabled ? "" : " paused"}`);
  article.id = `idol-${item.key}`;
  const button = node("button", "image-button");
  button.type = "button";
  button.setAttribute("aria-label", words().openDetail(text.name));
  button.append(item.image ? artwork(item) : node("span", "image-fallback", words().pendingImage));
  button.append(node("span", "zoom-hint", words().preview));
  button.addEventListener("click", () => showDetail(item.key));
  const copy = node("div", "card-copy");
  const meta = node("div", "card-meta");
  meta.append(node("span", "", `${String(index + 1).padStart(2, "0")} / ${categoryName(item.category)}`));
  meta.append(node("span", "badge", item.enabled ? words().enabled : words().paused));
  const title = node("h3");
  const link = node("a", "", text.name);
  link.href = `?lang=${state.language}#${encodeURIComponent(item.key)}`;
  link.addEventListener("click", event => { event.preventDefault(); showDetail(item.key); });
  title.append(link);
  copy.append(meta, title, node("p", "advice", text.advice), node("p", "description", text.description));
  if (!item.enabled) copy.append(node("p", "paused-notice", words().pausedCard));
  article.append(button, copy);
  return article;
}

function render() {
  if (!state.catalog) return;
  const allItems = state.catalog.items;
  $("collection-count").textContent = words().count(allItems.length, allItems.filter(item => item.enabled).length);
  const featured = allItems.find(item => item.key === "duck_step" && item.image) || allItems.find(item => item.image);
  $("featured-image").hidden = !featured;
  if (featured) {
    $("featured-image").src = featured.image;
    $("featured-caption").textContent = localized(featured).advice;
  }
  const query = $("search").value.trim().toLocaleLowerCase();
  const status = $("status").value;
  const items = state.catalog.items.filter(item =>
    (state.category === "all" || item.category === state.category) &&
    (status === "all" || item.enabled === (status === "enabled")) &&
    [item.name, item.advice, item.description, item.category, item.key, categoryName(item.category),
      item.english?.name, item.english?.advice, item.english?.description].join(" ").toLocaleLowerCase().includes(query));
  $("grid").replaceChildren(...items.map(item => card(item, state.catalog.items.indexOf(item))));
  $("result-count").textContent = `${items.length} / ${state.catalog.items.length}`;
  $("empty").hidden = items.length !== 0;
  $("index").replaceChildren(...items.map(item => {
    const link = node("a");
    link.href = `#idol-${item.key}`;
    link.append(node("span", "", String(state.catalog.items.indexOf(item) + 1).padStart(2, "0")), document.createTextNode(localized(item).name));
    return link;
  }));
  const categories = ["all", ...new Set(state.catalog.items.map(item => item.category))];
  $("categories").replaceChildren(...categories.map(category => {
    const button = node("button", "", categoryName(category));
    button.dataset.category = category;
    button.type = "button";
    button.setAttribute("aria-pressed", String(category === state.category));
    button.addEventListener("click", () => {
      state.category = category;
      render();
      // Keep keyboard focus after rebuilding the filter buttons.
      [...$("categories").children].find(child => child.dataset.category === category)?.focus();
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
      state.connection = "ok";
      connectionStatus();
      return;
    }
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const catalog = await response.json();
    if (catalog.schema !== "ces-idol-wiki-v1" || !Array.isArray(catalog.items)) throw new Error("图鉴数据格式不符");
    etag = response.headers.get("ETag");
    state.catalog = catalog;
    $("version").textContent = `v${catalog.version}`;
    if (state.revision !== catalog.revision) {
      state.revision = catalog.revision;
      if (state.category !== "all" && !catalog.items.some(item => item.category === state.category)) state.category = "all";
      render();
      if (dialog.open) showDetail(state.detailKey, false);
      else readHash();
    }
    state.connection = "ok";
    connectionStatus();
  } catch {
    state.connection = "error";
    connectionStatus();
  } finally {
    window.setTimeout(refresh, 5000);
  }
}

$("search").addEventListener("input", render);
$("status").addEventListener("change", render);
$("reset").addEventListener("click", () => { $("search").value = ""; $("status").value = "all"; state.category = "all"; render(); $("search").focus(); });
$("language").addEventListener("change", () => {
  state.language = $("language").value;
  remember("ces-wiki-language", state.language);
  updateUrl();
  applyLanguage();
});
for (const density of ["compact", "large"]) $(density + "-view").addEventListener("click", () => {
  setDensity(density);
  remember("ces-wiki-density", density);
});
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
    await navigator.clipboard.writeText(new URL(`/wiki/?lang=${state.language}#${encodeURIComponent(state.detailKey)}`, location.origin).href);
    $("copy-status").textContent = words().copied;
  } catch {
    $("copy-status").textContent = words().copyFallback;
  }
});
window.addEventListener("hashchange", readHash);
setDensity(state.density);
applyLanguage();
refresh();
