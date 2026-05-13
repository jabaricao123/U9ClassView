/* ===== ClassView Core: API, State, Router, Tabs, Bookmarks ===== */
function resolveApiBase() {
    const queryApiBase = new URLSearchParams(location.search).get('apiBase');
    if (queryApiBase) {
        localStorage.setItem('cv.apiBase', queryApiBase);
    }
    const configuredApiBase = window.CLASSVIEW_API_BASE || queryApiBase || localStorage.getItem('cv.apiBase');
    const fallbackApiBase = location.protocol === 'file:' ? 'http://localhost/api' : '/api';
    return String(configuredApiBase || fallbackApiBase).trim().replace(/\/+$/, '');
}

const API_BASE = resolveApiBase();
if (location.protocol === 'file:' && !window.CLASSVIEW_API_BASE && !new URLSearchParams(location.search).get('apiBase') && !localStorage.getItem('cv.apiBase')) {
    console.warn('Using fallback API base http://localhost/api. You can set ?apiBase=http://host:port/api for local file debugging.');
}

function buildApiUrl(url) {
    if (/^https?:\/\//i.test(url || '')) return url;
    const path = (url || '').startsWith('/') ? url : '/' + (url || '');
    return API_BASE + path;
}

const api = {
    async get(url) {
        const r = await fetch(buildApiUrl(url));
        return r.json();
    },
    async post(url, body) {
        const r = await fetch(buildApiUrl(url), {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body)
        });
        return r.json();
    }
};

/* ===== Toast ===== */
function toast(msg, type = 'info') {
    const c = document.getElementById('toastContainer');
    const el = document.createElement('div');
    el.className = 'toast ' + type;
    el.textContent = msg;
    c.appendChild(el);
    setTimeout(() => el.remove(), 3000);
}

/* ===== State ===== */
const FAVORITE_ALLOWED_TYPES = new Set(['entity', 'form', 'view', 'reference', 'bp']);

const state = {
    tabs: JSON.parse(sessionStorage.getItem('cv.tabs') || '[]'),
    activeTab: sessionStorage.getItem('cv.activeTab') || '',
    favorites: [],
    connInfo: null,
    sidebarCollapsed: false,
    theme: localStorage.getItem('cv.theme') || 'dark'
};

function saveTabs() {
    sessionStorage.setItem('cv.tabs', JSON.stringify(state.tabs));
    sessionStorage.setItem('cv.activeTab', state.activeTab);
}

function setTabLabel(route, label) {
    const tab = state.tabs.find(t => t.route === route);
    if (!tab || !label) return;
    tab.label = label;
    saveTabs();
    renderTabs();
}

function cacheDetail(key, data) { sessionStorage.setItem('cv.detail.' + key, JSON.stringify(data)); }
function getCachedDetail(key) { try { return JSON.parse(sessionStorage.getItem('cv.detail.' + key)); } catch { return null; } }

function applyTheme() {
    const theme = state.theme === 'light' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-theme', theme);
    document.body.setAttribute('data-theme', theme);
    const btn = document.getElementById('themeToggle');
    if (btn) btn.textContent = theme === 'light' ? '深色' : '浅色';
}

function toggleTheme() {
    state.theme = state.theme === 'light' ? 'dark' : 'light';
    localStorage.setItem('cv.theme', state.theme);
    applyTheme();
}

/* ===== Favorites / Bookmarks ===== */
async function loadFavorites() {
    try {
        const r = await api.get('/favorite/list');
        if (r.success) {
            const all = r.data || [];
            state.favorites = all.filter(f => FAVORITE_ALLOWED_TYPES.has((f.ItemType || '').toLowerCase()));
        }
    } catch (e) { console.warn('load favorites failed', e); }
    renderBookmarks();
}

async function toggleFavorite(itemType, itemKey, title, subtitle, extraJson) {
    const normalizedType = (itemType || '').toLowerCase();
    if (!FAVORITE_ALLOWED_TYPES.has(normalizedType)) {
        toast('仅支持实体/Form/View/参照/BP 收藏', 'info');
        return null;
    }
    try {
        const r = await api.post('/favorite/toggle', { ItemType: normalizedType, ItemKey: itemKey, Title: title || '', Subtitle: subtitle || '', ExtraJson: extraJson || null });
        if (r.success) { await loadFavorites(); return r.data.isFavorited; }
    } catch (e) { toast('收藏操作失败', 'error'); }
    return null;
}

