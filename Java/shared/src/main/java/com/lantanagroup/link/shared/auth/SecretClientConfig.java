package com.lantanagroup.link.shared.auth;

import com.azure.identity.DefaultAzureCredentialBuilder;
import com.azure.security.keyvault.secrets.SecretClient;
import com.azure.security.keyvault.secrets.SecretClientBuilder;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class SecretClientConfig {

  @Value("${secretmanagement.manageruri}")
  private String vaultUrl;

  @Bean
  public SecretClient createSecretClient() {
    return new SecretClientBuilder()
            .vaultUrl(vaultUrl)
            .credential(new DefaultAzureCredentialBuilder().build())
            .buildClient();
  }

}
