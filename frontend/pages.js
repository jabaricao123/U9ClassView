/* ===== Page Renderer ===== */
async function renderPage(parsed, route) {
    const mc = document.getElementById('mainContent');
    switch (parsed.page) {
        case 'entity': return renderSearchPage(mc, 'entity', '实体查询', '/entity/search', entityCols());
        case 'form': return renderSearchPage(mc, 'form', 'Form 查询', '/form/search', formCols());
        case 'view': return renderSearchPage(mc, 'view', 'View 查询', '/view/search', viewCols());
        case 'reference': return renderSearchPage(mc, 'reference', '参照查询', '/reference/search', refCols());
        case 'bp': return renderSearchPage(mc, 'bp', 'BP 查询', '/bp/search', bpCols());
        case 'oql': return renderOqlPage(mc);
        case 'recent': return renderRecentPage(mc);
        case 'db-config': return renderConfigPage(mc);
        case 'entityDetail': return renderEntityDetail(mc, parsed.id, route);
        case 'viewDetail': return renderViewDetail(mc, parsed.className, parsed.viewName, parsed.key);
        case 'dictDetail': return renderDictDetail(mc, parsed.itemType, parsed.itemKey);
        default: mc.innerHTML = emptyHtml('页面不存在');
    }
}

/* ===== Column Definitions ===== */
function entityCols() {
    return [
        { key: '_fav', th: '★', w: '36px', render: r => starHtml(r.IsFavorite) },
        { key: 'Name', th: '名称', link: r => 'detail/entity/' + r.ID },
        { key: 'DisplayName', th: '显示名称' },
        { key: '_note', th: '备注', render: r => noteInput(r.ItemType, r.ItemKey, r.Note) },
        { key: 'FullName', th: '命名空间', render: r => { const fn = r.FullName || ''; const i = fn.lastIndexOf('.'); return i > 0 ? esc(fn.substring(0, i)) : esc(fn); } },
        { key: 'DefaultTableName', th: '表名' },
        { key: 'AssemblyName', th: '程序集' },
        { key: 'LastClickedAt', th: '最近点击', render: r => fmtDate(r.LastClickedAt), w: '130px' }
    ];
}
function formCols() {
    return [
        { key: '_fav', th: '★', w: '36px', render: r => starHtml(r.IsFavorite) },
        { key: 'Name', th: '页面名称', link: r => { cacheDetail('form:' + r.ItemKey, r); return 'detail/item/form/' + enc(r.ItemKey); } },
        { key: 'Url', th: 'URI' },
        { key: 'ClassName', th: '类名' },
        { key: 'MenuName', th: '菜单路径' },
        { key: 'AssemblyName', th: '程序集' },
        { key: '_note', th: '备注', render: r => noteInput(r.ItemType, r.ItemKey, r.Note) },
        { key: 'LastClickedAt', th: '最近点击', render: r => fmtDate(r.LastClickedAt), w: '130px' }
    ];
}
function viewCols() {
    return [
        { key: '_fav', th: '★', w: '36px', render: r => starHtml(r.IsFavorite) },
        { key: 'ViewName', th: 'View 名称', link: r => 'detail/view?className=' + enc(r.ClassName) + '&viewName=' + enc(r.ViewName) + '&key=' + enc(r.ItemKey) },
        { key: 'ViewDisplayName', th: 'View 显示名' },
        { key: 'UIForm', th: 'Form 名称' },
        { key: 'FormDisplayName', th: 'Form 显示名' },
        { key: 'ClassName', th: '类名' },
        { key: 'AssemblyName', th: '程序集' },
        { key: 'UIModel', th: 'UI Model' },
        { key: '_note', th: '备注', render: r => noteInput(r.ItemType, r.ItemKey, r.Note) },
        { key: 'LastClickedAt', th: '最近点击', render: r => fmtDate(r.LastClickedAt), w: '130px' }
    ];
}
function refCols() {
    return [
        { key: '_fav', th: '★', w: '36px', render: r => starHtml(r.IsFavorite) },
        { key: 'DisplayName', th: '参照名称', link: r => { cacheDetail('reference:' + r.ItemKey, r); return 'detail/item/reference/' + enc(r.ItemKey); } },
        { key: 'RefEntityName', th: '引用实体' },
        { key: 'FormName', th: 'Form 名称' },
        { key: 'ClassName', th: '类名' },
        { key: 'Assembly', th: '程序集' },
        { key: 'Url', th: 'URI' },
        { key: '_note', th: '备注', render: r => noteInput(r.ItemType, r.ItemKey, r.Note) },
        { key: 'LastClickedAt', th: '最近点击', render: r => fmtDate(r.LastClickedAt), w: '130px' }
    ];
}
function bpCols() {
    return [
        { key: '_fav', th: '★', w: '36px', render: r => starHtml(r.IsFavorite) },
        { key: 'DisplayName', th: '显示名称', link: r => { cacheDetail('bp:' + r.ItemKey, r); return 'detail/item/bp/' + enc(r.ItemKey); } },
        { key: 'FullName', th: '全名' },
        { key: 'AssemblyName', th: '程序集' },
        { key: 'Kind', th: '类型' },
        { key: 'ComponentDisplayName', th: '组件' },
        { key: '_note', th: '备注', render: r => noteInput(r.ItemType, r.ItemKey, r.Note) },
        { key: 'LastClickedAt', th: '最近点击', render: r => fmtDate(r.LastClickedAt), w: '130px' }
    ];
}

