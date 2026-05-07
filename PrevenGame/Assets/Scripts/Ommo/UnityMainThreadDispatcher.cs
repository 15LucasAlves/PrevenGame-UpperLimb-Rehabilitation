using UnityEngine;
using System;
using System.Collections.Generic;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    static readonly Queue<Action> _queue = new Queue<Action>();
    static UnityMainThreadDispatcher _instance;

    public static void Enqueue(Action action)
    {
        if (action == null) return;
        lock (_queue) { _queue.Enqueue(action); }
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        lock (_queue)
            while (_queue.Count > 0)
                _queue.Dequeue()?.Invoke();
    }
}
