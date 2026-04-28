// scripted-counter.js — 与 scripted-counter.pxml 配合
// 全局变量 btnInc / btnReset / swLog / lblCount 由 Pixi2D.Host 在执行本脚本前 SetGlobal 注入。
// PXML 里 on-click="onInc" 会在脚本执行后通过 obj.on('click', onInc) 完成绑定。

let count = 0;

function refresh() {
    lblCount.content = String(count);
}

function onInc() {
    count += 1;
    refresh();
    if (swLog.isOn) console.log("count =", count);
}

function onReset() {
    count = 0;
    refresh();
    console.warn("counter reset");
}

// 切换开关时也提示一下
swLog.on('changed', function (v) {
    console.info("log switch:", v);
});

console.log("scripted-counter loaded");
