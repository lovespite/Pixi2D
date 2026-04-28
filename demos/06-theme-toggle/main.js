// 06-theme-toggle — 多个 switch 联动多个 fancy-text
function refresh() {
    const dark = swDark.isOn;
    const compact = swCompact.isOn;

    lblTheme.content   = "当前主题: " + (dark ? "浅色" : "深色");
    lblDensity.content = "布局密度: " + (compact ? "紧凑" : "标准");

    if (dark && compact) lblPreview.content = "[预览·浅色·紧凑]   Pixi2D";
    else if (dark)       lblPreview.content = "[预览·浅色] Pixi2D — A lightweight UI library";
    else if (compact)    lblPreview.content = "[预览·深色·紧凑]   Pixi2D";
    else                 lblPreview.content = "[预览·深色] Pixi2D — A lightweight UI library";

    lblHint.visible = !(dark && compact);
}

swDark.on('changed',   function () { refresh(); console.log("dark =", swDark.isOn); });
swCompact.on('changed', function () { refresh(); console.log("compact =", swCompact.isOn); });

refresh();
console.log("06-theme-toggle ready");