/* ===== Generic Search Page ===== */
async function renderSearchPage(mc, itemType, title, apiPath, cols) {
    const cachedKw = sessionStorage.getItem('cv.kw.' + itemType) || '';
    const cachedFuzzy = sessionStorage.getItem('cv.fz.' + itemType) !== 'false';
    mc.innerHTML = `
        <div class="page-header">
            <h1 class="page-title">${title}</h1>
            <div class="search-bar">
                <input id="searchInput" type="text" placeholder="输入关键词搜索…" value="${esc(cachedKw)}">
                <div class="toggle-wrap">
                    <span>模糊</span>
                    <div class="toggle-switch ${cachedFuzzy ? 'on' : ''}" id="fuzzyToggle"></div>
                </div>
                <button class="btn btn-primary" id="searchBtn">搜索</button>
            </div>
            <span class="result-info" id="resultInfo"></span>
        </div>
        <div class="table-wrap" id="tableWrap">${loadingHtml()}</div>`;
    
    let fuzzy = cachedFuzzy;
    const toggle = document.getElementById('fuzzyToggle');
    toggle.onclick = () => { fuzzy = !fuzzy; toggle.classList.toggle('on', fuzzy); };
    
    const doSearch = async () => {
        const kw = document.getElementById('searchInput').value.trim();
        sessionStorage.setItem('cv.kw.' + itemType, kw);
        sessionStorage.setItem('cv.fz.' + itemType, String(fuzzy));
        document.getElementById('tableWrap').innerHTML = loadingHtml();
        try {
            const r = await api.get(apiPath + '?keyword=' + enc(kw) + '&fuzzy=' + fuzzy);
            if (!r.success) { toast(r.message || '查询失败', 'error'); document.getElementById('tableWrap').innerHTML = emptyHtml(r.message); return; }
            let data = r.data || [];
            if (!kw) data = data.filter(d => d.ClickCount > 0 || d.IsFavorite);
            if (data.length === 0 && !kw) data = (r.data || []).slice(0, 50);
            document.getElementById('resultInfo').textContent = `共 ${data.length} 条`;
            renderTable(document.getElementById('tableWrap'), data, cols, itemType);
        } catch (e) { document.getElementById('tableWrap').innerHTML = emptyHtml('请求失败'); toast('网络错误', 'error'); }
    };
    
    document.getElementById('searchBtn').onclick = doSearch;
    document.getElementById('searchInput').addEventListener('keydown', e => { if (e.key === 'Enter') doSearch(); });
    doSearch();
}

