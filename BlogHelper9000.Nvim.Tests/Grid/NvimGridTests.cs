using BlogHelper9000.Nvim.Grid;
using BlogHelper9000.Nvim.UiEvents;
using FluentAssertions;

namespace BlogHelper9000.Nvim.Tests.Grid;

public class NvimGridTests
{
    [Fact]
    public void New_Grid_Has_Correct_Dimensions()
    {
        var grid = new NvimGrid(80, 24);

        grid.Width.Should().Be(80);
        grid.Height.Should().Be(24);
    }

    [Fact]
    public void New_Grid_Is_All_Spaces()
    {
        var grid = new NvimGrid(10, 5);

        grid.GetRowText(0).Should().Be("          ");
    }

    [Fact]
    public void New_Grid_Has_All_Rows_Dirty()
    {
        var grid = new NvimGrid(10, 5);

        grid.DirtyRows.Should().HaveCount(5);
    }

    [Fact]
    public void ClearDirtyRows_Resets_Tracking()
    {
        var grid = new NvimGrid(10, 5);
        grid.ClearDirtyRows();

        grid.DirtyRows.Should().BeEmpty();
    }

    [Fact]
    public void ApplyLine_Places_Text_At_Correct_Position()
    {
        var grid = new NvimGrid(20, 5);
        grid.ClearDirtyRows();

        var line = new GridLineEvent(1, 0, 0, [
            new GridLineCell("H", 0, 1),
            new GridLineCell("e", 0, 1),
            new GridLineCell("l", 0, 1),
            new GridLineCell("l", 0, 1),
            new GridLineCell("o", 0, 1),
        ]);

        grid.ApplyLine(line);

        grid.GetRowText(0).Should().StartWith("Hello");
        grid.DirtyRows.Should().Contain(0);
    }

    [Fact]
    public void ApplyLine_With_Repeat_Fills_Multiple_Cells()
    {
        var grid = new NvimGrid(20, 5);

        var line = new GridLineEvent(1, 1, 0, [
            new GridLineCell(" ", 0, 10),
            new GridLineCell("X", 1, 5),
        ]);

        grid.ApplyLine(line);

        grid.GetRowText(1).Should().Be("          XXXXX     ");
    }

    [Fact]
    public void ApplyLine_With_ColStart_Offset()
    {
        var grid = new NvimGrid(20, 5);

        var line = new GridLineEvent(1, 2, 5, [
            new GridLineCell("W", 0, 1),
            new GridLineCell("o", 0, 1),
            new GridLineCell("r", 0, 1),
            new GridLineCell("l", 0, 1),
            new GridLineCell("d", 0, 1),
        ]);

        grid.ApplyLine(line);

        grid.GetRowText(2).Should().Be("     World          ");
    }

    [Fact]
    public void ApplyLine_Inherits_HlId_When_Null()
    {
        var grid = new NvimGrid(10, 5);

        var line = new GridLineEvent(1, 0, 0, [
            new GridLineCell("A", 5, 1),
            new GridLineCell("B", null, 1), // should inherit hlId=5
        ]);

        grid.ApplyLine(line);

        grid[0, 0].HlId.Should().Be(5);
        grid[0, 1].HlId.Should().Be(5);
    }

    [Fact]
    public void Resize_Preserves_Existing_Content()
    {
        var grid = new NvimGrid(10, 5);

        grid.ApplyLine(new GridLineEvent(1, 0, 0, [
            new GridLineCell("A", 0, 1),
            new GridLineCell("B", 0, 1),
        ]));

        grid.Resize(20, 10);

        grid.Width.Should().Be(20);
        grid.Height.Should().Be(10);
        grid[0, 0].Text.Should().Be("A");
        grid[0, 1].Text.Should().Be("B");
    }

    [Fact]
    public void ApplyScroll_Up_Moves_Rows_Up()
    {
        var grid = new NvimGrid(10, 5);

        // Put identifiable content on each row
        for (var r = 0; r < 5; r++)
        {
            grid.ApplyLine(new GridLineEvent(1, r, 0, [
                new GridLineCell($"{r}", 0, 10)
            ]));
        }
        grid.ClearDirtyRows();

        // Scroll up by 2 in the full region
        grid.ApplyScroll(new GridScrollEvent(1, 0, 5, 0, 10, 2, 0));

        // Row 0 should now have content of old row 2
        grid[0, 0].Text.Should().Be("2");
        grid[1, 0].Text.Should().Be("3");
        grid[2, 0].Text.Should().Be("4");
        // Bottom rows should be cleared
        grid[3, 0].Text.Should().Be(" ");
        grid[4, 0].Text.Should().Be(" ");
    }

    [Fact]
    public void ApplyScroll_Down_Moves_Rows_Down()
    {
        var grid = new NvimGrid(10, 5);

        for (var r = 0; r < 5; r++)
        {
            grid.ApplyLine(new GridLineEvent(1, r, 0, [
                new GridLineCell($"{r}", 0, 10)
            ]));
        }
        grid.ClearDirtyRows();

        // Scroll down by 2 (negative rows)
        grid.ApplyScroll(new GridScrollEvent(1, 0, 5, 0, 10, -2, 0));

        // Top rows should be cleared
        grid[0, 0].Text.Should().Be(" ");
        grid[1, 0].Text.Should().Be(" ");
        // Rows shifted down
        grid[2, 0].Text.Should().Be("0");
        grid[3, 0].Text.Should().Be("1");
        grid[4, 0].Text.Should().Be("2");
    }

    [Fact]
    public void ApplyScroll_Marks_Region_Dirty()
    {
        var grid = new NvimGrid(10, 5);
        grid.ClearDirtyRows();

        grid.ApplyScroll(new GridScrollEvent(1, 1, 4, 0, 10, 1, 0));

        grid.DirtyRows.Should().Contain(1);
        grid.DirtyRows.Should().Contain(2);
        grid.DirtyRows.Should().Contain(3);
    }

    [Fact]
    public void Clear_Resets_All_Cells()
    {
        var grid = new NvimGrid(10, 5);
        grid.ApplyLine(new GridLineEvent(1, 0, 0, [
            new GridLineCell("X", 1, 10)
        ]));

        grid.Clear();

        grid[0, 0].Text.Should().Be(" ");
        grid[0, 0].HlId.Should().Be(0);
    }

    [Fact]
    public void ApplyEvent_GridCursorGoto_Updates_Position()
    {
        var grid = new NvimGrid(80, 24);

        grid.ApplyEvent(new GridCursorGotoEvent(1, 10, 20));

        grid.CursorRow.Should().Be(10);
        grid.CursorCol.Should().Be(20);
    }
}
