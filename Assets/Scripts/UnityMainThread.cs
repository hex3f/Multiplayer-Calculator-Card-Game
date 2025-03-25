using UnityEngine;
using System.Collections.Generic;
using System;

public class UnityMainThread : MonoBehaviour
{
    private static UnityMainThread instance;
    private static readonly Queue<Action> executeOnMainThread = new Queue<Action>();
    private static readonly object queueLock = new object();

    public static UnityMainThread Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<UnityMainThread>();
                if (instance == null)
                {
                    GameObject go = new GameObject("UnityMainThread");
                    instance = go.AddComponent<UnityMainThread>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    public static void Execute(Action action)
    {
        if (action == null) return;

        lock (queueLock)
        {
            executeOnMainThread.Enqueue(action);
        }
    }

    private void Update()
    {
        lock (queueLock)
        {
            while (executeOnMainThread.Count > 0)
            {
                executeOnMainThread.Dequeue().Invoke();
            }
        }
    }
} 