// scripted-login.js — 极简登录校验示例
// 真实场景应通过 fetch (qjs.net 内置) 提交后台; 这里直接本地校验。

function onLogin() {
    const u = txtUser.value || "";
    const p = txtPass.value || "";
    if (!u || !p) {
        lblStatus.content = "请填写用户名和密码";
        console.warn("missing fields");
        return;
    }
    if (u === "admin" && p === "123456") {
        lblStatus.content = "登录成功: " + u;
        console.log("login OK", u);
    } else {
        lblStatus.content = "用户名或密码错误";
        console.error("login failed", u);
    }
}

console.log("scripted-login loaded");
