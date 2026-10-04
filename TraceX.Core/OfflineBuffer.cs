using System.Text.Json;

namespace TraceX.Core;

public sealed class OfflineBuffer
{
    private const int MaxCapacity = 100;

    private readonly Queue<CrashEventRequest> _queue = new();
    private readonly object _lock = new();
    private readonly string _filePath;

    public OfflineBuffer(string? filePath = null)
    {
        _filePath =
            filePath ?? GetDefaultFilePath();

        LoadFromDisk();
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }
    }

    public void Enqueue(
        CrashEventRequest crash)
    {
        ArgumentNullException.ThrowIfNull(crash);

        lock (_lock)
        {
            if (_queue.Count >= MaxCapacity)
            {
                _queue.Dequeue();
            }

            _queue.Enqueue(crash);

            SaveToDisk();
        }
    }

    public bool TryDequeue(
        out CrashEventRequest? crash)
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
            {
                crash = null;
                return false;
            }

            crash =
                _queue.Dequeue();

            SaveToDisk();

            return true;
        }
    }

    private void LoadFromDisk()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            try
            {
                var json =
                    File.ReadAllText(
                        _filePath);

                var crashes =
                    JsonSerializer.Deserialize<
                        List<CrashEventRequest>>(
                        json);

                if (crashes is null)
                {
                    return;
                }

                foreach (var crash in
                    crashes.TakeLast(
                        MaxCapacity))
                {
                    _queue.Enqueue(
                        crash);
                }
            }
            catch (IOException)
            {
                // Ignore disk read failures.
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore access failures.
            }
            catch (JsonException)
            {
                // Ignore corrupted buffer files.
            }
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var directory =
                Path.GetDirectoryName(
                    _filePath);

            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }

            var json =
                JsonSerializer.Serialize(
                    _queue.ToList());

            File.WriteAllText(
                _filePath,
                json);
        }
        catch (IOException)
        {
            // Ignore disk write failures.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore access failures.
        }
    }

    private static string GetDefaultFilePath()
    {
        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        var directory =
            Path.Combine(
                localApplicationData,
                "TraceX");

        return Path.Combine(
            directory,
            "offline-buffer.json");
    }
}