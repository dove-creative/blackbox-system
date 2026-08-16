#if !NET6_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class InterpolatedStringHandlerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class InterpolatedStringHandlerArgumentAttribute : Attribute
    {
        public string[] Arguments { get; }
        public InterpolatedStringHandlerArgumentAttribute(string argument) => Arguments = new[] { argument };
    }
}
#endif
