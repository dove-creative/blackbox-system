using System;
using System.Collections.Generic;
using System.Text;

namespace BlackThunder.BlackboxSystem
{
    internal static class TypeDisplayName
    {
        internal static string Format(Type type)
        {
            if (type.IsArray) return Format(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            if (type.IsByRef) return Format(type.GetElementType()) + "&";
            if (type.IsPointer) return Format(type.GetElementType()) + "*";
            if (type.IsGenericParameter) return type.Name;
            var chain = new Stack<Type>();
            for (var current = type; current != null; current = current.DeclaringType) chain.Push(current);
            var arguments = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
            var result = new StringBuilder();
            int argument = 0;
            while (chain.Count > 0)
            {
                var part = chain.Pop();
                if (result.Length > 0) result.Append('.');
                var name = part.Name;
                int tick = name.LastIndexOf('`');
                int count = 0;
                if (tick >= 0 && int.TryParse(name.Substring(tick + 1), out count)) name = name.Substring(0, tick);
                result.Append(name);
                if (count == 0) continue;
                result.Append('<');
                for (int i = 0; i < count; i++)
                {
                    if (i > 0) result.Append(", ");
                    result.Append(Format(arguments[argument++]));
                }
                result.Append('>');
            }
            return result.ToString();
        }
    }
}
