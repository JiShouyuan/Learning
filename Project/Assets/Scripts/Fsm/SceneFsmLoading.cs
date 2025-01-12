﻿using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine;
using DG.Tweening;
using Framework.Asset;

public class SceneFsmLoading : IFsmState
{
    public StatusType Type => StatusType.Loading;
    private bool m_IsLoading = false;

    private StatusType nextType = StatusType.Non;
    private System.Object[] nextParms = null;
    public void Enter(params object[] objs)
    {
        if (objs.Length > 0)
        {
            nextType = (StatusType) objs[0];

            if (objs.Length > 1)
            {
                nextParms = new System.Object[objs.Length - 1];
                for (int i = 1; i < objs.Length; i++)
                {
                    nextParms[i - 1] = objs[i];
                }
            }
        }

        EventDispatcher.Instance.DispatchEventImmediately(EventEnum.LOADING_START);
        EventDispatcher.Instance.AddEventListener(EventEnum.LOADING_FINISH, LoadingFinish);
        ResourcesManager.Instance.UnLoadAllCache();
    }
    public void Update(float deltaTime)
    {
    }
    public void LateUpdate(float deltaTime)
    {
        
    }
    public void Exit()
    {
        EventDispatcher.Instance.RemoveEventListener(EventEnum.LOADING_FINISH, LoadingFinish);
    }

    private void LoadingFinish(BaseEvent e)
    {
        SceneFsm.Instance.ClientInited = true;
        SceneFsm.Instance.ChangeState(nextType, nextParms);
    }
}