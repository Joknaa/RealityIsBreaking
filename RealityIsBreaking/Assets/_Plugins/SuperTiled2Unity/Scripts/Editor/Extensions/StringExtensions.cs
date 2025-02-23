﻿using System;
using UnityEngine;

namespace SuperTiled2Unity.Editor {
    public static class StringExtensions {
        public static bool IsNullOrWhiteSpace(this string value) {
#if NET_LEGACY
            // From: https://referencesource.microsoft.com/#mscorlib/system/string.cs
            if (value == null) return true;

            for (int i = 0; i < value.Length; i++)
            {
                if (!Char.IsWhiteSpace(value[i])) return false;
            }

            return true;
#else
            return string.IsNullOrWhiteSpace(value);
#endif
        }

        public static byte[] Base64ToBytes(this string data) {
            var bytes = Convert.FromBase64String(data);
            return bytes;
        }

        public static string SanitizePath(this string path) {
            if (string.IsNullOrEmpty(path)) return string.Empty;

            return path.Replace('\\', '/');
        }

        public static void CopyToClipboard(this string str) {
            var te = new TextEditor();
            te.text = str;
            te.SelectAll();
            te.Copy();
        }
    }
}