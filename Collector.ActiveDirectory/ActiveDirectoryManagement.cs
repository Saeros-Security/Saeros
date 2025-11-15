using System.Diagnostics.CodeAnalysis;
using System.DirectoryServices.Protocols;
using System.Net;
using Collector.ActiveDirectory.Exceptions;
using Microsoft.Extensions.Logging;
using SearchScope = System.DirectoryServices.Protocols.SearchScope;

namespace Collector.ActiveDirectory;

public abstract class ActiveDirectoryManagement(string domain, string username, string password, string server, int ldapPort, ILogger logger)
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private const string ShowDeletedOid = "1.2.840.113556.1.4.417";
    private const string SdFlagsOid = "1.2.840.113556.1.4.801";
    private const string ShowRecycledOid = "1.2.840.113556.1.4.2064";
    private const string ShowDeactivatedLinkOid = "1.2.840.113556.1.4.2065";
    private const string MatchingRuleBitAnd = "1.2.840.113556.1.4.803";
    private const int FlagAttrIsConstructed = 0x00000004;
    private const int FlagSchemaBaseObject = 0x00000010;
    private const int OWNER_SECURITY_INFORMATION = 0x1;
    private const int GROUP_SECURITY_INFORMATION = 0x2;
    private const int DACL_SECURITY_INFORMATION = 0x4;
    public const string DistinguishedNameAttribute = "distinguishedName";

    protected ILogger Logger { get; } = logger;
    protected NetworkCredential NetworkCredential { get; } = new(username, password, domain);
    protected LdapDirectoryIdentifier LdapDirectoryIdentifier { get; } = new(server, ldapPort);

    public static readonly DirectoryControl[] Controls =
    [
        new(ShowDeletedOid, null!, isCritical: true, serverSide: true),
        new(ShowRecycledOid, null!, isCritical: true, serverSide: true),
        new(ShowDeactivatedLinkOid, null!, isCritical: true, serverSide: true),
        new(MatchingRuleBitAnd, BerConverter.Encode("{i}", FlagSchemaBaseObject | FlagAttrIsConstructed), isCritical: false, serverSide: false),
        new(SdFlagsOid, BerConverter.Encode("{i}", OWNER_SECURITY_INFORMATION | GROUP_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION), isCritical: true, serverSide: true),
        new SearchOptionsControl(System.DirectoryServices.Protocols.SearchOption.DomainScope)
    ];
    
    protected bool TryGetRootNamingContext([MaybeNullWhen(false)] out string rootNamingContext)
    {
        using var connection = GetConnection();
        return TryGetNamingContext(Logger, connection, out rootNamingContext);
    }

    protected bool TryFindObject(string distinguishedName, string filter, SearchScope scope)
    {
        using var connection = GetConnection();
        var searchRequest = new SearchRequest(distinguishedName, filter, scope);
        searchRequest.Controls.AddRange(Controls);
        try
        {
            if (SendRequest(Logger, searchRequest, connection) is not SearchResponse searchResponse) throw new Exception("Could not send search request");
            if (searchResponse.ResultCode == ResultCode.NoSuchObject)
            {
                return false;
            }

            if (searchResponse.ResultCode == ResultCode.Success)
            {
                return true;
            }

            Execute(Logger, () => throw new LdapException((int)searchResponse.ResultCode, searchResponse.ErrorMessage));
        }
        catch (DirectoryOperationException ex)
        {
            if (ex.Response.ResultCode == ResultCode.NoSuchObject) return false;
            Execute(Logger, () => throw ex);
        }

        return false;
    }
    
    public static bool TryGetNamingContext(ILogger logger, LdapConnection connection, [MaybeNullWhen(false)] out string namingContext)
    {
        namingContext = null;
        const string defaultNc = "defaultNamingContext";
        var rootDseSearchRequest = new SearchRequest(string.Empty, ldapFilter: "(objectClass=*)", SearchScope.Base, defaultNc);
        rootDseSearchRequest.Controls.AddRange(Controls);
        var rootDseSearchResults = (SearchResponse)SendRequest(logger, rootDseSearchRequest, connection);
        if (rootDseSearchResults.ResultCode != ResultCode.Success)
        {
            throw new LdapException((int)rootDseSearchResults.ResultCode, rootDseSearchResults.ErrorMessage);
        }

        if (rootDseSearchResults.Entries.Count < 1)
        {
            return false;
        }

        var rootDse = rootDseSearchResults.Entries[0];
        if (!rootDse.Attributes.Contains(defaultNc))
        {
            return false;
        }

        namingContext = rootDse.Attributes[defaultNc][0].ToString();
        if (string.IsNullOrEmpty(namingContext)) return false;
        return true;
    }
    
    protected LdapConnection GetConnection() => CreateConnection(Logger, LdapDirectoryIdentifier, NetworkCredential);
    
    public static LdapConnection CreateConnection(ILogger logger, LdapDirectoryIdentifier ldapDirectoryIdentifier, NetworkCredential networkCredential)
    {
        var connection = new LdapConnection(ldapDirectoryIdentifier)
        {
            AuthType = AuthType.Negotiate,
            SessionOptions =
            {
                ProtocolVersion = 3,
                AutoReconnect = false,
                RootDseCache = false,
                TcpKeepAlive = false,
                ReferralChasing = ReferralChasingOptions.None,
                SecureSocketLayer = ldapDirectoryIdentifier.PortNumber == 636,
                VerifyServerCertificate = (_, _) => true,
                Signing = true,
                Sealing = true
            },
            AutoBind = false,
            Timeout = DefaultTimeout,
            Credential = networkCredential
        };

        Execute(logger, () =>
        {
            connection.Bind();
        });

        return connection;
    }

    public static DirectoryResponse SendRequest(ILogger logger, DirectoryRequest request, LdapConnection connection)
    {
        return Execute(logger, () => connection.SendRequest(request, DefaultTimeout));
    }

    private static void Execute(ILogger logger, Action action)
    {
        Execute(logger, () =>
        {
            action();
            return true;
        });
    }
    
    private static T Execute<T>(ILogger logger, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (LdapException ex) when (ex.ErrorCode == 49)
        {
            logger.LogError("The LDAP credentials are invalid");
            throw new AbortException(ex);
        }
        catch (LdapException ex) when (ex.ErrorCode == 3)
        {
            logger.LogError("The LDAP time limit has exceeded");
            throw;
        }
        catch (LdapException ex) when (ex.ErrorCode == 7)
        {
            logger.LogError("The LDAP authentication method is not supported");
            throw new AbortException(ex);
        }
        catch (LdapException ex) when (ex.ErrorCode == 8)
        {
            logger.LogError("The LDAP server requires a stronger authentication");
            throw new AbortException(ex);
        }
        catch (LdapException ex) when (ex.ErrorCode == 51)
        {
            logger.LogError("The LDAP server is busy");
            throw;
        }
        catch (LdapException ex) when (ex.ErrorCode == 52)
        {
            logger.LogError("The LDAP server is unavailable");
            throw;
        }
        catch (LdapException ex) when (ex.ErrorCode == 81)
        {
            logger.LogError("The LDAP server has been shutdown");
            throw new AbortException(ex);
        }
        catch (LdapException ex) when (ex.ErrorCode == 85)
        {
            logger.LogError("The LDAP connection has timed-out");
            throw;
        }
        catch (LdapException ex) when (ex.ErrorCode == 91)
        {
            logger.LogError("The LDAP connection could not be established");
            throw;
        }
    }
}