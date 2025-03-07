using System.Collections.Generic;

namespace Amazon.Lambda.AppSyncEvents
{
    /// <summary>
    /// Represents OPENID CONNECT authorization identity for AppSync
    /// </summary>
    public class AppSyncOidcIdentity
    {
        /// <summary>
        /// Claims from the OIDC token as key-value pairs
        /// </summary>
        public Dictionary<string, object> Claims { get; set; }

        /// <summary>
        /// The issuer of the OIDC token
        /// </summary>
        public string Issuer { get; set; }

        /// <summary>
        /// The UUID of the authenticated user
        /// </summary>
        public string Sub { get; set; }
    }
}