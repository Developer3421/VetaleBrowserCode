using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Сервис базы данных для загрузок с AES шифрованием чувствительных полей.
/// </summary>
public class DownloadDatabaseService : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly DatabaseEncryptionService _encryption;
    private readonly ILiteCollection<DownloadItem> _downloads;
    private readonly string _databasePath;

    // Лимиты
    private const int MaxItems = 10000;
    private const long MaxDatabaseSizeBytes = 300 * 1024 * 1024; // 300MB

    public DownloadDatabaseService(string databasePath, string encryptionKey)
    {
        _encryption = new DatabaseEncryptionService(encryptionKey);
        _databasePath = databasePath;

        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var cs = new ConnectionString { Filename = databasePath, Connection = ConnectionType.Shared };
        _database = new LiteDatabase(cs);

        _downloads = _database.GetCollection<DownloadItem>("downloads");
        _downloads.EnsureIndex(x => x.Status);
        _downloads.EnsureIndex(x => x.StartTime);
        _downloads.EnsureIndex(x => x.EndTime);

        CheckSize();
    }

    private void CheckSize()
    {
        var fi = new FileInfo(_databasePath);
        if (fi.Exists && fi.Length > MaxDatabaseSizeBytes)
        {
            CleanupOld();
            _database.Rebuild();
        }
    }

    private void CleanupOld()
    {
        var total = _downloads.Count();
        if (total > MaxItems)
        {
            var toDelete = total - MaxItems;
            var old = _downloads.Query().Where(x => x.IsArchived || x.Status == "Completed" || x.Status == "Error" || x.Status == "Cancelled")
                .OrderBy(x => x.EndTime ?? x.StartTime)
                .Limit(toDelete)
                .ToList();
            foreach (var item in old)
            {
                _downloads.Delete(item.Id);
            }
        }
    }

    public int AddOrUpdate(DownloadItem item)
    {
        try
        {
            // Шифруем строки
            item.Url = _encryption.EncryptString(item.Url);
            item.FileName = _encryption.EncryptString(item.FileName);
            item.TargetPath = _encryption.EncryptString(item.TargetPath);
            if (!string.IsNullOrEmpty(item.ErrorMessage))
                item.ErrorMessage = _encryption.EncryptString(item.ErrorMessage);
            if (!string.IsNullOrEmpty(item.ContentType))
                item.ContentType = _encryption.EncryptString(item.ContentType);

            if (item.Id == 0)
            {
                _downloads.Insert(item);
            }
            else
            {
                _downloads.Update(item);
            }
            CheckSize();
            return item.Id;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DownloadDatabaseService] AddOrUpdate error: {ex.Message}");
            return 0;
        }
    }

    private DownloadItem? Decrypt(DownloadItem? item)
    {
        if (item == null) return null;
        try
        {
            item.Url = _encryption.DecryptString(item.Url);
            item.FileName = _encryption.DecryptString(item.FileName);
            item.TargetPath = _encryption.DecryptString(item.TargetPath);
            if (!string.IsNullOrEmpty(item.ErrorMessage))
                item.ErrorMessage = _encryption.DecryptString(item.ErrorMessage);
            if (!string.IsNullOrEmpty(item.ContentType))
                item.ContentType = _encryption.DecryptString(item.ContentType);
        }
        catch { }
        return item;
    }

    private List<DownloadItem> DecryptList(IEnumerable<DownloadItem> items)
    {
        return items.Select(x => Decrypt(x)!).ToList();
    }

    public DownloadItem? GetById(int id)
    {
        return Decrypt(_downloads.FindById(id));
    }

    public List<DownloadItem> GetActive()
    {
        var list = _downloads.Query().Where(x => x.Status == "Pending" || x.Status == "Downloading").OrderByDescending(x => x.StartTime).ToList();
        return DecryptList(list);
    }

    public List<DownloadItem> GetRecent(int limit = 100)
    {
        var list = _downloads.Query().OrderByDescending(x => x.StartTime).Limit(limit).ToList();
        return DecryptList(list);
    }

    public void UpdateProgress(int id, long bytesReceived, long totalBytes, double speed, double avgSpeed, double eta)
    {
        try
        {
            var item = _downloads.FindById(id);
            if (item == null) return;
            item.BytesReceived = bytesReceived;
            if (totalBytes >= 0) item.TotalBytes = totalBytes;
            item.LastMeasuredSpeedBytesPerSec = speed;
            item.AverageSpeedBytesPerSec = avgSpeed;
            item.EstimatedRemainingSeconds = eta;
            _downloads.Update(item);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DownloadDatabaseService] UpdateProgress error: {ex.Message}");
        }
    }

    public void SetStatus(int id, string status, string? errorMessage = null)
    {
        try
        {
            var item = _downloads.FindById(id);
            if (item == null) return;
            item.Status = status;
            if (status == "Completed" || status == "Error" || status == "Cancelled")
                item.EndTime = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(errorMessage))
                item.ErrorMessage = _encryption.EncryptString(errorMessage);
            _downloads.Update(item);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DownloadDatabaseService] SetStatus error: {ex.Message}");
        }
    }

    public void ArchiveOld(DateTime olderThan)
    {
        try
        {
            var items = _downloads.Query().Where(x => x.EndTime != null && x.EndTime < olderThan).ToList();
            foreach (var i in items)
            {
                i.IsArchived = true;
                _downloads.Update(i);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DownloadDatabaseService] ArchiveOld error: {ex.Message}");
        }
    }

    public List<DownloadItem> Search(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return GetRecent();
        term = term.ToLowerInvariant();
        var all = GetRecent(500);
        return all.Where(x => x.FileName.ToLowerInvariant().Contains(term) || x.Url.ToLowerInvariant().Contains(term)).ToList();
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}

