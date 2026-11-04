using System.Diagnostics.CodeAnalysis;

namespace SimpleJsonRpc.Tests.Data;

[method: SetsRequiredMembers]
public struct TwoNumbers(int a, int b)
{
    public required int A { get; set; } = a;
    public required int B { get; set; } = b;
}