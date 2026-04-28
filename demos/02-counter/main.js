// 02-counter — on-click 与 on('changed', fn) 两种订阅形式
let count = 0;

function refresh() { lblCount.content = String(count); }

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

swLog.on('changed', function (v) { console.info("log switch:", v); });

console.log("02-counter ready");