function renderTable(container, data, cols, itemType) {
    if (!data.length) { container.innerHTML = emptyHtml('暂无数据'); return; }
    let html = '<table><thead><tr>';
    cols.forEach(c => html += `<th${c.w ? ' style="width:'+c.w+'"' : ''}>${c.th}</th>`);
    html += '</tr></thead><tbody>';
    data.forEach((row, i) => {
        html += '<tr>';
        cols.forEach(c => {
            let cell;
            if (c.render) cell = c.render(row);
            else if (c.link) cell = `<span class="cell-link" data-nav="${c.link(row)}">${esc(row[c.key] || '')}</span>`;
            else cell = esc(row[c.key] != null ? String(row[c.key]) : '');
            html += `<td>${cell}</td>`;
        });
        html += '</tr>';
    });
    html += '</tbody></table>';
    container.innerHTML = html;
    // Wire up stars
    container.querySelectorAll('.fav-star').forEach((el, i) => {
        el.onclick = async () => {
            const row = data[i];
            const result = await toggleFavorite(row.ItemType, row.ItemKey, row.DisplayName || row.Name || row.ViewName || '', row.FullName || row.ClassName || '', null);
            if (result !== null) { row.IsFavorite = result; el.classList.toggle('active', result); }
        };
    });
    // Wire up links
    container.querySelectorAll('.cell-link[data-nav]').forEach(el => {
        el.onclick = () => navigate(el.dataset.nav);
    });
    setupNoteListeners(container);
}

/* ===== Entity Detail ===== */
async function renderEntityDetail(mc, id, route) {
    mc.innerHTML = loadingHtml();
    await recordClick('entity', id);

    let entityMeta = null;
    try {
        const sr = await api.get('/entity/search?keyword=&fuzzy=true');
        if (sr.success && sr.data) {
            entityMeta = (sr.data || []).find(d => d.ID === id) || null;
        }
    } catch (e) { }

    const entityName = entityMeta ? (entityMeta.Name || id) : id;
    const entityDisplayName = entityMeta ? (entityMeta.DisplayName || '') : '';
    const entityFullName = entityMeta ? (entityMeta.FullName || '') : '';
    const entityTable = entityMeta ? (entityMeta.DefaultTableName || '') : '';
    const entityIsFavorite = entityMeta ? !!entityMeta.IsFavorite : false;

    if (typeof setTabLabel === 'function' && route) {
        setTabLabel(route, entityName || entityDisplayName);
    }

    await recordRecent('entity', id, entityDisplayName || entityName, entityTable || entityFullName, null);
    try {
        const r = await api.get('/entity/attributes?id=' + enc(id));
        if (!r.success) { mc.innerHTML = emptyHtml(r.message); return; }
        const data = r.data || [];
        mc.innerHTML = `
            <div class="detail-header">
                <h1 class="detail-title">${esc(entityDisplayName || entityName)}</h1>
                ${entityTable ? '<span class="detail-subtitle detail-pill">表: ' + esc(entityTable) + '</span>' : ''}
                <span class="detail-badge">实体详情</span>
                <button class="entity-fav-btn ${entityIsFavorite ? 'active' : ''}" id="entityFavBtn" type="button">★ 收藏实体</button>
                <button class="sql-gen-btn" id="sqlGenBtn" type="button">生成 SQL</button>
                <span class="detail-subtitle detail-pill">${data.length} 个字段</span>
            </div>
            <div class="table-wrap" id="detailTable"></div>
            <div class="sql-drawer-mask" id="sqlDrawerMask"></div>
            <aside class="sql-drawer" id="sqlDrawer">
                <div class="sql-drawer-head">
                    <h3>SQL 生成结果</h3>
                    <button class="sql-drawer-close" id="sqlDrawerClose" type="button">关闭</button>
                </div>
                <div class="sql-drawer-body" id="sqlDrawerBody"></div>
            </aside>`;

        const favBtn = document.getElementById('entityFavBtn');
        if (favBtn) {
            favBtn.onclick = async () => {
                const result = await toggleFavorite('entity', id, entityDisplayName || entityName, entityTable || entityFullName, null);
                if (result !== null) favBtn.classList.toggle('active', result);
            };
        }

        const pinnedStorageKey = 'cv.entityPins.' + id;
        const pinned = new Set(JSON.parse(localStorage.getItem(pinnedStorageKey) || '[]'));
        const tableEl = document.getElementById('detailTable');
        const renderWithPins = () => {
            const top = data.filter(row => pinned.has(row.ItemKey));
            const rest = data.filter(row => !pinned.has(row.ItemKey));
            renderEntityAttrTable(tableEl, top.concat(rest), id, pinned, renderWithPins);
        };

        const sqlDrawer = document.getElementById('sqlDrawer');
        const sqlDrawerMask = document.getElementById('sqlDrawerMask');
        const sqlDrawerBody = document.getElementById('sqlDrawerBody');
        const openSqlDrawer = () => {
            sqlDrawer.classList.add('open');
            sqlDrawerMask.classList.add('open');
        };
        const closeSqlDrawer = () => {
            sqlDrawer.classList.remove('open');
            sqlDrawerMask.classList.remove('open');
        };

        const sqlGenBtn = document.getElementById('sqlGenBtn');
        const sqlDrawerClose = document.getElementById('sqlDrawerClose');
        if (sqlDrawerClose) sqlDrawerClose.onclick = closeSqlDrawer;
        if (sqlDrawerMask) sqlDrawerMask.onclick = closeSqlDrawer;

        if (sqlGenBtn) {
            sqlGenBtn.onclick = () => {
                const generated = buildEntityQuickQuery(entityTable, data);
                sqlDrawerBody.innerHTML = renderGeneratedSqlHtml(generated);
                openSqlDrawer();
            };
        }

        renderWithPins();
    } catch (e) { mc.innerHTML = emptyHtml('加载失败'); }
}