function renderBookmarks() {
    const list = document.getElementById('bookmarkList');
    list.innerHTML = '';
    state.favorites.forEach(f => {
        const el = document.createElement('div');
        el.className = 'bookmark-item';
        el.innerHTML = `<span class="bm-text">${esc(f.Title || f.ItemKey)}</span><span class="bm-close" title="取消收藏">✕</span>`;
        el.querySelector('.bm-text').onclick = () => openFavorite(f);
        el.querySelector('.bm-close').onclick = async (e) => { e.stopPropagation(); await toggleFavorite(f.ItemType, f.ItemKey); };
        list.appendChild(el);
    });
}

function openFavorite(f) {
    const t = (f.ItemType || '').toLowerCase();
    const k = f.ItemKey;
    if (t === 'entity') navigate('detail/entity/' + k);
    else if (t === 'view') { const p = k.split('|'); navigate('detail/view?className=' + enc(p[0]) + '&viewName=' + enc(p[1]) + '&key=' + enc(k)); }
    else navigate('detail/item/' + t + '/' + enc(k));
}

/* ===== Connection Status ===== */
async function loadConnStatus() {
    const el = document.getElementById('connStatus');
    const text = document.getElementById('connText');
    try {
        const r = await api.get('/config/current');
        if (r.success && r.data) {
            state.connInfo = r.data;
            if (el) el.classList.add('connected');
            if (text) text.textContent = (r.data.Server || '').replace('tcp:', '') + '/' + (r.data.DatabaseName || '');
        } else {
            state.connInfo = null;
            if (el) el.classList.remove('connected');
            if (text) text.textContent = '未连接';
        }
    } catch (e) {
        state.connInfo = null;
        if (el) el.classList.remove('connected');
        if (text) text.textContent = '未连接';
        console.warn('load conn failed', e);
    }
}

/* ===== Tabs ===== */
const routeLabels = { entity: '实体查询', form: 'Form 查询', view: 'View 查询', reference: '参照查询', bp: 'BP 查询', oql: 'OQL 工具', recent: '最近浏览', 'db-config': '数据库设置' };

function addTab(route, label) {
    if (!state.tabs.find(t => t.route === route)) {
        state.tabs.push({ route, label: label || routeLabels[route] || route });
    }
    state.activeTab = route;
    saveTabs();
    renderTabs();
}

function closeTab(route) {
    const idx = state.tabs.findIndex(t => t.route === route);
    if (idx < 0) return;
    state.tabs.splice(idx, 1);
    if (state.activeTab === route) {
        const next = state.tabs[Math.min(idx, state.tabs.length - 1)];
        state.activeTab = next ? next.route : 'entity';
        navigate(state.activeTab);
    }
    saveTabs();
    renderTabs();
}

function renderTabs() {
    const list = document.getElementById('tabList');
    list.innerHTML = '';
    state.tabs.forEach(t => {
        const el = document.createElement('div');
        el.className = 'tab-item' + (t.route === state.activeTab ? ' active' : '');
        el.innerHTML = `<span>${esc(t.label)}</span><span class="tab-close" title="关闭">✕</span>`;
        el.querySelector('span:first-child').onclick = () => navigate(t.route);
        el.querySelector('.tab-close').onclick = (e) => { e.stopPropagation(); closeTab(t.route); };
        list.appendChild(el);
    });
}

/* ===== Router ===== */
function navigate(route) {
    location.hash = '#/' + route;
}

function getRoute() {
    return (location.hash || '#/entity').replace('#/', '');
}

function parseRoute(route) {
    if (route.startsWith('detail/entity/')) return { page: 'entityDetail', id: route.replace('detail/entity/', '') };
    if (route.startsWith('detail/view')) {
        const params = new URLSearchParams(route.split('?')[1] || '');
        return { page: 'viewDetail', className: params.get('className'), viewName: params.get('viewName'), key: params.get('key') };
    }
    if (route.startsWith('detail/item/')) {
        const parts = route.replace('detail/item/', '').split('/');
        return { page: 'dictDetail', itemType: parts[0], itemKey: decodeURIComponent(parts.slice(1).join('/')) };
    }
    return { page: route || 'entity' };
}

