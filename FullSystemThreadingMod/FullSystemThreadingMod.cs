using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FullSystemThreadingMod
{
    public class ThreadManager : MonoBehaviour
    {
        public static ThreadManager Instance;

        private BlockingCollection<Action> _workQueue;
        private ConcurrentQueue<Action> _resultQueue;

        private CancellationTokenSource _cts;
        private Task[] _workers;
        private int _workerCount;

        public void Init()
        {
            Instance = this;
            _cts = new CancellationTokenSource();
            _workerCount = Mathf.Clamp(Environment.ProcessorCount - 1, 1, 6);

            _workQueue = new BlockingCollection<Action>(new ConcurrentQueue<Action>());
            _resultQueue = new ConcurrentQueue<Action>();

            _workers = new Task[_workerCount];
            for (int i = 0; i < _workerCount; i++)
            {
                _workers[i] = Task.Run(() => BackgroundWorker(_cts.Token));
            }

            Debug.Log($"[ThreadManager] Initialized with {_workerCount} threads.");
        }

        public void Shutdown()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            _workQueue?.CompleteAdding();
            _workQueue?.Dispose();
            _workQueue = null;

            _resultQueue = null;

            if (_workers != null)
            {
                foreach (var t in _workers)
                    t?.Dispose();
                _workers = null;
            }

            Instance = null;
            Debug.Log("[ThreadManager] Shutdown complete.");
        }

        private void BackgroundWorker(CancellationToken token)
        {
            try
            {
                foreach (var work in _workQueue.GetConsumingEnumerable(token))
                {
                    try { work?.Invoke(); }
                    catch (Exception e) { Debug.LogError($"[ThreadManager] Worker exception: {e}"); }
                }
            }
            catch (OperationCanceledException) { /* 정상 종료 */ }
        }

        public void EnqueueWork(Action work)
        {
            if (!_workQueue.IsAddingCompleted)
                _workQueue.Add(work);
        }

        public void EnqueueResult(Action result)
        {
            _resultQueue.Enqueue(result);
        }

        void Update()
        {
            int maxPerFrame = 100;
            int count = 0;

            while (_resultQueue.TryDequeue(out var r))
            {
                try { r?.Invoke(); }
                catch (Exception e) { Debug.LogError(e); }

                count++;
                if (count >= maxPerFrame) break;
            }
        }
    }

    public static class Main
    {
        private static GameObject _go;

        public static void Load()
        {
            _go = new GameObject("ThreadManager");
            UnityEngine.Object.DontDestroyOnLoad(_go);
            _go.hideFlags = HideFlags.HideAndDontSave;

            var manager = _go.AddComponent<ThreadManager>();
            manager.Init();
        }

        public static void Unload()
        {
            if (_go != null)
            {
                var manager = _go.GetComponent<ThreadManager>();
                manager?.Shutdown();
                UnityEngine.Object.Destroy(_go);
                _go = null;
            }
        }
    }
}
