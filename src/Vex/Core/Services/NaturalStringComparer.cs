using System.Collections.Generic;
using System.Text;

namespace Vex.Core.Services;

/// <summary>资源管理器式自然名称比较：数字段按数值、其余按不区分大小写序。</summary>
public sealed class NaturalStringComparer : IComparer<string>
{
    public static readonly NaturalStringComparer Descending = new(descending: true);

    private readonly bool _descending;

    public NaturalStringComparer(bool descending = false) => _descending = descending;

    public int Compare(string? x, string? y)
    {
        var result = CompareCore(x ?? string.Empty, y ?? string.Empty);
        return _descending ? -result : result;
    }

    private static int CompareCore(string left, string right)
    {
        var li = 0; var ri = 0;
        while (li < left.Length && ri < right.Length)
        {
            var leftDigit = char.IsDigit(left[li]);
            var rightDigit = char.IsDigit(right[ri]);
            if (leftDigit && rightDigit)
            {
                var liEnd = li; while (liEnd < left.Length && char.IsDigit(left[liEnd])) liEnd++;
                var riEnd = ri; while (riEnd < right.Length && char.IsDigit(right[riEnd])) riEnd++;
                var leftText = left[li..liEnd].TrimStart('0');
                var rightText = right[ri..riEnd].TrimStart('0');
                var byLength = leftText.Length.CompareTo(rightText.Length);
                if (byLength != 0) return byLength;
                var byText = string.CompareOrdinal(leftText, rightText);
                if (byText != 0) return byText;
                li = liEnd; ri = riEnd;
                continue;
            }
            var byChar = string.Compare(
                left[li].ToString(), right[ri].ToString(), StringComparison.OrdinalIgnoreCase);
            if (byChar != 0) return byChar;
            li++; ri++;
        }
        var byRest = (left.Length - li).CompareTo(right.Length - ri);
        return byRest != 0 ? byRest : string.CompareOrdinal(left, right);
    }
}