function renderEntityAttrTable(container, data, entityId, pinned, onPinChange) {
    if (!data.length) { container.innerHTML = emptyHtml('无字段'); return; }
    let html = '<table><thead><tr><th style="width:34px">置顶</th><th>名称</th><th>显示名称</th><th>备注</th><th>类型</th><th>描述</th><th>默认值</th><th>分组</th><th style="width:50px">主键</th><th style="width:50px">可空</th><th style="width:50px">只读</th><th style="width:50px">系统</th></tr></thead><tbody>';
    data.forEach(row => {
        const fullName = row.FullName || '';
        const isUfida = fullName.toLowerCase().startsWith('ufida');
        const typeId = row.ID || '';
        const checked = pinned && pinned.has(row.ItemKey) ? ' checked' : '';
        const typeCell = isUfida
            ? `<span class="cell-type-link" data-type-id="${esc(typeId)}">${esc(fullName)}</span>`
            : esc(fullName);
        const nameCell = isUfida
            ? `<span class="cell-type-link" data-type-id="${esc(typeId)}">${esc(row.Name)}</span>`
            : esc(row.Name);
        html += `<tr>
            <td><input class="pin-check" type="checkbox" data-item-key="${esc(row.ItemKey)}"${checked}></td>
            <td>${nameCell}</td>
            <td>${esc(row.DisplayName)}${noteInput(row.ItemType, row.ItemKey, row.Note)}</td>
            <td>${typeCell}</td><td>${esc(row.Description)}</td>
            <td>${esc(row.DefaultValue)}</td><td>${esc(row.GroupName)}</td>
            <td>${boolTag(row.IsKey)}</td><td>${boolTag(row.IsNullable)}</td>
            <td>${boolTag(row.IsReadOnly)}</td><td>${boolTag(row.IsSystem)}</td></tr>`;
    });
    html += '</tbody></table>';
    container.innerHTML = html;

    container.querySelectorAll('.pin-check').forEach(el => {
        el.addEventListener('change', () => {
            const itemKey = el.dataset.itemKey || '';
            if (el.checked) {
                pinned.add(itemKey);
            } else {
                pinned.delete(itemKey);
            }
            localStorage.setItem('cv.entityPins.' + entityId, JSON.stringify(Array.from(pinned)));
            if (onPinChange) onPinChange();
        });
    });
    container.querySelectorAll('.cell-type-link').forEach(el => {
        el.onclick = () => {
            const typeId = el.dataset.typeId;
            if (typeId) {
                navigate('detail/entity/' + typeId);
            }
        };
    });
    setupNoteListeners(container);
}

