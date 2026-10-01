using System;

namespace Game.Content
{
    /// <summary>Marks a type whose public members remote content scripts call; its signatures are part of the content format.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum, Inherited = false)]
    public sealed class ContentScriptApiAttribute : Attribute { }
}
