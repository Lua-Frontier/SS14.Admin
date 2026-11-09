using System.Net;
using System.Text;

namespace SS14.Admin.AdminLogs;

public static class AdminLogMessageFormatter
{
    private static readonly string[] ActorPalette = { "#8ED9FF", "#E9C46A", "#8CCFAE", "#C7ACF2", "#F29C8F", "#78C7D4" };

    public static string Format(string? message, IEnumerable<string> playerNames)
    {
        if (string.IsNullOrEmpty(message)) return string.Empty;

        var actors = playerNames.Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => (Name: name, Color: ActorPalette[StableColorIndex(name)]))
            .OrderByDescending(actor => actor.Name.Length)
            .Take(64)
            .ToArray();
        var html = new StringBuilder(message.Length + 64);
        var quoteOpen = false;
        for (var index = 0; index < message.Length;)
        {
            var actor = actors.FirstOrDefault(candidate => IsActorAt(message, index, candidate.Name));
            if (actor.Name != null)
            {
                html.Append("<span class=\"log-actor\" style=\"--actor-color:")
                    .Append(actor.Color).Append("\">").Append(WebUtility.HtmlEncode(actor.Name)).Append("</span>");
                index += actor.Name.Length;
                continue;
            }

            var current = message[index];
            if (current is '"' or '“' or '”' or '«' or '»')
            {
                var opensQuote = current is '"' or '“' or '«';
                var closesQuote = current is '"' or '”' or '»';
                if (opensQuote && !quoteOpen)
                {
                    html.Append("<span class=\"log-quote\">").Append(WebUtility.HtmlEncode(current.ToString()));
                    quoteOpen = true;
                }
                else if (closesQuote && quoteOpen)
                {
                    html.Append(WebUtility.HtmlEncode(current.ToString())).Append("</span>");
                    quoteOpen = false;
                }
                else html.Append(WebUtility.HtmlEncode(current.ToString()));
                index++;
                continue;
            }

            html.Append(WebUtility.HtmlEncode(current.ToString()));
            index++;
        }

        if (quoteOpen) html.Append("</span>");
        return html.ToString();
    }

    private static bool IsActorAt(string message, int index, string name)
    {
        if (index + name.Length > message.Length || !message.AsSpan(index, name.Length).Equals(name, StringComparison.OrdinalIgnoreCase))
            return false;
        return (index == 0 || !IsWord(message[index - 1]) || !IsWord(name[0]))
            && (index + name.Length == message.Length || !IsWord(message[index + name.Length]) || !IsWord(name[^1]));
    }

    private static bool IsWord(char character) => char.IsLetterOrDigit(character) || character == '_';

    private static int StableColorIndex(string name)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in name.ToUpperInvariant())
                hash = (hash ^ character) * 16777619;
            return (int)(hash % ActorPalette.Length);
        }
    }
}
