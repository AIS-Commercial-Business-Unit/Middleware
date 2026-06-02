package com.ais.middleware.common.kafka;

import com.azure.core.credential.AccessToken;
import com.azure.core.credential.TokenRequestContext;
import com.azure.identity.DefaultAzureCredentialBuilder;
import com.azure.identity.DefaultAzureCredential;
import org.apache.kafka.common.security.auth.AuthenticateCallbackHandler;
import org.apache.kafka.common.security.oauthbearer.OAuthBearerToken;
import org.apache.kafka.common.security.oauthbearer.OAuthBearerTokenCallback;

import javax.security.auth.callback.Callback;
import javax.security.auth.callback.UnsupportedCallbackException;
import javax.security.auth.login.AppConfigurationEntry;
import java.io.IOException;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.Set;

/**
 * Kafka OAUTHBEARER callback handler that acquires tokens from Azure Event Hubs
 * using {@link DefaultAzureCredential}. In AKS with workload identity, this
 * automatically uses the federated service account token. Locally (if the azure
 * profile is somehow activated), it falls through to Azure CLI or env-based auth.
 *
 * <p>This keeps the default (non-azure) profile completely unaffected — plain
 * Kafka with no auth for docker-compose local dev.
 */
public class AzureEventHubsLoginCallbackHandler implements AuthenticateCallbackHandler {

    private DefaultAzureCredential credential;
    private String scope;

    @Override
    public void configure(Map<String, ?> configs, String saslMechanism, List<AppConfigurationEntry> jaasConfigEntries) {
        this.credential = new DefaultAzureCredentialBuilder().build();

        // Extract scope from JAAS config options (set in application.yml / env vars)
        // e.g. scope="https://evhns-middleware-dev.servicebus.windows.net/.default"
        if (jaasConfigEntries != null && !jaasConfigEntries.isEmpty()) {
            Map<String, ?> options = jaasConfigEntries.get(0).getOptions();
            this.scope = (String) options.get("scope");
        }
        if (this.scope == null || this.scope.isBlank()) {
            throw new IllegalArgumentException(
                "JAAS config must include scope for Azure Event Hubs, e.g. " +
                "scope=\"https://<namespace>.servicebus.windows.net/.default\"");
        }
    }

    @Override
    public void handle(Callback[] callbacks) throws IOException, UnsupportedCallbackException {
        for (Callback callback : callbacks) {
            if (callback instanceof OAuthBearerTokenCallback oauthCallback) {
                try {
                    TokenRequestContext context = new TokenRequestContext();
                    context.addScopes(scope);
                    AccessToken accessToken = credential.getTokenSync(context);

                    oauthCallback.token(new OAuthBearerToken() {
                        @Override
                        public String value() {
                            return accessToken.getToken();
                        }

                        @Override
                        public Long startTimeMs() {
                            return System.currentTimeMillis();
                        }

                        @Override
                        public long lifetimeMs() {
                            return accessToken.getExpiresAt().toInstant().toEpochMilli();
                        }

                        @Override
                        public Set<String> scope() {
                            return Collections.singleton(AzureEventHubsLoginCallbackHandler.this.scope);
                        }

                        @Override
                        public String principalName() {
                            return "azure-workload-identity";
                        }
                    });
                } catch (Exception e) {
                    oauthCallback.error("token_retrieval_failed", e.getMessage(), null);
                }
            } else {
                throw new UnsupportedCallbackException(callback);
            }
        }
    }

    @Override
    public void close() {
        // no-op
    }
}
