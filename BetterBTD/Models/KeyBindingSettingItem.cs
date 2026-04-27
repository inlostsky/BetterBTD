using System.Collections.ObjectModel;
using BetterBTD.Core.Config;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BetterBTD.Models;

public partial class KeyBindingSettingItem : ObservableObject
{
    /// <summary>
    /// 按键绑定值
    /// </summary>
    [ObservableProperty]
    private HotkeyBinding _keyValue = new();

    [ObservableProperty]
    private ObservableCollection<KeyBindingSettingItem> _children = [];

    public string ActionName { get; }

    public bool IsExpanded { get; set; } = true;

    /// <summary>
    /// 界面上显示是文件夹而不是按键绑定
    /// </summary>
    [ObservableProperty]
    private bool _isDirectory;

    public string ConfigPropertyName { get; }

    public KeyBindingSettingItem(string name)
    {
        IsDirectory = true;
        ActionName = name;
        ConfigPropertyName = string.Empty;
    }

    public KeyBindingSettingItem(string actionName, string configPropertyName, HotkeyBinding keyValue)
    {
        ActionName = actionName;
        ConfigPropertyName = configPropertyName;
        KeyValue = keyValue;
        IsDirectory = false;
    }
}
