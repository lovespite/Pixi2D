// 07-modal-flow — Modal show/hide
dlg.hide();

function onAskDelete() { dlg.show(); console.log("modal opened"); }
function onCancel()    { dlg.hide(); lblStatus.content = "操作记录: 已取消"; console.warn("cancelled"); }
function onConfirm()   { dlg.hide(); lblStatus.content = "操作记录: ✅ 已删除 3 个文件"; console.log("deleted"); }

console.log("07-modal-flow ready");
