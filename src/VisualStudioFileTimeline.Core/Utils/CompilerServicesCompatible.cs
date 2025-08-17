namespace System.Diagnostics.CodeAnalysis
{
    public sealed class SetsRequiredMembersAttribute : Attribute
    {
    }
}

namespace System.Runtime.CompilerServices
{
    public sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
    {
        #region Public 属性

        public string FeatureName { get; } = featureName;

        public bool IsOptional { get; }

        #endregion Public 属性
    }

    public sealed class RequiredMemberAttribute : Attribute
    {
    }
}
