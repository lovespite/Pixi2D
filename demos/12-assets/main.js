// 12-assets — 演示 globalThis.assets：异步加载本地 JSON / 远程文本 / 缓存控制。

function fillTable(items) {
    const rows = [["id", "name", "price"]];
    for (let i = 0; i < items.length; i++) {
        const it = items[i];
        rows.push([String(it.id), String(it.name), String(it.price)]);
    }
    grid.setData(rows);
    status.content = "loaded " + items.length + " items";
}

assets.on('loadedJson', function (id, url, jsonText, metaJson) {
    try {
        const obj = JSON.parse(jsonText);
        const meta = JSON.parse(metaJson);
        console.log("loadedJson #" + id, url, "fromCache=" + meta.fromCache, "size=" + meta.sizeBytes);
        fillTable(obj.items || []);
    } catch (e) {
        status.content = "parse error: " + e;
        console.error(e);
    }
});

assets.on('loadedText', function (id, url, text, metaJson) {
    const meta = JSON.parse(metaJson);
    console.log("loadedText #" + id, url, "status=" + (meta.statusCode || ''), "len=" + text.length);
    status.content = "remote ok (" + text.length + " bytes)";
});

assets.on('error', function (id, url, msg) {
    console.error("asset error #" + id, url, msg);
    status.content = "error: " + msg;
});

assets.on('progress', function (id, url, loaded, total) {
    console.log("progress #" + id, loaded + "/" + total);
});

function onReload() {
    status.content = "reloading sample.json ...";
    assets.loadJson("sample.json");
}

function onRemote() {
    status.content = "fetching example.com ...";
    assets.loadText("https://example.com/");
}

function onClear() {
    assets.clearCache();
    status.content = "cache cleared (memory + disk)";
}

function onStats() {
    const s = JSON.parse(assets.cacheStats());
    status.content = "cache: mem=" + s.memoryBytes + "B/" + s.memoryEntries + " disk=" + s.diskBytes + "B/" + s.diskEntries;
}

// 初始加载
onReload();
console.log("12-assets ready");
