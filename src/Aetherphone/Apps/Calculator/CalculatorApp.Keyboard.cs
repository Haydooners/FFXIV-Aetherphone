using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Calculator;

internal sealed partial class CalculatorApp
{
    private const char FirstPrintable = ' ';
    private static readonly CalcKey[] DigitKeys =
    {
        CalcKey.Zero, CalcKey.One, CalcKey.Two, CalcKey.Three, CalcKey.Four, CalcKey.Five, CalcKey.Six,
        CalcKey.Seven, CalcKey.Eight, CalcKey.Nine,
    };

    private void HandleKeyboard(Rect screen)
    {
        if (!UiInteract.Hover(screen.Min, screen.Max) || !GameInput.Claim())
        {
            return;
        }

        var io = ImGui.GetIO();
        if (io.KeyCtrl)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.C, false))
            {
                CopyResult();
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.V, false))
            {
                PasteFromClipboard();
            }

            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape, false))
        {
            ClearFromKeyboard();
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Backspace) || ImGui.IsKeyPressed(ImGuiKey.Delete))
        {
            if (engine.CanBackspace)
            {
                Press(CalcKey.Clear, true);
            }

            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Enter, false) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter, false))
        {
            Press(CalcKey.Equals, true);
            return;
        }

        var queue = io.InputQueueCharacters;
        for (var index = 0; index < queue.Size; index++)
        {
            if (TryKeyFor((char)queue[index], out var key))
            {
                Press(key, true);
            }
        }
    }

    private void ClearFromKeyboard()
    {
        if (menuOpen)
        {
            menuOpen = false;
            return;
        }

        keyPress[(int)CalcKey.Clear].SnapTo(1f);
        engine.AllClear();
        Play(UiSound.KeystrokeDelete);
    }

    private static bool TryKeyFor(char character, out CalcKey key)
    {
        key = CalcKey.Equals;
        if (character < FirstPrintable)
        {
            return false;
        }

        if (char.IsAsciiDigit(character))
        {
            key = DigitKeys[character - '0'];
            return true;
        }

        switch (character)
        {
            case '+':
                key = CalcKey.Add;
                return true;
            case '-':
                key = CalcKey.Subtract;
                return true;
            case '*':
            case 'x':
            case 'X':
            case '×':
                key = CalcKey.Multiply;
                return true;
            case '/':
            case '÷':
                key = CalcKey.Divide;
                return true;
            case '=':
                key = CalcKey.Equals;
                return true;
            case '%':
                key = CalcKey.Percent;
                return true;
            case '.':
            case ',':
                key = CalcKey.Decimal;
                return true;
            default:
                return false;
        }
    }
}
