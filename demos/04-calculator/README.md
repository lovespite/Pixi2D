4 函数计算器：演示多按钮事件分发与共享状态机。

# 04-calculator

控件：`button × 16`、`fancy-text(display)`。
脚本中维护 `current / previous / op` 三态，加减乘除与等号串接计算。

启动：

```powershell
.\demos\run.ps1 -Name calculator
```
