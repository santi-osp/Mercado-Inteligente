using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MercadoInteligente.Api;

/// <summary>
/// An intentionally narrow local diagnostic sink. It never formats log state or exception
/// messages: arbitrary framework logs can contain URLs, headers, payloads or credentials.
/// </summary>
public sealed class LocalFileLoggerProvider : ILoggerProvider
{
    private const string Prefix = "market-";
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private readonly string directory;
    private readonly long maxBytes;
    private readonly int maxFiles;
    private readonly TimeSpan retention;
    private readonly object gate = new();

    public LocalFileLoggerProvider(string directory, long maxBytes = 1_048_576, int maxFiles = 28, TimeSpan? retention = null)
    {
        if (maxBytes < 128 || maxFiles < 1 || retention is { Ticks: <= 0 }) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        this.directory = directory;
        this.maxBytes = maxBytes;
        this.maxFiles = maxFiles;
        this.retention = retention ?? TimeSpan.FromDays(14);
        Directory.CreateDirectory(directory);
        lock (gate)
        {
            try { Prune(DateTime.UtcNow); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public ILogger CreateLogger(string categoryName) => new LocalLogger(this, categoryName);
    public void Dispose() { }

    private void Write(int eventId, Exception? exception, IReadOnlyList<KeyValuePair<string, object?>>? state)
    {
        var now = DateTime.UtcNow;
        int? status = null;
        long? duration = null;
        if (eventId == 1 && state is not null)
        {
            foreach (var field in state)
            {
                if (field.Key == "StatusCode" && field.Value is int code && code is >= 100 and <= 599) status = code;
                if (field.Key == "DurationMs" && field.Value is long ms && ms is >= 0 and <= 86_400_000) duration = ms;
            }
        }
        // Only known numeric fields and the runtime type name; no text supplied by callers.
        var line = string.Create(CultureInfo.InvariantCulture,
            $"{now:O} event={eventId} status={status?.ToString(CultureInfo.InvariantCulture) ?? "-"} durationMs={duration?.ToString(CultureInfo.InvariantCulture) ?? "-"} exception={SafeType(exception)}{Environment.NewLine}");
        var bytes = Utf8NoBom.GetByteCount(line);
        if (bytes > maxBytes) return;
        lock (gate)
        {
            try
            {
                Directory.CreateDirectory(directory);
                Prune(now);
                var day = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                var index = 0;
                string path;
                do
                {
                    path = Path.Combine(directory, $"{Prefix}{day}-{index:D4}.log");
                    if (!File.Exists(path) || new FileInfo(path).Length + bytes <= maxBytes) break;
                    index++;
                } while (index < 10000);
                if (index == 10000) return;
                File.AppendAllText(path, line, Utf8NoBom);
                Prune(now);
            }
            catch (IOException) { } // Diagnostics must not break the API.
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string SafeType(Exception? exception)
    {
        if (exception is null) return "-";
        // Type names are controlled by compiled code, not by exception text or stack traces.
        var name = exception.GetType().Name;
        return name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ? name : "Exception";
    }

    private void Prune(DateTime now)
    {
        var files = Directory.GetFiles(directory, "market-????????-????.log")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.Name, StringComparer.Ordinal)
            .ToArray();
        for (var i = 0; i < files.Length; i++)
        {
            var date = files[i].Name.Substring(Prefix.Length, 8);
            if (!DateTime.TryParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ||
                day.Date < now.Date - retention || day.Date > now.Date || i >= maxFiles)
                files[i].Delete();
        }
    }

    private sealed class LocalLogger(LocalFileLoggerProvider owner, string category) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => category == "MercadoInteligente.Api.Operations" && logLevel >= LogLevel.Information && logLevel != LogLevel.None;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || eventId.Id is not (1 or 2)) return;
            owner.Write(eventId.Id, exception, state as IReadOnlyList<KeyValuePair<string, object?>>);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