function findDateFieldByDisplayName(fields, keyword) {
    const key = (keyword || '').trim();
    if (!key) return null;
    const exact = fields.find(f => String(f.DisplayName || '').trim() === key);
    if (exact) return exact;
    return fields.find(f => String(f.DisplayName || '').indexOf(key) >= 0) || null;
}

function rankDateField(field) {
    const display = String(field.DisplayName || '');
    if (display.indexOf('修改日期') >= 0) return 0;
    if (display.indexOf('创建日期') >= 0) return 1;
    if (display.indexOf('审核日期') >= 0) return 2;
    if (display.indexOf('日期') >= 0) return 3;
    return 9;
}

function quoteSqlName(name) {
    return '[' + String(name || '').replace(/\]/g, ']]') + ']';
}

function buildEntityQuickQuery(tableName, fields) {
    const table = String(tableName || '').trim();
    if (!table) {
        return { sql: '', orderedBy: '', candidates: [], message: '当前实体没有表名，无法生成 SQL。' };
    }

    const modified = findDateFieldByDisplayName(fields, '修改日期');
    const created = findDateFieldByDisplayName(fields, '创建日期');
    const approved = findDateFieldByDisplayName(fields, '审核日期');
    const orderField = modified || created || approved || null;

    const candidates = (fields || [])
        .filter(f => {
            const display = String(f.DisplayName || '');
            const name = String(f.Name || '');
            return display.indexOf('日期') >= 0 || /date|time/i.test(name);
        })
        .sort((a, b) => rankDateField(a) - rankDateField(b));

    if (!orderField) {
        return {
            sql: '',
            orderedBy: '',
            candidates,
            message: '未找到“修改日期 / 创建日期 / 审核日期”字段，不生成 SQL。'
        };
    }

    const sql = 'select top 100 * from ' + quoteSqlName(table) + ' order by ' + quoteSqlName(orderField.Name) + ' desc';
    return {
        sql,
        orderedBy: orderField.Name,
        orderedByDisplay: orderField.DisplayName || orderField.Name,
        candidates,
        message: ''
    };
}

function renderGeneratedSqlHtml(result) {
    const candidatesHtml = (result.candidates || []).map(f =>
        '<tr>' +
        '<td>' + esc(f.Name || '') + '</td>' +
        '<td>' + esc(f.DisplayName || '') + '</td>' +
        '<td>' + esc(f.FullName || '') + '</td>' +
        '</tr>'
    ).join('');

    const topInfo = result.sql
        ? '<div class="sql-info ok">排序字段：' + esc(result.orderedByDisplay || result.orderedBy || '') + '（' + esc(result.orderedBy || '') + '）</div>'
        : '<div class="sql-info warn">' + esc(result.message || '未生成 SQL') + '</div>';

    const sqlBlock = result.sql
        ? '<pre class="sql-text">' + esc(result.sql) + '</pre>'
        : '<pre class="sql-text empty">-- 未生成 SQL</pre>';

    const listWrap = candidatesHtml
        ? '<table class="sql-candidate-table"><thead><tr><th>名称</th><th>显示名称</th><th>类型</th></tr></thead><tbody>' + candidatesHtml + '</tbody></table>'
        : '<div class="sql-info">当前字段中没有识别到日期相关字段。</div>';

    return topInfo +
        '<div class="sql-section-title">SQL</div>' + sqlBlock +
        '<div class="sql-section-title">日期相关字段（优先最新）</div>' + listWrap;
}

