// 11-stopwatch — setInterval / clearInterval (依赖 Host Pump)
let tenths = 0;
let handle = null;

function fmt(t) {
    const min = Math.floor(t / 600);
    const sec = Math.floor((t % 600) / 10);
    const dec = t % 10;
    return (min < 10 ? "0" : "") + min + ":" + (sec < 10 ? "0" : "") + sec + "." + dec;
}

function render() { display.content = fmt(tenths); }

function onStart() {
    if (handle !== null) return;
    handle = setInterval(function () { tenths += 1; render(); }, 100);
    console.log("started, handle =", handle);
}

function onStop() {
    if (handle === null) return;
    clearInterval(handle);
    handle = null;
    console.warn("paused at", fmt(tenths));
}

function onReset() {
    onStop();
    tenths = 0;
    render();
    console.log("reset");
}

render();
console.log("11-stopwatch ready");
