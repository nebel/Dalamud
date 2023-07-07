using System;
using System.Collections.Generic;

namespace Dalamud.Utility;

public readonly ref struct FuzzyMatcher
{
    private static readonly (int, int)[] EmptySegArray = Array.Empty<(int, int)>();

    private readonly string needleString = string.Empty;
    private readonly ReadOnlySpan<char> needleSpan = ReadOnlySpan<char>.Empty;
    private readonly int needleFinalPosition = -1;
    private readonly (int start, int end)[] needleSegments = EmptySegArray;

    public FuzzyMatcher(string term)
    {
        needleString = term;
        needleSpan = needleString.AsSpan();
        needleFinalPosition = needleSpan.Length - 1;
        needleSegments = FindNeedleSegments(needleSpan);
    }

    private static (int start, int end)[] FindNeedleSegments(ReadOnlySpan<char> span)
    {
        var segments = new List<(int, int)>();
        var wordStart = -1;

        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] is not ' ' and not '\u3000')
            {
                if (wordStart < 0)
                {
                    wordStart = i;
                }
            }
            else if (wordStart >= 0)
            {
                segments.Add((wordStart, i - 1));
                wordStart = -1;
            }
        }

        if (wordStart >= 0)
        {
            segments.Add((wordStart, span.Length - 1));
        }

        return segments.ToArray();
    }

    public int Matches(string value)
    {
        if (value.Contains(needleString)) {
            return 100;
        }

        var haystack = value.AsSpan();

        if (needleSegments.Length < 2)
        {
            return GetRawScore(haystack, 0, needleFinalPosition);
        }

        var total = 0;
        for (var i = 0; i < needleSegments.Length; i++)
        {
            var (start, end) = needleSegments[i];
            var cur = GetRawScore(haystack, start, end);
            if (cur == 0)
            {
                return 0;
            }

            total += cur;
        }

        return total;
    }

    public int MatchesAny(params string[] values)
    {
        var max = 0;
        for (var i = 0; i < values.Length; i++)
        {
            var cur = Matches(values[i]);
            if (cur > max)
            {
                max = cur;
            }
        }

        return max;
    }

    private int GetRawScore(ReadOnlySpan<char> haystack, int needleStart, int needleEnd)
    {
        var (matchPos, gaps, consecutive, borderMatches, endPos) = FindForward(haystack, needleStart, needleEnd);
        if (matchPos < 0)
        {
            return 0;
        }

        var needleSize = needleEnd - needleStart + 1;

        var score = CalculateRawScore(needleSize, matchPos, gaps, consecutive, borderMatches);
        // Console.WriteLine(
        //     $"['{needleString.Substring(needleStart, needleEnd - needleStart + 1)}' in '{haystack}'] fwd: needleSize={needleSize} startPos={matchPos} gaps={gaps} consecutive={consecutive} borderMatches={borderMatches} score={score}");

        (matchPos, gaps, consecutive, borderMatches) = FindReverse(haystack, endPos, needleStart, needleEnd);
        var revScore = CalculateRawScore(needleSize, matchPos, gaps, consecutive, borderMatches);
        // Console.WriteLine(
        //     $"['{needleString.Substring(needleStart, needleEnd - needleStart + 1)}' in '{haystack}'] rev: needleSize={needleSize} startPos={matchPos} gaps={gaps} consecutive={consecutive} borderMatches={borderMatches} score={revScore}");

        return int.Max(score, revScore);
    }

    private (int matchPos, int gaps, int consecutive, int borderMatches, int haystackIndex) FindForward(
        ReadOnlySpan<char> haystack, int needleStart, int needleEnd)
    {
        var needleIndex = needleStart;
        var lastMatchIndex = -10;

        var startPos = 0;
        var gaps = 0;
        var consecutive = 0;
        var borderMatches = 0;

        for (var haystackIndex = 0; haystackIndex < haystack.Length; haystackIndex++)
        {
            if (haystack[haystackIndex] == needleSpan[needleIndex])
            {
                needleIndex++;

                if (haystackIndex == lastMatchIndex + 1)
                {
                    consecutive++;
                }

                if (needleIndex > needleEnd)
                {
                    return (startPos, gaps, consecutive, borderMatches, haystackIndex);
                }

                lastMatchIndex = haystackIndex;
            }
            else
            {
                if (needleIndex > needleStart)
                {
                    gaps++;
                }
                else
                {
                    startPos++;
                }
            }
        }

        return (-1, 0, 0, 0, 0);
    }

    private (int matchPos, int gaps, int consecutive, int borderMatches) FindReverse(ReadOnlySpan<char> haystack,
        int haystackLastMatchIndex, int needleStart, int needleEnd)
    {
        var needleIndex = needleEnd;
        var revLastMatchIndex = haystack.Length + 10;

        var gaps = 0;
        var consecutive = 0;
        var borderMatches = 0;

        for (var haystackIndex = haystackLastMatchIndex; haystackIndex >= 0; haystackIndex--)
        {
            if (haystack[haystackIndex] == needleSpan[needleIndex])
            {
                needleIndex--;

                if (haystackIndex == revLastMatchIndex - 1)
                {
                    consecutive++;
                }

                if (needleIndex < needleStart)
                {
                    return (haystackIndex, gaps, consecutive, borderMatches);
                }

                revLastMatchIndex = haystackIndex;
            }
            else
            {
                gaps++;
            }
        }

        return (-1, 0, 0, 0);
    }

    public static int CalculateRawScore(int needleSize, int matchPos, int gaps, int consecutive, int borderMatches)
    {
        var score = 100
                    + (needleSize * 3)
                    + (borderMatches * 3)
                    + (consecutive * 5)
                    - (gaps * 10);
        if (matchPos == 0)
            score += 5;
        return score < 1 ? 1 : score;
    }
}
