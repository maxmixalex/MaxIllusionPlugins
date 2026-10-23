using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MaximumUI {
    public enum DebugLogLevel {
        Info,
        Warning,
        Error
    }

    public sealed class DebugLogEntry {
        public DateTime Time;
        public DebugLogLevel Level;
        public string Message;

        public DebugLogEntry() {
            Time = DateTime.Now;
            Level = DebugLogLevel.Info;
            Message = string.Empty;
        }
    }

    public static class DebugLog {
        private static readonly List<DebugLogEntry> entries = new List<DebugLogEntry>();

        public static int MaxEntries = 200;

        public static void Info(string message) {
            Add(DebugLogLevel.Info, message);
        }

        public static void Warning(string message) {
            Add(DebugLogLevel.Warning, message);
        }

        public static void Error(string message) {
            Add(DebugLogLevel.Error, message);
        }

        public static void Add(DebugLogLevel level, string message) {
            entries.Add(new DebugLogEntry {
                Time = DateTime.Now,
                Level = level,
                Message = message ?? string.Empty
            });

            int max = MaxEntries <= 0 ? 200 : MaxEntries;
            while (entries.Count > max) entries.RemoveAt(0);
        }

        public static void Clear() {
            entries.Clear();
        }

        public static IList<DebugLogEntry> Snapshot() {
            return new List<DebugLogEntry>(entries);
        }

        public static string LogToMemory() {
            return FormatEntries(entries);
        }

        public static void LogToFile(string path) {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path");
            File.WriteAllText(path, LogToMemory(), Encoding.UTF8);
        }

        public static void LogToClipboard() {
            GUIUtility.systemCopyBuffer = LogToMemory();
        }

        public static string FormatEntries(IEnumerable<DebugLogEntry> source) {
            StringBuilder builder = new StringBuilder();
            if (source == null) return string.Empty;

            foreach (DebugLogEntry entry in source) {
                if (entry == null) continue;
                builder.Append(entry.Time.ToString("yyyy-MM-dd HH:mm:ss"));
                builder.Append(" [");
                builder.Append(entry.Level.ToString());
                builder.Append("] ");
                builder.Append(entry.Message ?? string.Empty);
                builder.Append(Environment.NewLine);
            }

            return builder.ToString();
        }
    }

    public sealed class RuntimeMetrics {
        public int FrameCount;
        public int MountCount;
        public int VisibleMountCount;
        public int PendingCallbackCount;
        public bool IsMouseOverAnyWindow;
        public bool IsAnyTextFieldFocused;
        public bool IsModalOpen;
    }

    public sealed class UiStateEntry {
        public string Id;
        public string TypeName;
    }
}
