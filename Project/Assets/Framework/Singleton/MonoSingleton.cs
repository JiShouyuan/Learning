using UnityEngine;
public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static bool m_ShuttingDown = false;
    private static object m_Lock = new object();
    private static T m_Instance;
    public static T Instance
    {
        get
        {
            lock (m_Lock)
            {
                if (m_Instance == null)
                {
                    m_Instance = (T)FindObjectOfType(typeof(T));
                    if (m_Instance == null)
                    {
                        var singletonObject = new GameObject();
                        m_Instance = singletonObject.AddComponent<T>();
                        singletonObject.name = typeof(T) + " (Singleton)";
                        //singletonObject.hideFlags = HideFlags.HideInHierarchy;
                        if (Application.isPlaying) DontDestroyOnLoad(singletonObject);
                        (m_Instance as MonoSingleton<T>).InitImmediately();
                    }
                }

                return m_Instance;
            }
        }
    }

    // ����������override������Instance���������̵���, �������������Ĭ�ϳ�ʼ��һЩ����
    protected virtual void InitImmediately()
    {
    }

    private void OnApplicationQuit()
    {
        m_ShuttingDown = true;
        m_Instance = null;
    }
    private void OnDestroy()
    {
    }
}