async function onRouteChange() {
    const route = getRoute();
    const parsed = parseRoute(route);

    // Update sidebar active
    document.querySelectorAll('.nav-item').forEach(el => {
        el.classList.toggle('active', el.dataset.route === parsed.page || (el.dataset.route === route));
    });

    // Determine label
    let label = routeLabels[route] || route;
    if (parsed.page === 'entityDetail') label = '实体详情';
    if (parsed.page === 'viewDetail') label = 'View: ' + (parsed.viewName || '');
    if (parsed.page === 'dictDetail') label = parsed.itemType + ': ' + (parsed.itemKey || '').substring(0, 12);

    addTab(route, label);
    await renderPage(parsed, route);
}

/* ===== Note Saving ===== */
async function saveNote(itemType, itemKey, note) {
    try {
        const r = await api.post('/note/save', { ItemType: itemType, ItemKey: itemKey, Note: note });
        if (!r.success) toast('备注保存失败: ' + (r.message || ''), 'error');
    } catch (e) { toast('备注保存失败', 'error'); }
}

/* ===== Record Click & Recent ===== */
async function recordClick(itemType, itemKey) {
    try { await api.post('/recent/click', { ItemType: itemType, ItemKey: itemKey }); } catch (e) { }
}

async function recordRecent(itemType, itemKey, title, subtitle, extraJson) {
    try { await api.post('/recent/record', { ItemType: itemType, ItemKey: itemKey, Title: title || '', Subtitle: subtitle || '', ExtraJson: extraJson || null }); } catch (e) { }
}

/* ===== Helpers ===== */
function esc(s) { const d = document.createElement('div'); d.textContent = s || ''; return d.innerHTML; }
function enc(s) { return encodeURIComponent(s || ''); }
function fmtDate(s) { if (!s) return ''; try { const d = new Date(s); return d.toLocaleDateString('zh-CN') + ' ' + d.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' }); } catch { return s; } }
function boolTag(v) { const s = String(v).toLowerCase(); return (s === 'true' || s === '1') ? '<span class="bool-yes">是</span>' : '<span class="bool-no">否</span>'; }

function starHtml(isFav) {
    return `<span class="fav-star ${isFav ? 'active' : ''}" title="收藏">★</span>`;
}

function noteInput(itemType, itemKey, note) {
    return `<input class="inline-note" type="text" value="${esc(note || '')}" data-item-type="${esc(itemType)}" data-item-key="${esc(itemKey)}" placeholder="添加备注…">`;
}

function setupNoteListeners(container) {
    container.querySelectorAll('.inline-note').forEach(el => {
        const save = () => saveNote(el.dataset.itemType, el.dataset.itemKey, el.value);
        el.addEventListener('blur', save);
        el.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); save(); el.blur(); } });
    });
}

function loadingHtml() { return '<div class="loading"><div class="spinner"></div>加载中…</div>'; }
function emptyHtml(msg) { return `<div class="empty-state"><p>${msg || '暂无数据'}</p></div>`; }

/* ===== Init ===== */
function initApp() {
    applyTheme();
    // Sidebar toggle
    const menuToggle = document.getElementById('menuToggle');
    const syncSidebarState = () => {
        document.body.classList.toggle('sidebar-collapsed', state.sidebarCollapsed);
        menuToggle.setAttribute('aria-expanded', String(!state.sidebarCollapsed));
    };
    syncSidebarState();
    menuToggle.onclick = () => {
        state.sidebarCollapsed = !state.sidebarCollapsed;
        syncSidebarState();
    };
    const themeToggle = document.getElementById('themeToggle');
    if (themeToggle) themeToggle.onclick = toggleTheme;
    // Nav items
    document.querySelectorAll('.nav-item').forEach(el => {
        el.onclick = () => navigate(el.dataset.route);
    });
    // Hash change
    window.addEventListener('hashchange', onRouteChange);
    // Load data
    loadConnStatus();
    loadFavorites();
    // Initial route
    if (!location.hash) location.hash = '#/entity';
    else onRouteChange();
    // Restore tabs
    renderTabs();
}

document.addEventListener('DOMContentLoaded', initApp);
