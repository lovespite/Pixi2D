// =====================================================================
// Pixi2D PXML Preview — main.js
// ---------------------------------------------------------------------
// 这是一个用 PXML + JS + Pixi2D.Host 自举的 .pxml 预览/编辑器，
// 与旧 WinForms `Pixi2D.Preview` 工具功能对等（MVP 范围）：
//   • 命令行透传的目标 .pxml 路径 (globalThis.hostArgs[0]) 启动加载
//   • 内联多行编辑器
//   • AutoSave: 文本变更后 500ms 防抖回写磁盘
//   • AutoHotReload: 800ms 轮询 mtime, 外部修改自动重载
//   • Pxml.parse → 渲染对象树 + 诊断列表 + 状态栏统计
//
// 注意：所有 [JSExport] 控件属性名均为 camelCase
//      （如 editor.text、swAutoSave.isOn、lblPath.content；
//       不要写 .Text / .IsOn / .Content）。
// =====================================================================

const SAVE_DEBOUNCE   = 500;   // AutoSave 防抖
const POLL_FILE_MS    = 800;   // 磁盘 mtime 轮询
const SUPPRESS_MS     = 1200;  // 自写入后忽略外部回调的窗口

let currentPath  = (typeof globalThis.hostArgs !== 'undefined' && hostArgs[0]) ? hostArgs[0] : null;
let lastSeenText = '';
let lastSavedAt  = 0;
let lastDiskMtime = 0;
let saveTimer    = null;
let suppressUntil = 0;
let _lastDiags   = [];

// ── 启动 ──────────────────────────────────────────────────────────────
function init() {
    swAutoSave.isOn   = true;
    swAutoReload.isOn = true;

    // 诊断表样式 + 表头
    diagTable.hasHeader = true;
    diagTable.setHeaderStyle({ backColor: '#22272e', color: '#cdd9e5', fontSize: 12, align: 'left' });
    diagTable.setTableStyle({ backColor: '#1b1f25', color: '#cdd9e5', borderColor: '#2c333a', fontSize: 12, align: 'left' });
    diagTable.on('cellClicked', (row, _col, _txt) => {
        // hasHeader=true 时 DataSource[0] 是表头, 数据行从 1 起
        const d = _lastDiags[row - 1];
        if (!d || !d.line) return;
        editor.scrollToLine(d.line);
    });

    if (currentPath) {
        try {
            const txt = fs.readFile(currentPath, 'utf8');
            editor.text = txt;
            lastSeenText = txt;
            lastDiskMtime = safeMtime(currentPath);
            lblPath.content = currentPath;
            setStatus('loaded ' + currentPath);
        } catch (e) {
            setStatus('error: ' + e.message);
            lblPath.content = '(load failed)';
        }
    } else {
        editor.text = '<?xml version="1.0" encoding="utf-8"?>\n<panel id="root" width="640" height="360" />\n';
        lastSeenText = editor.text;
        lblPath.content = '(no file - editing in memory)';
        setStatus('no input file; edit and Reload to parse');
    }

    parseAndRender();

    // editor 文本变化 → debounce 解析 + 保存
    editor.on('changed', (txt) => {
        if (txt === lastSeenText) return;
        lastSeenText = txt;
        parseAndRender();
        if (currentPath && swAutoSave.isOn) {
            if (saveTimer) clearTimeout(saveTimer);
            saveTimer = setTimeout(saveNow, SAVE_DEBOUNCE);
        }
    });

    setInterval(checkDiskChange, POLL_FILE_MS);
}

// ── 编辑器轮询 (legacy fallback) ──────────────────────────────────────
function checkEditorChange() {
    const t = editor.text;
    if (t === lastSeenText) return;
    lastSeenText = t;
    parseAndRender();
    if (currentPath && swAutoSave.isOn) {
        if (saveTimer) clearTimeout(saveTimer);
        saveTimer = setTimeout(saveNow, SAVE_DEBOUNCE);
    }
}

function saveNow() {
    saveTimer = null;
    if (!currentPath) return;
    try {
        fs.writeFile(currentPath, editor.text, 'utf8');
        lastSavedAt = Date.now();
        suppressUntil = lastSavedAt + SUPPRESS_MS;
        lastDiskMtime = safeMtime(currentPath);
        setStatus('saved ' + currentPath + ' (' + new Date(lastSavedAt).toLocaleTimeString() + ')');
    } catch (e) {
        setStatus('save error: ' + e.message);
    }
}

