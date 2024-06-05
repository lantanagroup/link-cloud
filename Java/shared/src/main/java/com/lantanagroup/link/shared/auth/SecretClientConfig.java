package com.lantanagroup.link.shared.auth;

import com.azure.identity.DefaultAzureCredentialBuilder;
import com.azure.security.keyvault.secrets.SecretClient;
import com.azure.security.keyvault.secrets.SecretClientBuilder;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class SecretClientConfig {

  @Value("${secretManagement.managerUri}")
  private String vaultUrl;
  @ConditionalOnProperty(prefix = "authentication",
          name = "enableAnonymousAccess",
          havingValue = "false")
  @Bean
  public SecretClient createSecretClient() {
    return new SecretClientBuilder()
            .vaultUrl(vaultUrl)
            .credential(new DefaultAzureCredentialBuilder().build())
            .buildClient();
  }

}
