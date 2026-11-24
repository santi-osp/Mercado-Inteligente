using Xunit;

namespace MercadoInteligente.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class EnvironmentVariableFactAttribute : FactAttribute
{
    public EnvironmentVariableFactAttribute(string variableName, string expectedValue = "1")
    {
        if (Environment.GetEnvironmentVariable(variableName) != expectedValue)
        {
            Skip = $"SKIPPED — {variableName} not enabled";
        }
    }
}