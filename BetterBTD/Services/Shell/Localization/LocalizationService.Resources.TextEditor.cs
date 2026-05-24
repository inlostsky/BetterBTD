using System;
using System.Collections.Generic;

namespace BetterBTD.Services.Shell.Localization;

public sealed partial class LocalizationService
{
    private static Dictionary<string, string> BuildZhCnTextEditorResources() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["TextEditor.WindowTitle"] = "文本编辑器",
        ["TextEditor.Menu.Save"] = "保存",
        ["TextEditor.Menu.Reload"] = "重新加载",
        ["TextEditor.Menu.Close"] = "关闭",
        ["TextEditor.Status.Loading"] = "正在加载编辑器...",
        ["TextEditor.Status.Ready"] = "已就绪",
        ["TextEditor.Status.Modified"] = "文件已修改，尚未保存",
        ["TextEditor.Status.Saved"] = "已保存",
        ["TextEditor.Status.Error"] = "编辑器错误：{0}",
        ["TextEditor.Unsaved.Title"] = "未保存的更改",
        ["TextEditor.Unsaved.Message"] = "当前文件有未保存的修改，是否先保存？",
        ["TextEditor.Unsaved.Save"] = "保存",
        ["TextEditor.Unsaved.Discard"] = "不保存",
        ["TextEditor.Unsaved.Cancel"] = "取消",
        ["TextEditor.SaveError.Title"] = "保存文件",
        ["TextEditor.SaveError.Message"] = "保存文件失败。\n\n{0}",
        ["TextEditor.Dialog.Ok"] = "确定"
    };

    private static Dictionary<string, string> BuildEnUsTextEditorResources() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["TextEditor.WindowTitle"] = "Text Editor",
        ["TextEditor.Menu.Save"] = "Save",
        ["TextEditor.Menu.Reload"] = "Reload",
        ["TextEditor.Menu.Close"] = "Close",
        ["TextEditor.Status.Loading"] = "Loading editor...",
        ["TextEditor.Status.Ready"] = "Ready",
        ["TextEditor.Status.Modified"] = "File modified and not saved",
        ["TextEditor.Status.Saved"] = "Saved",
        ["TextEditor.Status.Error"] = "Editor error: {0}",
        ["TextEditor.Unsaved.Title"] = "Unsaved Changes",
        ["TextEditor.Unsaved.Message"] = "The current file has unsaved changes. Save them first?",
        ["TextEditor.Unsaved.Save"] = "Save",
        ["TextEditor.Unsaved.Discard"] = "Discard",
        ["TextEditor.Unsaved.Cancel"] = "Cancel",
        ["TextEditor.SaveError.Title"] = "Save File",
        ["TextEditor.SaveError.Message"] = "Failed to save file.\n\n{0}",
        ["TextEditor.Dialog.Ok"] = "OK"
    };
}