/* ===== View Detail ===== */
async function renderViewDetail(mc, className, viewName, key) {
    mc.innerHTML = loadingHtml();
    const itemKey = key || (className + '|' + viewName);
    await recordClick('view', itemKey);
    await recordRecent('view', itemKey, 'View: ' + viewName, className, null);
    try {
        const r = await api.get('/view/fields?className=' + enc(className) + '&viewName=' + enc(viewName));
        if (!r.success) { mc.innerHTML = emptyHtml(r.message); return; }
        const data = r.data || [];
        mc.innerHTML = `
            <div class="detail-header">
                <h1 class="detail-title">${esc(viewName)}</h1>
                <span class="detail-badge">View 详情</span>
                <span class="detail-subtitle">${esc(className)} · ${data.length} 个字段</span>
            </div>
            <div class="table-wrap" id="detailTable"></div>`;
        renderViewFieldTable(document.getElementById('detailTable'), data);
    } catch (e) { mc.innerHTML = emptyHtml('加载失败'); }
}

function renderViewFieldTable(container, data) {
    if (!data.length) { container.innerHTML = emptyHtml('无字段'); return; }
    let html = '<table><thead><tr><th>字段名</th><th>数据类型</th><th>默认值</th><th>分组</th><th>提示</th><th>备注</th></tr></thead><tbody>';
    data.forEach(row => {
        html += `<tr>
            <td>${esc(row.Name)}</td><td>${esc(row.DataType)}</td>
            <td>${esc(row.DefaultValue)}</td><td>${esc(row.GroupName)}</td>
            <td>${esc(row.ToolTips)}</td>
            <td>${noteInput(row.ItemType, row.ItemKey, row.Note)}</td></tr>`;
    });
    html += '</tbody></table>';
    container.innerHTML = html;
    setupNoteListeners(container);
}

/* ===== Dict Detail (Form/Ref/BP) ===== */
async function renderDictDetail(mc, itemType, itemKey) {
    mc.innerHTML = loadingHtml();
    await recordClick(itemType, itemKey);
    await recordRecent(itemType, itemKey, itemType + ': ' + itemKey, '', null);
    // Try to load from search cache or re-search
    let item = getCachedDetail(itemType + ':' + itemKey);
    if (!item) {
        const searchMap = { form: '/form/search', reference: '/reference/search', bp: '/bp/search' };
        const searchUrl = searchMap[itemType];
        if (searchUrl) {
            try {
                const r = await api.get(searchUrl + '?keyword=&fuzzy=true');
                if (r.success && r.data) item = r.data.find(d => d.ItemKey === itemKey);
            } catch (e) { }
        }
    }
    if (!item) { item = { ItemType: itemType, ItemKey: itemKey }; }
    // Render as field table
    const fields = Object.entries(item).filter(([k]) => !['MatchRank', 'MatchLength', 'ItemType', 'ItemKey'].includes(k));
    mc.innerHTML = `
        <div class="detail-header">
            <h1 class="detail-title">${esc(item.DisplayName || item.Name || itemKey)}</h1>
            <span class="detail-badge">${esc(itemType)}</span>
        </div>
        <div class="dict-grid">${fields.map(([k, v]) => `<div class="dict-key">${esc(k)}</div><div class="dict-val">${esc(v != null ? String(v) : '')}</div>`).join('')}</div>`;
}

