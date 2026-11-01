using RoomManagement.Business.Errors;
using RoomManagement.Business.Services;
using Xunit;

namespace RoomManagement.Business.Tests;

public sealed class RoomNumberingTests
{
    [Theory]
    [InlineData(null, 1, 1, "101")]
    [InlineData(null, 2, 20, "220")]
    [InlineData("A", null, 2, "A02")]
    [InlineData("A", 1, 3, "A103")]
    [InlineData("B", 2, 1, "B201")]
    public void Format_UsesScopePrefix(string? rowCode, int? floor, int sequence, string expected) =>
        Assert.Equal(expected, RoomNumbering.Format(rowCode, floor, sequence));

    [Theory]
    [InlineData(null, null, 1)]
    [InlineData(null, 1, 0)]
    [InlineData("A", 1, 100)]
    [InlineData("A", -1, 1)]
    public void Format_RejectsInvalidScopeOrSequence(string? rowCode, int? floor, int sequence) =>
        Assert.Throws<BusinessException>(() => RoomNumbering.Format(rowCode, floor, sequence));
}
