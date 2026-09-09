package com.lantanagroup.link.validation.configs;

import com.lantanagroup.link.validation.providers.RubricCacheService;
import org.junit.jupiter.api.Test;
import org.springframework.boot.autoconfigure.data.redis.RedisProperties;
import org.springframework.data.redis.cache.RedisCacheConfiguration;
import org.springframework.data.redis.cache.RedisCacheManager;
import org.springframework.data.redis.connection.RedisStandaloneConfiguration;
import org.springframework.data.redis.connection.lettuce.LettuceConnectionFactory;
import org.springframework.data.redis.serializer.RedisSerializationContext;

import java.nio.ByteBuffer;
import java.time.Duration;
import java.time.OffsetDateTime;

import static org.assertj.core.api.Assertions.assertThat;

class RedisCacheConfigTest {

    private static RedisProperties properties(
            String host, int port, int database, String username, String password, boolean ssl) {
        RedisProperties properties = new RedisProperties();
        properties.setHost(host);
        properties.setPort(port);
        properties.setDatabase(database);
        properties.setUsername(username);
        properties.setPassword(password);
        properties.getSsl().setEnabled(ssl);
        return properties;
    }

    @Test
    void redisConnectionFactory_wiresHostPortAndDatabaseFromProperties() {
        RedisCacheConfig config = new RedisCacheConfig(
                new CacheConfig(), properties("redis-host", 6380, 2, null, null, false));

        LettuceConnectionFactory factory = (LettuceConnectionFactory) config.redisConnectionFactory();

        assertThat(factory.getHostName()).isEqualTo("redis-host");
        assertThat(factory.getPort()).isEqualTo(6380);
        assertThat(factory.getDatabase()).isEqualTo(2);
    }

    @Test
    void redisConnectionFactory_wiresUsernameAndPasswordWhenPresent() {
        RedisCacheConfig config = new RedisCacheConfig(
                new CacheConfig(), properties("redis-host", 6379, 0, "redisuser", "s3cret", false));

        LettuceConnectionFactory factory = (LettuceConnectionFactory) config.redisConnectionFactory();
        RedisStandaloneConfiguration standalone = factory.getStandaloneConfiguration();

        assertThat(standalone.getUsername()).isEqualTo("redisuser");
        assertThat(standalone.getPassword().isPresent()).isTrue();
        assertThat(new String(standalone.getPassword().get())).isEqualTo("s3cret");
    }

    @Test
    void redisConnectionFactory_leavesCredentialsUnsetWhenAbsent() {
        RedisCacheConfig config = new RedisCacheConfig(
                new CacheConfig(), properties("redis-host", 6379, 0, null, null, false));

        LettuceConnectionFactory factory = (LettuceConnectionFactory) config.redisConnectionFactory();
        RedisStandaloneConfiguration standalone = factory.getStandaloneConfiguration();

        assertThat(standalone.getUsername()).isNullOrEmpty();
        assertThat(standalone.getPassword().isPresent()).isFalse();
    }

    @Test
    void redisCacheConfiguration_usesValidateCodeTtlAsTheDefault() {
        CacheConfig cacheConfig = new CacheConfig();
        cacheConfig.getValidateCode().setTtl(90);
        RedisCacheConfig config = new RedisCacheConfig(cacheConfig, properties("h", 6379, 0, null, null, false));

        RedisCacheConfiguration redisCacheConfiguration = config.redisCacheConfiguration();

        assertThat(redisCacheConfiguration.getTtl()).isEqualTo(Duration.ofSeconds(90));
    }

    @Test
    void redisCacheConfiguration_serializerRoundTripsAPolymorphicValueAndAnOffsetDateTime() {
        RedisCacheConfig config = new RedisCacheConfig(new CacheConfig(), properties("h", 6379, 0, null, null, false));
        RedisCacheConfiguration redisCacheConfiguration = config.redisCacheConfiguration();
        RedisSerializationContext.SerializationPair<Object> pair = redisCacheConfiguration.getValueSerializationPair();

        SamplePayload original = new SamplePayload();
        original.setName("hello");
        original.setTimestamp(OffsetDateTime.parse("2026-01-01T00:00:00Z"));

        ByteBuffer serialized = pair.write(original);
        Object roundTripped = pair.read(serialized);

        assertThat(roundTripped).isInstanceOf(SamplePayload.class);
        SamplePayload result = (SamplePayload) roundTripped;
        assertThat(result.getName()).isEqualTo("hello");
        assertThat(result.getTimestamp()).isEqualTo(original.getTimestamp());
    }

    @Test
    void redisCacheManager_registersTheRubricCachesAsInitialCaches() {
        CacheConfig cacheConfig = new CacheConfig();
        cacheConfig.getRubric().setTtl(300);
        RedisCacheConfig config = new RedisCacheConfig(cacheConfig, properties("h", 6379, 0, null, null, false));
        LettuceConnectionFactory factory = (LettuceConnectionFactory) config.redisConnectionFactory();

        RedisCacheManager cacheManager = (RedisCacheManager) config.redisCacheManager(factory, config.redisCacheConfiguration());
        // Mirrors what the Spring container does for any InitializingBean before first use; this manager
        // is never opened over the network by doing so (RedisCache wrappers hold the factory lazily).
        cacheManager.afterPropertiesSet();

        assertThat(cacheManager.getCacheNames()).contains(
                RubricCacheService.VERSION_CACHE, RubricCacheService.LATEST_SEMVER_CACHE);
    }

    public static class SamplePayload {
        private String name;
        private OffsetDateTime timestamp;

        public String getName() {
            return name;
        }

        public void setName(String name) {
            this.name = name;
        }

        public OffsetDateTime getTimestamp() {
            return timestamp;
        }

        public void setTimestamp(OffsetDateTime timestamp) {
            this.timestamp = timestamp;
        }
    }
}
