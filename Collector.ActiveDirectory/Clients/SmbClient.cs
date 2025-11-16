using System.Diagnostics.CodeAnalysis;
using System.Text;
using Collector.ActiveDirectory.Exceptions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using SMBLibrary;
using SMBLibrary.Client;
using FileAttributes = SMBLibrary.FileAttributes;

namespace Collector.ActiveDirectory.Clients;

internal sealed class SmbClient : IDisposable
{
    private static readonly RetryPolicy<bool> ResiliencyPolicy = Policy.HandleResult<bool>(success => !success).WaitAndRetry(5, sleepDurationProvider: _ => TimeSpan.FromSeconds(5));

    private const string Sysvol = "SYSVOL";
    private readonly ILogger _logger;
    private readonly SMB2Client _client;
    private readonly ISMBFileStore _store;
    private readonly string _domain;

    public SmbClient(ILogger logger, string serverName, string domain, string username, string password)
    {
        _logger = logger;
        if (TryCreateStore(logger, serverName, domain, username, password, out var client, out var store))
        {
            _client = client;
            _store = store;
            _domain = domain;
        }
        else
        {
            throw new SmbException($"Could not establish SMB connection to {serverName}");
        }
    }

    private string SanitizePath(string path)
    {
        return path.Replace($@"\\{_domain}\sysvol\", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCreateStore(ILogger logger, string serverName, string domain, string username, string password, [MaybeNullWhen(false)] out SMB2Client client, [MaybeNullWhen(false)] out ISMBFileStore fileStore)
    {
        fileStore = null;
        client = new SMB2Client();
        if (client.Connect(serverName, SMBTransportType.DirectTCPTransport))
        {
            var status = client.Login(domain, username, password);
            if (status == NTStatus.STATUS_SUCCESS)
            {
                var shares = client.ListShares(out status);
                if (status == NTStatus.STATUS_SUCCESS)
                {
                    if (shares.Contains(Sysvol))
                    {
                        fileStore = client.TreeConnect(Sysvol, out status);
                        return status == NTStatus.STATUS_SUCCESS;
                    }
                    else
                    {
                        logger.LogError("Could not find SYSVOL share: '{NTStatus}'", status.ToString());
                    }
                }
                else
                {
                    logger.LogError("Could not list SMB shares: '{NTStatus}'", status.ToString());
                }
            }
            else
            {
                logger.LogError("Could not login to SMB server: '{NTStatus}'", status.ToString());
            }
        }
        else
        {
            logger.LogError("Could not connect to SMB server");
        }

        return false;
    }

    public void DeleteFile(string path)
    {
        var result = ResiliencyPolicy.Execute(() =>
            {
                var sanitizedPath = SanitizePath(path);
                var status = _store.CreateFile(out var fileHandle, out _, sanitizedPath, AccessMask.GENERIC_WRITE | AccessMask.DELETE | AccessMask.SYNCHRONIZE, FileAttributes.Normal, ShareAccess.None, CreateDisposition.FILE_OPEN, CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
                if (status == NTStatus.STATUS_SUCCESS)
                {
                    var fileDispositionInformation = new FileDispositionInformation
                    {
                        DeletePending = true
                    };

                    status = _store.SetFileInformation(fileHandle, fileDispositionInformation);
                    return status == NTStatus.STATUS_SUCCESS && _store.CloseFile(fileHandle) == NTStatus.STATUS_SUCCESS;
                }

                _logger.LogWarning("Could not delete file '{Path}': {Status}", path, status.ToString());
                return false;
            }
        );

        if (!result)
        {
            throw new SmbException($"Could not delete file {path}");
        }
    }

    public void DeleteFolder(string path)
    {
        var result = ResiliencyPolicy.Execute(() =>
        {
            var sanitizedPath = SanitizePath(path);
            var status = _store.CreateFile(out var directoryHandle, out _, sanitizedPath, AccessMask.GENERIC_WRITE | AccessMask.GENERIC_READ | AccessMask.DELETE | AccessMask.SYNCHRONIZE, FileAttributes.Directory, ShareAccess.Read | ShareAccess.Write, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
            if (status == NTStatus.STATUS_SUCCESS)
            {
                _store.QueryDirectory(out var files, directoryHandle, fileName: "*", FileInformationClass.FileDirectoryInformation);
                foreach (var file in files.OfType<FileDirectoryInformation>())
                {
                    if (!Path.HasExtension(file.FileName)) continue;
                    DeleteFile(Path.Combine(sanitizedPath, file.FileName));
                }

                var fileDispositionInformation = new FileDispositionInformation
                {
                    DeletePending = true
                };

                status = _store.SetFileInformation(directoryHandle, fileDispositionInformation);
                return status == NTStatus.STATUS_SUCCESS && _store.CloseFile(directoryHandle) == NTStatus.STATUS_SUCCESS;
            }

            _logger.LogWarning("Could not delete folder '{Path}': {Status}", path, status.ToString());
            return false;
        });

        if (!result)
        {
            throw new SmbException($"Could not delete folder {path}");
        }
    }

    public void WriteFile(Stream stream, string path)
    {
        var result = ResiliencyPolicy.Execute(() =>
        {
            var sanitizedPath = SanitizePath(path);
            var success = false;
            var parts = sanitizedPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            var i = 0;
            var subPath = string.Empty;
            do
            {
                subPath = Path.Combine(subPath, parts[i]);
                if (!Path.HasExtension(subPath))
                {
                    if (!subPath.EndsWith("\\Policies") && !CreateDirectory(subPath))
                    {
                        _logger.LogWarning("Could not create directory '{Directory}'", subPath);
                    }
                }

                i++;
            } while (i < parts.Length);

            var status = _store.CreateFile(out var fileHandle, out _, sanitizedPath, AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE, FileAttributes.Normal, ShareAccess.None, CreateDisposition.FILE_OVERWRITE_IF, CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
            if (status == NTStatus.STATUS_SUCCESS)
            {
                var error = false;
                long writeOffset = 0;
                while (stream.Position < stream.Length)
                {
                    var writeSize = (int)_client.MaxWriteSize;
                    var buffer = new byte[writeSize];
                    var bytesRead = stream.Read(buffer, offset: 0, count: buffer.Length);
                    if (bytesRead < writeSize)
                    {
                        Array.Resize(ref buffer, bytesRead);
                    }

                    status = _store.WriteFile(out _, fileHandle, writeOffset, buffer);
                    if (status != NTStatus.STATUS_SUCCESS)
                    {
                        _logger.LogWarning("Could not write to file '{Path}': {Status}", path, status.ToString());
                        error = true;
                        break;
                    }

                    writeOffset += bytesRead;
                }

                status = _store.CloseFile(fileHandle);
                if (status == NTStatus.STATUS_SUCCESS)
                {
                    if (!error)
                    {
                        success = true;
                    }
                }
                else
                {
                    _logger.LogWarning("Could not close file '{Path}': {Status}", path, status.ToString());
                }
            }
            else
            {
                _logger.LogWarning("Could not create file '{Path}': {Status}", path, status.ToString());
            }

            return success;
        });

        if (!result)
        {
            throw new SmbException($"Could not write to file {path}");
        }
    }

    public void WriteFile(string content, string path)
    {
        WriteFile(Encoding.UTF8.GetBytes(content), path);
    }

    public void WriteFile(byte[] content, string path)
    {
        var result = ResiliencyPolicy.Execute(() =>
        {
            var sanitizedPath = SanitizePath(path);
            var success = false;
            var parts = sanitizedPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            var i = 0;
            var subPath = string.Empty;
            do
            {
                subPath = Path.Combine(subPath, parts[i]);
                if (!Path.HasExtension(subPath))
                {
                    if (!subPath.EndsWith("\\Policies") && !CreateDirectory(subPath))
                    {
                        _logger.LogWarning("Could not create directory '{Directory}'", subPath);
                    }
                }

                i++;
            } while (i < parts.Length);

            var status = _store.CreateFile(out var fileHandle, out _, sanitizedPath, AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE, FileAttributes.Normal, ShareAccess.None, CreateDisposition.FILE_OVERWRITE_IF, CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
            if (status == NTStatus.STATUS_SUCCESS)
            {
                var error = false;
                status = _store.WriteFile(out _, fileHandle, offset: 0, data: content);
                if (status != NTStatus.STATUS_SUCCESS)
                {
                    _logger.LogWarning("Could not write to file '{Path}': {Status}", path, status.ToString());
                    error = true;
                }

                status = _store.CloseFile(fileHandle);
                if (status == NTStatus.STATUS_SUCCESS)
                {
                    if (!error)
                    {
                        success = true;
                    }
                }
                else
                {
                    _logger.LogWarning("Could not close file '{Path}': {Status}", path, status.ToString());
                }
            }
            else
            {
                _logger.LogWarning("Could not create file '{Path}': {Status}", path, status.ToString());
            }

            return success;
        });

        if (!result)
        {
            throw new SmbException($"Could not write to file {path}");
        }
    }

    public bool FileExists(string path)
    {
        var sanitizedPath = SanitizePath(path);
        var status = _store.CreateFile(out var fileHandle, out _, sanitizedPath, AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE, FileAttributes.Normal, ShareAccess.None, CreateDisposition.FILE_OPEN, CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
        if (status == NTStatus.STATUS_SUCCESS)
        {
            _store.CloseFile(fileHandle);
            return true;
        }

        if (status == NTStatus.STATUS_OBJECT_NAME_NOT_FOUND)
        {
            return false;
        }
        
        if (status == NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
        {
            return false;
        }

        throw new SmbException($"Could not ensure file '{path}' exists: {status.ToString()}");
    }

    public bool CreateDirectory(string path)
    {
        var sanitizedPath = SanitizePath(path);
        var status = _store.CreateFile(out var fileHandle, out _, sanitizedPath, AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE, FileAttributes.Directory, ShareAccess.None, CreateDisposition.FILE_OPEN_IF, CreateOptions.FILE_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
        if (status == NTStatus.STATUS_SUCCESS)
        {
            _store.CloseFile(fileHandle);
            return true;
        }

        return false;
    }

    public bool DirectoryExists(string path)
    {
        var sanitizedPath = SanitizePath(path);
        var status = _store.CreateFile(out var fileHandle, out _, sanitizedPath, AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE, FileAttributes.Directory, ShareAccess.None, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
        if (status == NTStatus.STATUS_SUCCESS)
        {
            _store.CloseFile(fileHandle);
            return true;
        }

        if (status == NTStatus.STATUS_OBJECT_NAME_NOT_FOUND)
        {
            return false;
        }
        
        if (status == NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
        {
            return false;
        }

        throw new SmbException($"Could not ensure directory '{path}' exists: {status.ToString()}");
    }

    public IList<string> EnumerateDirectories(string path)
    {
        var directories = new SortedDictionary<DateTime, string>();
        var sanitizedPath = SanitizePath(path);
        var status = _store.CreateFile(out var directoryHandle, out _, sanitizedPath, AccessMask.GENERIC_READ, FileAttributes.Directory, ShareAccess.Read | ShareAccess.Write, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);
        if (status == NTStatus.STATUS_SUCCESS)
        {
            _store.QueryDirectory(out var result, directoryHandle, fileName: "*", FileInformationClass.FileDirectoryInformation);
            foreach (var item in result)
            {
                if (item is FileDirectoryInformation info)
                {
                    if (info.FileAttributes.HasFlag(FileAttributes.Directory) && !info.FileName.Contains('.'))
                    {
                        directories.TryAdd(info.CreationTime, Path.Join(path, info.FileName));
                    }
                }
            }
            
            _store.CloseFile(directoryHandle);
        }

        return directories.Values.ToList();
    }
    
    public void Dispose()
    {
        _store.Disconnect();
        _client.Logoff();
        _client.Disconnect();
    }
}