// ── 磁盘 mtime 轮询 ───────────────────────────────────────────────────
function checkDiskChange() {
    if (!currentPath || !swAutoReload.isOn) return;
    if (Date.now() < suppressUntil) return;
    const m = safeMtime(currentPath);
    if (m === 0 || m === lastDiskMtime) return;
    lastDiskMtime = m;
    try {
        const txt = fs.readFile(currentPath, 'utf8');
        if (txt === editor.text) return;
        editor.text = txt;
        lastSeenText = txt;
        parseAndRender();
        setStatus('reloaded from disk @ ' + new Date().toLocaleTimeString());
    } catch (e) {
        setStatus('reload error: ' + e.message);
    }
}

function safeMtime(path) {
    try {
        const s = fs.stat(path);
        // qjs.net fs.stat 返回的对象通常有 mtimeMs / mtime
        if (s && typeof s.mtimeMs === 'number') return s.mtimeMs;
        if (s && s.mtime) return new Date(s.mtime).getTime();
        return 0;
    } catch (_) { return 0; }
}

// ── Pxml.parse + 渲染 ────────────────────────────────────────────────
function parseAndRender() {
    const r = Pxml.parse(editor.text, currentPath || '<editor>');
    renderTree(r.tree || []);
    renderDiagnostics(r.diagnostics || []);
    const errs = (r.diagnostics || []).filter(d => d.severity === 'Error').length;
    const warns = (r.diagnostics || []).filter(d => d.severity === 'Warning').length;
    setStatus((r.ok ? 'parsed ok' : 'parse failed') + ' — errors=' + errs + ' warnings=' + warns);
}

function renderTree(nodes) {
    UI.clear('treePanel');
    if (nodes.length === 0) { UI.appendText('treePanel', '(empty)', '#888888', 13); return; }
    for (const n of nodes) {
        const indent = '  '.repeat(n.depth);
        const idPart = n.id ? '  #' + n.id : '';
        UI.appendText('treePanel', indent + '<' + n.type + '>' + idPart, '#cdd9e5', 13);
    }
}

function renderDiagnostics(diags) {
    _lastDiags = diags;
    if (diags.length === 0) {
        diagTable.setData([['Severity','Line','Col','Element','Message'], ['Info','','','','(no diagnostics)']]);
        diagTable.clearStyles();
        diagTable.setHeaderStyle({ backColor: '#22272e', color: '#cdd9e5', fontSize: 12, align: 'left' });
        return;
    }
    const rows = [['Severity','Line','Col','Element','Message']];
    for (const d of diags) {
        const tag = d.element ? '<' + d.element + '>' + (d.attribute ? ' @' + d.attribute : '') : '';
        rows.push([
            String(d.severity || ''),
            d.line ? String(d.line) : '',
            d.column ? String(d.column) : '',
            tag,
            String(d.message || ''),
        ]);
    }
    diagTable.setData(rows);
    diagTable.clearStyles();
    diagTable.setHeaderStyle({ backColor: '#22272e', color: '#cdd9e5', fontSize: 12, align: 'left' });
    for (let i = 0; i < diags.length; i++) {
        const sev = diags[i].severity;
        const dataRow = i + 1; // 因 hasHeader=true, 数据行从 1 起
        if (sev === 'Error') {
            diagTable.setRowStyle(dataRow, { backColor: '#3a1f24', color: '#ff6b6b' });
        } else if (sev === 'Warning') {
            diagTable.setRowStyle(dataRow, { color: '#f1c40f' });
        }
    }
}

function setStatus(msg) {
    status.content = msg;
}

// ── 事件处理（PXML on-* 引用） ───────────────────────────────────────
function onReload() {
    if (!currentPath) { parseAndRender(); return; }
    try {
        const txt = fs.readFile(currentPath, 'utf8');
        editor.text = txt;
        lastSeenText = txt;
        lastDiskMtime = safeMtime(currentPath);
        parseAndRender();
        setStatus('manual reload @ ' + new Date().toLocaleTimeString());
    } catch (e) {
        setStatus('reload error: ' + e.message);
    }
}

// ── 启动 ─────────────────────────────────────────────────────────────
init();
