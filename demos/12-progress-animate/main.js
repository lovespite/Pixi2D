// 12-progress-animate — setInterval 平滑动画
let v = 0;
let handle = null;

function show() {
    bar.value = v;
    lblPct.content = Math.round(v * 100) + " %";
}

function onStart() {
    if (handle !== null) return;
    handle = setInterval(function () {
        v += 0.005;
        if (v >= 1) { v = 1; show(); onPause(); console.log("done"); return; }
        show();
    }, 16);
    console.log("animating");
}

function onPause() {
    if (handle === null) return;
    clearInterval(handle);
    handle = null;
    console.warn("paused at", Math.round(v * 100), "%");
}

function onReset() { onPause(); v = 0; show(); console.log("reset"); }

show();
console.log("12-progress-animate ready");