/* ===== OQL Page ===== */
async function renderOqlPage(mc) {
    mc.innerHTML = `
        <div class="page-header"><h1 class="page-title">OQL 工具</h1></div>
        <div class="oql-editor">
            <textarea class="oql-textarea" id="oqlInput" placeholder="输入 OQL 语句…"></textarea>
            <div class="oql-actions">
                <button class="btn btn-primary" id="oqlParseBtn">解析为 SQL</button>
                <button class="btn btn-secondary" id="oqlExecBtn" disabled>执行 SQL</button>
            </div>
            <div id="sqlPreview" style="display:none"></div>
            <div class="table-wrap" id="oqlResult" style="display:none"></div>
        </div>`;
    let parsedSql = '';
    document.getElementById('oqlParseBtn').onclick = async () => {
        const oql = document.getElementById('oqlInput').value.trim();
        if (!oql) { toast('请输入 OQL', 'error'); return; }
        try {
            const r = await api.post('/oql/parse', { Oql: oql });
            if (r.success) {
                parsedSql = r.data;
                document.getElementById('sqlPreview').style.display = 'block';
                document.getElementById('sqlPreview').innerHTML = `<div class="sql-preview">${esc(parsedSql)}</div>`;
                document.getElementById('oqlExecBtn').disabled = false;
            } else { toast(r.message || '解析失败', 'error'); }
        } catch (e) { toast('解析请求失败', 'error'); }
    };
    document.getElementById('oqlExecBtn').onclick = async () => {
        if (!parsedSql) return;
        const wrap = document.getElementById('oqlResult');
        wrap.style.display = 'block';
        wrap.innerHTML = loadingHtml();
        try {
            const r = await api.post('/oql/execute', { Sql: parsedSql });
            if (r.success) {
                const cols = r.columns || [];
                const rows = r.data || [];
                if (!rows.length) { wrap.innerHTML = emptyHtml('查询结果为空'); return; }
                let html = '<table><thead><tr>' + cols.map(c => `<th>${esc(c)}</th>`).join('') + '</tr></thead><tbody>';
                rows.forEach(row => {
                    html += '<tr>' + cols.map(c => `<td>${esc(row[c] != null ? String(row[c]) : '')}</td>`).join('') + '</tr>';
                });
                html += '</tbody></table>';
                wrap.innerHTML = html;
            } else { wrap.innerHTML = emptyHtml(r.message || '执行失败'); }
        } catch (e) { wrap.innerHTML = emptyHtml('执行请求失败'); }
    };
}

/* ===== Recent Page ===== */
async function renderRecentPage(mc) {
    mc.innerHTML = `
        <div class="page-header"><h1 class="page-title">最近浏览</h1></div>
        <div class="filter-chips" id="typeFilter">
            <span class="chip active" data-type="">全部</span>
            <span class="chip" data-type="entity">实体</span>
            <span class="chip" data-type="form">Form</span>
            <span class="chip" data-type="view">View</span>
            <span class="chip" data-type="reference">参照</span>
            <span class="chip" data-type="bp">BP</span>
        </div>
        <div class="table-wrap" id="recentTable">${loadingHtml()}</div>`;
    let currentType = '';
    const loadRecent = async (itemType) => {
        const wrap = document.getElementById('recentTable');
        wrap.innerHTML = loadingHtml();
        try {
            const url = '/recent/list?top=200' + (itemType ? '&itemType=' + enc(itemType) : '');
            const r = await api.get(url);
            if (!r.success) { wrap.innerHTML = emptyHtml(r.message); return; }
            const data = r.data || [];
            if (!data.length) { wrap.innerHTML = emptyHtml('暂无浏览记录'); return; }
            let html = '<table><thead><tr><th>类型</th><th>标题</th><th>副标题</th><th>浏览时间</th></tr></thead><tbody>';
            data.forEach(row => {
                const navRoute = getRecentNavRoute(row);
                html += `<tr><td><span class="detail-badge">${esc(row.ItemType)}</span></td>
                    <td><span class="cell-link" data-nav="${navRoute}">${esc(row.Title || row.ItemKey)}</span></td>
                    <td>${esc(row.Subtitle)}</td>
                    <td>${fmtDate(row.ViewedAt)}</td></tr>`;
            });
            html += '</tbody></table>';
            wrap.innerHTML = html;
            wrap.querySelectorAll('.cell-link[data-nav]').forEach(el => { el.onclick = () => navigate(el.dataset.nav); });
        } catch (e) { wrap.innerHTML = emptyHtml('加载失败'); }
    };
    document.querySelectorAll('#typeFilter .chip').forEach(chip => {
        chip.onclick = () => {
            document.querySelectorAll('#typeFilter .chip').forEach(c => c.classList.remove('active'));
            chip.classList.add('active');
            currentType = chip.dataset.type;
            loadRecent(currentType);
        };
    });
    loadRecent('');
}

