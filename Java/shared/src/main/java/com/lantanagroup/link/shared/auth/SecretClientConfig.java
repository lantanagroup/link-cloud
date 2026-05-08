package com.lantanagroup.link.shared.auth;

import com.azure.identity.DefaultAzureCredentialBuilder;
import com.azure.security.keyvault.secrets.SecretClient;
import com.azure.security.keyvault.secrets.SecretClientBuilder;
import com.lantanagroup.link.shared.config.SecretManagementConfig;
import org.apache.commons.lang3.StringUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class SecretClientConfig {
  private static final Logger logger = LoggerFactory.getLogger(SecretClientConfig.class);
  private final SecretManagementConfig secretManagementConfig;

  public SecretClientConfig(SecretManagementConfig secretManagementConfig) {
      this.secretManagementConfig = secretManagementConfig;
  }

  @Bean()
  public SecretClient createSecretClient() {
    if (StringUtils.isEmpty(this.secretManagementConfig.getKeyVaultUri())) {
      logger.warn("Key Vault URI is not set. Secret client will not be created.");
      return null;
    }

    return new SecretClientBuilder()
            .vaultUrl(this.secretManagementConfig.getKeyVaultUri())
            .credential(new DefaultAzureCredentialBuilder().build())
            .buildClient();
  }
}
