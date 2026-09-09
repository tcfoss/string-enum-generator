namespace TcfOss.StringEnumGenerator
{
    /// <summary>
    /// Marks a class as a string enum type allowing given string values. Each allowed value
    /// should get its own attribute usage.
    ///
    /// If no VariableName is given, the Value will be converted to PascalCase and used
    /// that as the variable name.
    ///
    /// EquivalentValues can be used to specify alternative string values that map onto
    /// the same enum-like variable (for example, in MariaDB, routine parameter direction
    /// can be "INOUT" or "IN OUT", which mean the same thing; one of these goes into
    /// EquivalentValues).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class StringEnumAttribute(string value, string? variableName = null, params string[]? equivalentValues) : Attribute
    {
        public string Value { get; } = value;
        public string VariableName { get; } = variableName ?? value.ToVariableName();
        public string[] EquivalentValues { get; } = equivalentValues ?? [];
    }
}
