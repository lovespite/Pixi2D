// 05-form-validation — 必填 + 邮箱正则校验
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function onCheck() {
    const errs = [];
    const name = (txtName.value || "").trim();
    const mail = (txtEmail.value || "").trim();

    if (!name) errs.push("姓名不能为空");
    if (!mail) errs.push("邮箱不能为空");
    else if (!EMAIL.test(mail)) errs.push("邮箱格式不合法");

    if (errs.length === 0) {
        lblResult.content = "✅ 校验通过：" + name + " <" + mail + ">";
        console.log("validation ok", name, mail);
    } else {
        lblResult.content = "❌ " + errs.join("；");
        console.warn("validation failed", errs);
    }
}

function onReset() {
    txtName.value = "";
    txtEmail.value = "";
    lblResult.content = "点击校验按钮验证表单";
}

console.log("05-form-validation ready");
