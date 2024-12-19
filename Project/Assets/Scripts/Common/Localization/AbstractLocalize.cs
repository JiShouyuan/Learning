﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Framework
{
    public abstract class AbstractLocalize : MonoBehaviour
    {
        [SerializeField] protected string Term;

        protected string CurrLocalize = string.Empty;
        public string Key { get { return Term; } set { Term = value; } }

        protected abstract void Localize();

        private void OnEnable()
        {
            if (NeedChangeLocalize())
                Localize();
        }

        private void Start()
        {
            Localize();
        }

        public void SetTerm(string term)
        {
            this.Term = term;
            Localize();
        }

        public string GetTerm()
        {
            return this.Term;
        }
        
        public object[] TermFormats;
        public void SetTermFormats(params object[] values)
        {
            TermFormats = values;
            if (Term == null)
            {
                Debug.LogError("没有初始Term");
                return;
            }
            Localize();
        }

        private bool NeedChangeLocalize()
        {
            return CurrLocalize != LocalizationManager.Instance.GetCurrentLocale();
        }
    }
}
