// 03-login — TextBox 输入校验
function onLogin() {
    const u = (txtUser.value || "").trim();
    const p = txtPass.value || "";
    if (u !== "admin")    { lblStatus.content = "❌ 用户名必须是 admin"; console.warn("bad user:", u); return; }
    if (p.length < 4)     { lblStatus.content = "❌ 密码至少 4 个字符";  console.warn("bad pass len:", p.length); return; }
    lblStatus.content = "✅ 登录成功，欢迎 " + u;
    console.log("login ok", u);
}

function onClear() {
    txtUser.value = "";
    txtPass.value = "";
    lblStatus.content = "请输入凭据";
}

console.log("03-login ready");
