用户名/密码登录：演示 TextBox 输入读取与状态文案更新。

# 03-login

控件：`text-box × 2`、`button`、`fancy-text`。
脚本读取 `txtUser.value` / `txtPass.value`，根据规则切换状态显示。

> 校验规则：用户名 = `admin`，密码 ≥ 4 字符。

启动：

```powershell
.\demos\run.ps1 -Name login
```