function getRecentNavRoute(row) {
    const t = row.ItemType, k = row.ItemKey;
    if (t === 'entity') return 'detail/entity/' + k;
    if (t === 'view') { const p = k.split('|'); return 'detail/view?className=' + enc(p[0]) + '&viewName=' + enc(p[1]) + '&key=' + enc(k); }
    return 'detail/item/' + t + '/' + enc(k);
}

/* ===== Config Page ===== */
async function renderConfigPage(mc) {
    mc.innerHTML = loadingHtml();
    let profile = { Server: '', DatabaseName: '', UserName: '', Password: '', HasPassword: false, IsDefault: true, Id: 0, Name: '' };
    try {
        const r = await api.get('/config/current');
        if (r.success && r.data) profile = r.data;
    } catch (e) { }
    if (typeof loadConnStatus === 'function') loadConnStatus();
    const hasPassword = !!profile.HasPassword;
    mc.innerHTML = `
        <div class="page-header"><h1 class="page-title">数据库连接设置</h1></div>
        <div class="config-form">
            <div class="form-group"><label>ERP 服务器</label><input id="cfgServer" value="${esc(profile.Server || '')}"></div>
            <div class="form-group"><label>ERP 数据库名</label><input id="cfgDb" value="${esc(profile.DatabaseName || '')}"></div>
            <div class="form-group"><label>ERP 账号</label><input id="cfgUser" value="${esc(profile.UserName || '')}"></div>
            <div class="form-group">
                <label>ERP 密码</label>
                <input id="cfgPwd" type="password" value="" placeholder="${hasPassword ? '已保存，输入新密码才覆盖' : '请输入密码'}">
                ${hasPassword ? '<div class="form-hint">已保存密码已隐藏，不会在页面显示密文。</div>' : ''}
            </div>
            <div class="config-actions">
                <button class="btn btn-secondary" id="cfgTestBtn">测试连接</button>
                <button class="btn btn-primary" id="cfgSaveBtn">保存连接</button>
            </div>
            <div id="cfgStatus"></div>
        </div>`;
    const getProfile = () => ({
        Id: profile.Id || 0, Name: profile.Name || 'default',
        Server: document.getElementById('cfgServer').value.trim(),
        DatabaseName: document.getElementById('cfgDb').value.trim(),
        UserName: document.getElementById('cfgUser').value.trim(),
        Password: document.getElementById('cfgPwd').value,
        KeepPassword: !document.getElementById('cfgPwd').value && hasPassword,
        IsDefault: true
    });
    document.getElementById('cfgTestBtn').onclick = async () => {
        const st = document.getElementById('cfgStatus');
        st.innerHTML = '<div class="config-status info">测试中…</div>';
        try {
            const r = await api.post('/config/test', getProfile());
            st.innerHTML = `<div class="config-status ${r.success ? 'success' : 'error'}">${esc(r.message || (r.success ? '连接成功' : '连接失败'))}</div>`;
        } catch (e) { st.innerHTML = '<div class="config-status error">请求失败</div>'; }
    };
    document.getElementById('cfgSaveBtn').onclick = async () => {
        const st = document.getElementById('cfgStatus');
        try {
            const r = await api.post('/config/save', getProfile());
            if (r.success) { st.innerHTML = '<div class="config-status success">保存成功</div>'; toast('连接配置已保存', 'success'); loadConnStatus(); }
            else st.innerHTML = `<div class="config-status error">${esc(r.message || '保存失败')}</div>`;
        } catch (e) { st.innerHTML = '<div class="config-status error">请求失败</div>'; }
    };
}
