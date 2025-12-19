using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.BasicFunctions.Shared;

public class EntityExtensionsTests
{
    [Fact]
    public void GetColumnName_ShouldReturnColumnName()
    {
        var result = EntityExtensions.GetColumnName<TestEntity>(x => x.TestColumn);

        Assert.Equal(NasIdentifier, result);
    }

    [Fact]
    public void GetColumnName_ShouldReturnIDColumn()
    {
        var result = EntityExtensions.GetColumnName<TestEntity>(x => x.Id);

        Assert.Equal(nameof(TestEntity.Id), result);
    }

    [Fact]
    public void GetTableName_ShouldReturnTableName()
    {
        var result = EntityExtensions.GetTableName<TestEntity>();

        Assert.Equal(Nas, result);
    }

    private const string NasIdentifier = "nasidentifier";
    private const string Nas = "nas";

    [Table(Nas)]
    private class TestEntity
    {
        [Key]
        public int Id { get; }

        [Column(NasIdentifier)]
        public int TestColumn { get; }
    }
}
