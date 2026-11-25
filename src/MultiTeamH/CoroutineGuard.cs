using System;
using System.Collections;

namespace MultiTeamH {
    // Unity advances iterators over multiple frames. A try/catch around
    // StartCoroutine only protects construction, not later MoveNext calls.
    // This guards the supplied iterator only. Its two callers yield null;
    // nested IEnumerator/CustomYieldInstruction values would be advanced by
    // Unity separately and need their own guard or polling in the parent.
    internal static class CoroutineGuard {
        internal static IEnumerator Run(IEnumerator routine, Action<Exception> onError, Action onFinished) {
            try {
                while (true) {
                    bool moved = false;
                    object current = null;
                    Exception failure = null;
                    try {
                        moved = routine.MoveNext();
                        if (moved) current = routine.Current;
                    } catch (Exception ex) { failure = ex; }
                    if (failure != null) {
                        if (onError != null) onError(failure);
                        yield break;
                    }
                    if (!moved) yield break;
                    yield return current;
                }
            } finally {
                try {
                    IDisposable disposable = routine as IDisposable;
                    if (disposable != null) disposable.Dispose();
                } finally { if (onFinished != null) onFinished(); }
            }
        }
    }
}
