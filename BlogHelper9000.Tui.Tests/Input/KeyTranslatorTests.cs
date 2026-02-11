using BlogHelper9000.Tui.Input;
using FluentAssertions;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace BlogHelper9000.Tui.Tests.Input;

public class KeyTranslatorTests
{
    [Fact]
    public void Esc_TranslatesToEscNotation()
    {
        var key = new Key(KeyCode.Esc);
        KeyTranslator.ToNvimNotation(key).Should().Be("<Esc>");
    }

    [Fact]
    public void Enter_TranslatesToCR()
    {
        var key = new Key(KeyCode.Enter);
        KeyTranslator.ToNvimNotation(key).Should().Be("<CR>");
    }

    [Fact]
    public void Backspace_TranslatesToBS()
    {
        var key = new Key(KeyCode.Backspace);
        KeyTranslator.ToNvimNotation(key).Should().Be("<BS>");
    }

    [Fact]
    public void Tab_TranslatesToTab()
    {
        var key = new Key(KeyCode.Tab);
        KeyTranslator.ToNvimNotation(key).Should().Be("<Tab>");
    }

    [Fact]
    public void Space_TranslatesToSpace()
    {
        var key = new Key(KeyCode.Space);
        KeyTranslator.ToNvimNotation(key).Should().Be("<Space>");
    }

    [Fact]
    public void ArrowUp_TranslatesToUp()
    {
        var key = new Key(KeyCode.CursorUp);
        KeyTranslator.ToNvimNotation(key).Should().Be("<Up>");
    }

    [Fact]
    public void ArrowDown_TranslatesToDown()
    {
        var key = new Key(KeyCode.CursorDown);
        KeyTranslator.ToNvimNotation(key).Should().Be("<Down>");
    }

    [Fact]
    public void ArrowLeft_TranslatesToLeft()
    {
        var key = new Key(KeyCode.CursorLeft);
        KeyTranslator.ToNvimNotation(key).Should().Be("<Left>");
    }

    [Fact]
    public void ArrowRight_TranslatesToRight()
    {
        var key = new Key(KeyCode.CursorRight);
        KeyTranslator.ToNvimNotation(key).Should().Be("<Right>");
    }

    [Fact]
    public void LowercaseA_TranslatesToA()
    {
        var key = new Key(KeyCode.A);
        KeyTranslator.ToNvimNotation(key).Should().Be("a");
    }

    [Fact]
    public void ShiftA_TranslatesToUppercaseA()
    {
        var key = new Key(KeyCode.A).WithShift;
        KeyTranslator.ToNvimNotation(key).Should().Be("A");
    }

    [Fact]
    public void CtrlA_TranslatesToCtrlNotation()
    {
        var key = new Key(KeyCode.A).WithCtrl;
        KeyTranslator.ToNvimNotation(key).Should().Be("<C-a>");
    }

    [Fact]
    public void AltX_TranslatesToMetaNotation()
    {
        var key = new Key(KeyCode.X).WithAlt;
        KeyTranslator.ToNvimNotation(key).Should().Be("<M-x>");
    }

    [Fact]
    public void AltShiftX_TranslatesToMetaUpperNotation()
    {
        var key = new Key(KeyCode.X).WithAlt.WithShift;
        KeyTranslator.ToNvimNotation(key).Should().Be("<M-X>");
    }

    [Fact]
    public void Digit5_TranslatesToDigitString()
    {
        var key = new Key(KeyCode.D5);
        KeyTranslator.ToNvimNotation(key).Should().Be("5");
    }

    [Fact]
    public void F1_TranslatesToF1()
    {
        var key = new Key(KeyCode.F1);
        KeyTranslator.ToNvimNotation(key).Should().Be("<F1>");
    }

    [Fact]
    public void F12_TranslatesToF12()
    {
        var key = new Key(KeyCode.F12);
        KeyTranslator.ToNvimNotation(key).Should().Be("<F12>");
    }

    [Fact]
    public void ShiftEnter_TranslatesToShiftCR()
    {
        var key = new Key(KeyCode.Enter).WithShift;
        KeyTranslator.ToNvimNotation(key).Should().Be("<S-CR>");
    }

    [Fact]
    public void CtrlBackspace_TranslatesToCtrlBS()
    {
        var key = new Key(KeyCode.Backspace).WithCtrl;
        KeyTranslator.ToNvimNotation(key).Should().Be("<C-BS>");
    }
}
