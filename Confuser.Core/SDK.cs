using System;

[AttributeUsage(AttributeTargets.All & ~AttributeTargets.GenericParameter & ~AttributeTargets.Parameter & ~AttributeTargets.ReturnValue, AllowMultiple = false)]
class ConfusingAttribute : Attribute
{
    public ConfusingAttribute()
    {
        ApplyToMembers = true;
        Exclude = false;
        Config = "default";
        StripAfterObfuscation = true;
    }
    public bool ApplyToMembers { get; set; }
    public bool Exclude { get; set; }
    public string Config { get; set; }
    public bool StripAfterObfuscation { get; set; }
}

[AttributeUsage(AttributeTargets.Assembly)]
class PackerAttribute : Attribute
{
    public PackerAttribute()
    {
        Config = "";
        StripAfterObfuscation = true;
    }
    public string Config { get; set; }
    public bool StripAfterObfuscation { get; set; }
}