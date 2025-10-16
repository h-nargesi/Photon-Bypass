using PhotonBypass.Tools;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Test.BasicFunctions.Shared;

public class EntityExtensionsTests
{
    [Fact]
    public void GetColumnName_ShouldReturnColumnName()
    {
        var result = EntityExtensions.GetColumnName<TestEntity>(x => x.TestColumn);

        Assert.Equal(nasidentifier, result);
    }

    [Fact]
    public void GetColumnName_ShouldReturnIDColumn()
    {
        var result = EntityExtensions.GetColumnName<TestEntity>(x => x.ID);

        Assert.Equal(nameof(TestEntity.ID), result);
    }

    [Fact]
    public void GetTableName_ShouldReturnTableName()
    {
        var result = EntityExtensions.GetTablename<TestEntity>();

        Assert.Equal(nas, result);
    }

    const string nasidentifier = "nasidentifier";
    const string nas = "nas";

    [Table(nas)]
    class TestEntity
    {
        [Key]
        public int ID { get; set; }

        [Column(nasidentifier)]
        public int TestColumn { get; set; }
    }
}
