using BlogHelper9000.Core.Scheduling;

namespace BlogHelper9000.Tests.Scheduling;

public class ScheduleDatabaseTests
{
    [Fact]
    public void OpenInMemory_CreatesSchemaAtVersion1()
    {
        using var db = ScheduleDatabase.OpenInMemory();

        db.SchemaVersion.Should().Be(1);
    }

    [Fact]
    public void Migrate_IsIdempotent_ForAnAlreadyMigratedDatabase()
    {
        using var db = ScheduleDatabase.OpenInMemory();

        var act = () => db.SchemaVersion;

        act.Should().NotThrow();
        db.SchemaVersion.Should().Be(1);
    }

    [Fact]
    public void PathFor_AppendsDotPrefixedFileNameToBlogRoot()
    {
        ScheduleDatabase.PathFor("/blog").Should().Be(Path.Combine("/blog", ".bloghelper.db"));
    }
}
