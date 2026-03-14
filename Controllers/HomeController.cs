using System.Diagnostics;
using System.Text;
using CustomWebTools.Models;
using Microsoft.AspNetCore.Mvc;

namespace CustomWebTools.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Compare([FromForm] string text1, [FromForm] string text2)
        {
            text1 ??= "";
            text2 ??= "";

            // Normalize line endings for consistent comparison.
            text1 = text1.Replace("\r\n", "\n").Replace("\r", "\n");
            text2 = text2.Replace("\r\n", "\n").Replace("\r", "\n");

            var l1lines = text1.Split('\n');
            var l2lines = text2.Split('\n');

            static void AppendHtmlEncoded(StringBuilder sb, ReadOnlySpan<char> value)
            {
                // Fast minimal HTML encoding without allocations.
                for (var i = 0; i < value.Length; i++)
                {
                    switch (value[i])
                    {
                        case '&': sb.Append("&amp;"); break;
                        case '<': sb.Append("&lt;"); break;
                        case '>': sb.Append("&gt;"); break;
                        case '"': sb.Append("&quot;"); break;
                        case '\'': sb.Append("&#39;"); break;
                        default: sb.Append(value[i]); break;
                    }
                }
            }

            static void AppendPositionalCompare(StringBuilder sb, string a, string b)
            {
                var len1 = a.Length;
                if (len1 == 0)
                {
                    sb.Append("<br/>");
                    return;
                }

                var runStart = 0;
                var runClass = "";

                for (var j = 0; j < len1; j++)
                {
                    var c1 = a[j];
                    var c2 = j < b.Length ? b[j] : '\0';
                    var cls = (c1 == c2) ? "greenback" : "redback";

                    if (j == 0)
                    {
                        runClass = cls;
                        runStart = 0;
                        continue;
                    }

                    if (!string.Equals(runClass, cls, StringComparison.Ordinal))
                    {
                        sb.Append("<span class='").Append(runClass).Append("'>");
                        AppendHtmlEncoded(sb, a.AsSpan(runStart, j - runStart));
                        sb.Append("</span>");

                        runClass = cls;
                        runStart = j;
                    }
                }

                sb.Append("<span class='").Append(runClass).Append("'>");
                AppendHtmlEncoded(sb, a.AsSpan(runStart, len1 - runStart));
                sb.Append("</span>");
                sb.Append("<br/>");
            }

            static void AppendAlignedCompare(StringBuilder sb, string a, string b)
            {
                // Align using Myers diff and render edits:
                // - equal chars: greenback
                // - inserted/deleted/replaced chars: redback
                // This makes insertions (e.g. extra space in text2) visible instead of looking "all green".
                var n = a.Length;
                var m = b.Length;
                if (n == 0 && m == 0)
                {
                    sb.Append("<br/>");
                    return;
                }

                var max = n + m;
                var offset = max;
                var v = new int[2 * max + 1];
                Array.Fill(v, -1);
                v[offset + 1] = 0;

                // Store V for backtracking (after each D iteration).
                var trace = new List<int[]>(capacity: Math.Min(max + 1, 512));
                var lastD = 0;

                for (var d = 0; d <= max; d++)
                {
                    for (var k = -d; k <= d; k += 2)
                    {
                        var idx = k + offset;
                        int x;
                        if (k == -d || (k != d && v[idx - 1] < v[idx + 1]))
                        {
                            x = v[idx + 1]; // down: consume from b (insertion relative to a)
                        }
                        else
                        {
                            x = v[idx - 1] + 1; // right: consume from a (deletion relative to a)
                        }

                        var y = x - k;

                        while (x < n && y < m && y >= 0 && a[x] == b[y])
                        {
                            x++;
                            y++;
                        }

                        v[idx] = x;
                        if (x >= n && y >= m)
                        {
                            lastD = d;
                            trace.Add((int[])v.Clone());
                            goto DiffDone;
                        }
                    }

                    trace.Add((int[])v.Clone());
                    lastD = d;
                }

            DiffDone:
                static void AppendHtmlEncodedChar(StringBuilder sb, char ch)
                {
                    switch (ch)
                    {
                        case '&': sb.Append("&amp;"); break;
                        case '<': sb.Append("&lt;"); break;
                        case '>': sb.Append("&gt;"); break;
                        case '"': sb.Append("&quot;"); break;
                        case '\'': sb.Append("&#39;"); break;
                        default: sb.Append(ch); break;
                    }
                }

                // Backtrack to build an operation list (equal/insert/delete).
                var ops = new List<(char ch, bool isEqual)>(n + m);
                var x2 = n;
                var y2 = m;

                for (var d = lastD; d > 0; d--)
                {
                    var vPrev = trace[d - 1];
                    var k = x2 - y2;
                    var idx = k + offset;

                    int prevK;
                    if (k == -d || (k != d && vPrev[idx - 1] < vPrev[idx + 1]))
                    {
                        prevK = k + 1;
                    }
                    else
                    {
                        prevK = k - 1;
                    }

                    var prevX = vPrev[prevK + offset];
                    var prevY = prevX - prevK;

                    while (x2 > prevX && y2 > prevY)
                    {
                        // Diagonal move: matched character
                        ops.Add((a[x2 - 1], true));
                        x2--;
                        y2--;
                    }

                    // One edit step (insert/delete). We only advance indices; no match marking here.
                    if (x2 == prevX)
                    {
                        // Insertion relative to 'a' (i.e., character exists in b only)
                        if (y2 > 0) ops.Add((b[y2 - 1], false));
                        y2--;
                    }
                    else
                    {
                        // Deletion from 'a' (i.e., character exists in a only)
                        if (x2 > 0) ops.Add((a[x2 - 1], false));
                        x2--;
                    }
                }

                // Flush any remaining leading matches
                while (x2 > 0 && y2 > 0 && a[x2 - 1] == b[y2 - 1])
                {
                    ops.Add((a[x2 - 1], true));
                    x2--;
                    y2--;
                }
                // Remaining leading edits
                while (x2 > 0)
                {
                    ops.Add((a[x2 - 1], false));
                    x2--;
                }
                while (y2 > 0)
                {
                    ops.Add((b[y2 - 1], false));
                    y2--;
                }

                ops.Reverse();

                if (ops.Count == 0)
                {
                    sb.Append("<br/>");
                    return;
                }

                // Output grouped spans for fewer DOM nodes.
                var runStart = 0;
                var runClass = ops[0].isEqual ? "greenback" : "redback";
                for (var i = 1; i < ops.Count; i++)
                {
                    var cls = ops[i].isEqual ? "greenback" : "redback";
                    if (!string.Equals(runClass, cls, StringComparison.Ordinal))
                    {
                        sb.Append("<span class='").Append(runClass).Append("'>");
                        for (var j = runStart; j < i; j++)
                        {
                            AppendHtmlEncodedChar(sb, ops[j].ch);
                        }
                        sb.Append("</span>");
                        runClass = cls;
                        runStart = i;
                    }
                }

                sb.Append("<span class='").Append(runClass).Append("'>");
                for (var j = runStart; j < ops.Count; j++)
                {
                    AppendHtmlEncodedChar(sb, ops[j].ch);
                }
                sb.Append("</span>");
                sb.Append("<br/>");
            }

            var sb = new StringBuilder(capacity: Math.Min(1024 * 1024, Math.Max(text1.Length, 1024)));
            for (var lineIndex = 0; lineIndex < l1lines.Length; lineIndex++)
            {
                var l1line = l1lines[lineIndex];
                var l2line = lineIndex < l2lines.Length ? l2lines[lineIndex] : "";

                // Use aligned diff for typical lines; fall back for very long lines to avoid heavy computation.
                const int maxAlignedLength = 4000;
                if (l1line.Length <= maxAlignedLength && l2line.Length <= maxAlignedLength)
                {
                    AppendAlignedCompare(sb, l1line, l2line);
                }
                else
                {
                    AppendPositionalCompare(sb, l1line, l2line);
                }
            }

            return Content(sb.ToString(), "text/html; charset=utf-8");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
