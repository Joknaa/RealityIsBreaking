﻿using System;
using System.ComponentModel;

namespace SuperTiled2Unity.Editor {
    public static class TypeExtensions {
        public static string GetDisplayName(this Type type) {
            var attribute = Attribute.GetCustomAttribute(type, typeof(DisplayNameAttribute)) as DisplayNameAttribute;

            if (attribute != null) return attribute.DisplayName;

            return type.Name;
        }
    }
}