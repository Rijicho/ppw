using UnityEngine;
using System;

namespace RUtil
{
    public class SingletonMonoBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        protected SingletonMonoBehaviour() { }
        private static T mInstance;
        public static T Instance
        {
            get
            {
                mInstance = mInstance ? mInstance : FindObjectOfType<T>();
                return mInstance ? mInstance : (mInstance = (new GameObject(typeof(T).ToString())).AddComponent<T>());
            }
        }
        public static void Make()
        {
            mInstance = mInstance ? mInstance : FindObjectOfType<T>();
            mInstance = mInstance ? mInstance : new GameObject(typeof(T).ToString()).AddComponent<T>();
        }
        virtual protected void Awake()
        {
            if (mInstance && mInstance != this)
            {
                Destroy(this.gameObject);
            }
            DontDestroyOnLoad(this);
        }
        virtual protected void OnDestroy()
        {
            if (this == mInstance)
                mInstance = null;
        }
    }



    public abstract class Singleton<T> where T : class, new()
    {
        static readonly T mInstance = new T();
        public static T Instance => mInstance;
        protected Singleton()
        {
            if (mInstance != null)
                throw new InvalidOperationException("Singleton instance already exists.");
        }
    }
}
