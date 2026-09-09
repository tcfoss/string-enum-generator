namespace TcfOss.StringEnumGenerator.Tests;

public class StringEnumAttributeTests
{
    private static readonly string[] s_expectedMultiple = ["Equivalent1", "Equivalent2", "Equivalent3"];
    private static readonly string[] s_expectedOne = ["Equivalent1"];

    [Fact]
    public void SetAll_OneEquivalent()
    {
        var attribute = new StringEnumAttribute("TEST_VALUE", "TestVariable", "Equivalent1");

        Assert.Equal("TEST_VALUE", attribute.Value);
        Assert.Equal("TestVariable", attribute.VariableName);
        Assert.Equal(s_expectedOne, attribute.EquivalentValues);
    }

    [Fact]
    public void SetAll_MultipleEquivalents()
    {
        var attribute = new StringEnumAttribute("TEST_VALUE", "TestVariable", "Equivalent1", "Equivalent2", "Equivalent3");

        Assert.Equal("TEST_VALUE", attribute.Value);
        Assert.Equal("TestVariable", attribute.VariableName);
        Assert.Equal(s_expectedMultiple, attribute.EquivalentValues);
    }

    [Fact]
    public void SetValueAndName_EquivalentsEmptyArray()
    {
        var attribute = new StringEnumAttribute("TEST_VALUE", "TestVariable");

        Assert.Equal("TEST_VALUE", attribute.Value);
        Assert.Equal("TestVariable", attribute.VariableName);
        Assert.NotNull(attribute.EquivalentValues);
        Assert.Empty(attribute.EquivalentValues);
    }

    [Fact]
    public void SetValueAndName_EquivalentsNull_EquivalentsEmptyArray()
    {
        var attribute = new StringEnumAttribute("TEST_VALUE", "TestVariable", null);

        Assert.Equal("TEST_VALUE", attribute.Value);
        Assert.Equal("TestVariable", attribute.VariableName);
        Assert.NotNull(attribute.EquivalentValues);
        Assert.Empty(attribute.EquivalentValues);
    }

    [Fact]
    public void SetValueOnly_NameGenerated_EquivalentsEmptyArray()
    {
        var attribute = new StringEnumAttribute("TEST_VALUE");

        Assert.Equal("TEST_VALUE", attribute.Value);
        Assert.Equal("TestValue", attribute.VariableName);
        Assert.NotNull(attribute.EquivalentValues);
        Assert.Empty(attribute.EquivalentValues);
    }
}
