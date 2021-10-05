// Copyright (c) Microsoft Corporation
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace Azure.CloudEvents.EventGridBridge
{
    using System;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using global::Azure.Identity;
    using Microsoft.Rest;

    public class AzureIdentityTokenProvider : ITokenProvider
    {
        static readonly TimeSpan ExpirationThreshold = TimeSpan.FromMinutes(5);

        AccessToken? accessToken;

        string[] scopes;

        TokenCredential tokenCredential;

        public AzureIdentityTokenProvider(string[] scopes = null) : this(new DefaultAzureCredential(), scopes)
        {
        }

        public AzureIdentityTokenProvider(TokenCredential tokenCredential, string[] scopes = null)
        {
            if (scopes == null || scopes.Length == 0)
            {
                scopes = new string[] { "https://management.azure.com/.default" };
            }

            this.scopes = scopes;
            this.tokenCredential = tokenCredential;
        }

        protected virtual bool AccessTokenExpired
        {
            get
            {
                return !this.accessToken.HasValue
                    ? true
                    : (DateTime.UtcNow + ExpirationThreshold >= this.accessToken.Value.ExpiresOn);
            }
        }

        public virtual async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(
            CancellationToken cancellationToken)
        {
            var accessToken = await GetTokenAsync(cancellationToken);
            return new AuthenticationHeaderValue("Bearer", accessToken.Token);
        }

        public virtual async Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken)
        {
            if (!this.accessToken.HasValue || AccessTokenExpired)
            {
                this.accessToken = await this.tokenCredential
                    .GetTokenAsync(new TokenRequestContext(this.scopes), cancellationToken).ConfigureAwait(false);
            }

            return this.accessToken.Value;
        }
    }

    public class AzureIdentityCredentialAdapter : TokenCredentials
    {
        public AzureIdentityCredentialAdapter(string[] scopes = null) : base(new AzureIdentityTokenProvider(scopes))
        {
        }

        public AzureIdentityCredentialAdapter(TokenCredential tokenCredential, string[] scopes = null) : base(new AzureIdentityTokenProvider(tokenCredential, scopes))
        {

        }
    }
}