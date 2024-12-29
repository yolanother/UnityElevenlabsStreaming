using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DoubTech.ElevenLabs.Streaming.Threading
{
    /// <summary>
    /// Base class for MonoBehaviour providing utility methods to handle asynchronous tasks
    /// on background and foreground threads.
    /// </summary>
    public class BaseAsyncMonoBehaviour : MonoBehaviour
    {
        private static SynchronizationContext mainThreadContext;

        protected virtual void Awake()
        {
            if (mainThreadContext == null)
            {
                mainThreadContext = SynchronizationContext.Current;
            }
        }

        /// <summary>
        /// Runs an action in the background thread.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        protected void RunInBackground(Action action)
        {
            ThreadUtils.RunInBackground(action);
        }

        /// <summary>
        /// Runs an action on the main thread.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        protected void RunOnMainThread(Action action)
        {
            ThreadUtils.RunOnMainThread(mainThreadContext, action);
        }

        /// <summary>
        /// Runs an asynchronous task in the background and safely logs errors if they occur.
        /// </summary>
        /// <param name="asyncTask">The asynchronous task to execute.</param>
        protected Task RunInBackground(Func<Task> asyncTask)
        {
            return ThreadUtils.RunInBackground(asyncTask);
        }

        /// <summary>
        /// Runs an asynchronous task in the background and safely logs errors if they occur, returning a result as a Task.
        /// </summary>
        /// <typeparam name="T">The return type of the asynchronous task.</typeparam>
        /// <param name="asyncTask">The asynchronous task to execute.</param>
        /// <returns>A Task representing the result of the operation.</returns>
        protected Task<T> RunInBackground<T>(Func<Task<T>> asyncTask)
        {
            return ThreadUtils.RunInBackground(asyncTask);
        }

        /// <summary>
        /// Runs a Task in the background and safely logs errors if they occur.
        /// </summary>
        /// <param name="task">The Task to execute.</param>
        protected Task RunInBackground(Task task)
        {
            return ThreadUtils.RunInBackground(() => task);
        }

        /// <summary>
        /// Runs an asynchronous task on the main thread.
        /// </summary>
        /// <param name="asyncTask">The asynchronous task to execute.</param>
        protected Task RunOnMainThread(Func<Task> asyncTask)
        {
            return ThreadUtils.RunOnMainThread(mainThreadContext, asyncTask);
        }

        /// <summary>
        /// Runs a Task on the main thread.
        /// </summary>
        /// <param name="task">The Task to execute.</param>
        protected Task RunOnMainThread(Task task)
        {
            return ThreadUtils.RunOnMainThread(mainThreadContext, () => task);
        }

        /// <summary>
        /// Runs an asynchronous task on the main thread and returns a result as a Task.
        /// </summary>
        /// <typeparam name="T">The return type of the asynchronous task.</typeparam>
        /// <param name="asyncTask">The asynchronous task to execute.</param>
        /// <returns>A Task representing the result of the operation.</returns>
        protected Task<T> RunOnMainThread<T>(Func<Task<T>> asyncTask)
        {
            return ThreadUtils.RunOnMainThread(mainThreadContext, asyncTask);
        }

        /// <summary>
        /// Runs a Task on the main thread and returns a result as a Task.
        /// </summary>
        /// <typeparam name="T">The return type of the Task.</typeparam>
        /// <param name="task">The Task to execute.</param>
        /// <returns>A Task representing the result of the operation.</returns>
        protected Task<T> RunOnMainThread<T>(Task<T> task)
        {
            return ThreadUtils.RunOnMainThread(mainThreadContext, () => task);
        }

        /// <summary>
        /// Operates as a coroutine and awaits the completion of the passed asynchronous task.
        /// </summary>
        /// <param name="asyncTask">The asynchronous task to await.</param>
        protected IEnumerator AwaitCoroutine(Func<Task> asyncTask)
        {
            var task = asyncTask();
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                Debug.LogError($"Exception in awaited coroutine task: {task.Exception}");
            }
        }

        /// <summary>
        /// Operates as a coroutine and awaits the completion of the passed asynchronous task with a result.
        /// </summary>
        /// <typeparam name="T">The return type of the asynchronous task.</typeparam>
        /// <param name="asyncTask">The asynchronous task to await.</param>
        /// <returns>The result of the asynchronous task.</returns>
        protected IEnumerator AwaitCoroutine<T>(Func<Task<T>> asyncTask, Action<T> onComplete = null)
        {
            var task = asyncTask();
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                Debug.LogError($"Exception in awaited coroutine task: {task.Exception}");
            }
            else
            {
                onComplete?.Invoke(task.Result);
            }
        }

        /// <summary>
        /// Operates as a coroutine and awaits the completion of the passed Task.
        /// </summary>
        /// <param name="task">The Task to await.</param>
        protected IEnumerator AwaitCoroutine(Task task)
        {
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                Debug.LogError($"Exception in awaited coroutine task: {task.Exception}");
            }
        }

        /// <summary>
        /// Operates as a coroutine and awaits the completion of the passed Task with a result.
        /// </summary>
        /// <typeparam name="T">The return type of the Task.</typeparam>
        /// <param name="task">The Task to await.</param>
        /// <param name="onComplete">The action to invoke upon completion.</param>
        protected IEnumerator AwaitCoroutine<T>(Task<T> task, Action<T> onComplete = null)
        {
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                Debug.LogError($"Exception in awaited coroutine task: {task.Exception}");
            }
            else
            {
                onComplete?.Invoke(task.Result);
            }
        }

        internal static void ExecuteOnMainThread(Action action)
        {
            mainThreadContext?.Post(_ => action(), null);
        }
    }

    /// <summary>
    /// Utility class to handle threading-related operations.
    /// </summary>
    public static class ThreadUtils
    {
        /// <summary>
        /// Runs an action in the background.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        public static void RunInBackground(Action action)
        {
            Task.Run(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            });
        }

        /// <summary>
        /// Runs an asynchronous task in the background and safely logs errors if they occur.
        /// </summary>
        /// <param name="asyncTask">The asynchronous task to execute.</param>
        public static Task RunInBackground(Func<Task> asyncTask)
        {
            return Task.Run(async () =>
            {
                try
                {
                    await asyncTask();
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                    throw;
                }
            });
        }

        /// <summary>
        /// Runs an asynchronous task in the background and safely logs errors if they occur, returning a result as a Task.
        /// </summary>
        /// <typeparam name="T">The return type of the asynchronous task.</typeparam>
        /// <param name="asyncTask">The asynchronous task to execute.</param>
        /// <returns>A Task representing the result of the operation.</returns>
        public static Task<T> RunInBackground<T>(Func<Task<T>> asyncTask)
        {
            return Task.Run(async () =>
            {
                try
                {
                    return await asyncTask();
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                    throw;
                }
            });
        }

        /// <summary>
        /// Runs a Task in the background and safely logs errors if they occur.
        /// </summary>
        /// <param name="task">The Task to execute.</param>
        public static Task RunInBackground(Task task)
        {
            return Task.Run(async () =>
            {
                try
                {
                    await task;
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                    throw;
                }
            });
        }

        /// <summary>
        /// Runs a Task in the background and safely logs errors if they occur, returning a result as a Task.
        /// </summary>
        /// <typeparam name="T">The return type of the Task.</typeparam>
        /// <param name="task">The Task to execute.</param>
        /// <returns>A Task representing the result of the operation.</returns>
        public static Task<T> RunInBackground<T>(Task<T> task)
        {
            return Task.Run(async () =>
            {
                try
                {
                    return await task;
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                    throw;
                }
            });
        }

        /// <summary>
        /// Runs an action on the main thread.
        /// </summary>
        /// <param name="context">The synchronization context to use for callbacks.</param>
        /// <param name="action">The action to execute.</param>
        public static void RunOnMainThread(SynchronizationContext context, Action action)
        {
            context.Post(_ =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }, null);
        }

        /// <summary>
        /// Runs an asynchronous task on the main thread.
        /// </summary>
        /// <param name="context">The synchronization context to use for callbacks.</param>
        /// <param name="asyncTask">The asynchronous task to execute.</param>
        public static Task RunOnMainThread(SynchronizationContext context, Func<Task> asyncTask)
        {
            var tcs = new TaskCompletionSource<bool>();

            context.Post(async _ =>
            {
                try
                {
                    await asyncTask();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                    tcs.SetException(ex);
                }
            }, null);

            return tcs.Task;
        }

        /// <summary>
        /// Runs an asynchronous task on the main thread and returns a result as a Task.
        /// </summary>
        /// <typeparam name="T">The return type of the asynchronous task.</typeparam>
        /// <param name="context">The synchronization context to use for callbacks.</param>
        /// <param name="asyncTask">The asynchronous task to execute.</param>
        /// <returns>A Task representing the result of the operation.</returns>
        public static Task<T> RunOnMainThread<T>(SynchronizationContext context, Func<Task<T>> asyncTask)
        {
            var tcs = new TaskCompletionSource<T>();

            context.Post(async _ =>
            {
                try
                {
                    var result = await asyncTask();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                    tcs.SetException(ex);
                }
            }, null);

            return tcs.Task;
        }
    }
}
