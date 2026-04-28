// 04-calculator — 4 函数计算器
let current = "0";
let previous = null;
let op = null;
let resetOnNext = false;

function show() { display.content = current; }

function inputDigit(d) {
    if (resetOnNext) { current = ""; resetOnNext = false; }
    if (current === "0") current = "";
    current += d;
    show();
}

function onDigit0() { inputDigit("0"); }
function onDigit1() { inputDigit("1"); }
function onDigit2() { inputDigit("2"); }
function onDigit3() { inputDigit("3"); }
function onDigit4() { inputDigit("4"); }
function onDigit5() { inputDigit("5"); }
function onDigit6() { inputDigit("6"); }
function onDigit7() { inputDigit("7"); }
function onDigit8() { inputDigit("8"); }
function onDigit9() { inputDigit("9"); }
function onDot()    { if (resetOnNext) { current = "0"; resetOnNext = false; } if (current.indexOf(".") < 0) { current += "."; show(); } }

function setOp(o) {
    if (previous !== null && op !== null && !resetOnNext) compute();
    previous = parseFloat(current);
    op = o;
    resetOnNext = true;
    console.log("op =", o, "prev =", previous);
}
function onAdd() { setOp("+"); }
function onSub() { setOp("-"); }
function onMul() { setOp("*"); }
function onDiv() { setOp("/"); }

function compute() {
    const a = previous;
    const b = parseFloat(current);
    let r = 0;
    switch (op) {
        case "+": r = a + b; break;
        case "-": r = a - b; break;
        case "*": r = a * b; break;
        case "/": r = b === 0 ? NaN : a / b; break;
        default:  return;
    }
    current = String(r);
    previous = r;
    show();
}

function onEq()    { compute(); op = null; previous = null; resetOnNext = true; }
function onClear() { current = "0"; previous = null; op = null; resetOnNext = false; show(); console.warn("cleared"); }

console.log("04-calculator ready");
