// 08-progress-stepper
function clamp(v) { return Math.max(0, Math.min(1, v)); }
function set(v) {
    bar.value = clamp(v);
    lblPct.content = Math.round(bar.value * 100) + " %";
}

function onInc()   { set(bar.value + 0.1); console.log("inc → " + lblPct.content); }
function onDec()   { set(bar.value - 0.1); console.log("dec → " + lblPct.content); }
function onReset() { set(0.4);             console.warn("reset"); }

set(bar.value);
console.log("08-progress-stepper ready");
