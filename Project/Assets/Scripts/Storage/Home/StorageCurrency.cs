/************************************************
 * Storage class : StorageCurrency
 * This file is can not be modify !!!
 * If there is some problem, ask bin.guo.
 ************************************************/

using System;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Framework.Storage
{
    [System.Serializable]
    public class StorageCurrency : StorageBase
    {
         
        // 金币的加密存储
        [JsonProperty]
        float _vc0;
        [JsonProperty]
        int _vc1;

        public void SetValue(int value)
        {
            if (GetValue() == value)
            {
                return;
            }
            if(value <= 0)
            {
                _vc0 = 0.0f;
                _vc1 = 0;
            }
            else
            {
                _vc1 = (int)Math.Floor(UnityEngine.Random.Range(0.0f, 1.0f) * value);
                _vc0 = (value - _vc1) / 8.0f;
            }
            StorageManager.Instance.LocalVersion++;
            StorageManager.Instance.SyncForce = true;
            
        }

        public int GetValue()
        {
            return (int)Math.Round(8.0f * _vc0 + _vc1);
        }
        // ---------------------------------//
        
    }
}