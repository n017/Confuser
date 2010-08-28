using System;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Module |
                AttributeTargets.Class | AttributeTargets.Delegate | AttributeTargets.Enum | AttributeTargets.Interface | AttributeTargets.Struct |
                AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Property,
                AllowMultiple = false)]
public sealed class ConfuseAttribute : Attribute
{
    string cfg;
    public string Configuration { get { return cfg; } set { cfg = value; } }
}