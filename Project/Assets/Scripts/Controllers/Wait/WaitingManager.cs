﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaitingManager : MonoSingleton<WaitingManager>
{
    public void OpenWindow(float timeOut = 30.0f, float delayTime = 0.0f, Action onTimeout = null)
    {
        WaitingController dlg = UIManager.Instance.OpenView(UINameConst.UIWaiting, timeOut, delayTime) as WaitingController;
        dlg.FinishAction = onTimeout;
    }

    public void CloseWindow()
    {
        UIManager.Instance.CloseWindow(UINameConst.UIWaiting, true);
    }
}