namespace Vex.Core.Services;

public static class MarkdownTextComparer
{
    public static bool EqualsNormalizedLineEndings(string? left, string? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        left ??= string.Empty;
        right ??= string.Empty;

        var leftIndex = 0;
        var rightIndex = 0;
        while (true)
        {
            var hasLeft = TryReadNormalizedCharacter(left, ref leftIndex, out var leftCharacter);
            var hasRight = TryReadNormalizedCharacter(right, ref rightIndex, out var rightCharacter);
            if (!hasLeft || !hasRight)
            {
                return hasLeft == hasRight;
            }

            if (leftCharacter != rightCharacter)
            {
                return false;
            }
        }
    }

    private static bool TryReadNormalizedCharacter(string text, ref int index, out char character)
    {
        if (index >= text.Length)
        {
            character = '\0';
            return false;
        }

        character = text[index++];
        if (character == '\r')
        {
            if (index < text.Length && text[index] == '\n')
            {
                index++;
            }

            character = '\n';
        }
        else if (IsSingleCharacterLineEnding(character))
        {
            character = '\n';
        }

        return true;
    }

    private static bool IsSingleCharacterLineEnding(char character)
    {
        return character is '\n' or '\f' or '\u0085' or '\u2028' or '\u2029';
    }
}
