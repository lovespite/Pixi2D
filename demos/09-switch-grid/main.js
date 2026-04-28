// 09-switch-grid — 聚合 4 个 switch 的开启数到 number 控件
const switches = [sw1, sw2, sw3, sw4];

function refresh() {
    let n = 0;
    for (let i = 0; i < switches.length; i++) if (switches[i].isOn) n++;
    cnt.value = n;
    console.log("switches on:", n);
}

for (let i = 0; i < switches.length; i++) switches[i].on('changed', refresh);

refresh();
console.log("09-switch-grid ready");
