using System;
using System.Collections.Generic;

public partial class UIManager
{
    struct WindowInfo
    {
        public UIWindowType windowType;
        public UIWindowLayer windowLayer;
        public Type componentType;
        public bool addUIMask;
    }

    private Dictionary<string, WindowInfo> _windowsInfo = new Dictionary<string, WindowInfo>(64);

    private void allWindowMeta()
    {
        // basic
        _WindowMeta(UINameConst.UILogin, UIWindowLayer.Normal);
    }


    private void _WindowMeta(string name, UIWindowLayer windowLayer, UIWindowType windowType = UIWindowType.Normal,
        bool addUIMask = false, Type componentType = null)
    {
        _windowsInfo.Add(name,
            new WindowInfo { windowType = windowType, windowLayer = windowLayer, addUIMask = addUIMask, componentType = componentType });
    }

    private void _WindowMeta(string name, UIWindowLayer windowLayer, bool addUIMask, Type componentType = null)
    {
        _windowsInfo.Add(name,
            new WindowInfo { windowType = UIWindowType.Normal, windowLayer = windowLayer, addUIMask = addUIMask, componentType = componentType });
    }

    public void _WindowMetaPublic(string name, UIWindowLayer windowLayer, bool addUIMask)
    {
        if (_windowsInfo.ContainsKey(name))
            return;
        _windowsInfo.Add(name,
            new WindowInfo { windowType = UIWindowType.Normal, windowLayer = windowLayer, addUIMask = addUIMask });
    }
}

public static class UIAnimationConst
{
    public static readonly string Appear = "appear";
    public static readonly string DisAppear = "disappear";
}

public static partial class UINameConst
{
    public static readonly string UILogin = "Home/UILogin";
    public static readonly string MergeMain = "Home/MergeMain";
}