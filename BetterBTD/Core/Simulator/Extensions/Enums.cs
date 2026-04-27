using System;
using System.Collections.Generic;
using System.Text;

namespace BetterBTD.Core.Simulator.Extensions;

/// <summary>
/// 模拟按键类型
/// </summary>
public enum KeyType
{

    /// <summary>
    /// 单击按键（KeyPress）
    /// </summary>
    KeyPress,

    /// <summary>
    /// 按住按键（KeyDown）
    /// </summary>
    KeyDown,

    /// <summary>
    /// 释放按键（KeyUp）
    /// </summary>
    KeyUp,

    /// <summary>
    /// 长按1s
    /// </summary>
    Hold,

}

/// <summary>
/// BTD按键动作
/// </summary>
public enum BTDActions
{

}
