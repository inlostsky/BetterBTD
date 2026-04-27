using BetterBTD.Core.Config;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace BetterBTD.Core.Simulator.Extensions;

public static class SimulateKeyHelper
{

    //private static KeyBindingsConfig KeyConfig => TaskContext.Instance().Config.KeyBindingsConfig;

    public static KeyId ToActionKey(this BTDActions action)
    {
        return GetActionKey(action);
    }

    /// <summary>
    /// 通过ActionId取得实际的键盘绑定
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public static KeyId GetActionKey(BTDActions action)
    {
        return action switch
        {

            _ => default,
        };
    }

}
