package com.lantanagroup.link.validation.providers;

import com.fasterxml.jackson.annotation.JsonAutoDetect;
import com.fasterxml.jackson.annotation.PropertyAccessor;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.jsontype.impl.LaissezFaireSubTypeValidator;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.RubricVersionSnapshot;
import com.lantanagroup.link.validation.repositories.RubricCheckRepository;
import com.lantanagroup.link.validation.repositories.RubricVersionRepository;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.cache.CacheManager;
import org.springframework.cache.annotation.EnableCaching;
import org.springframework.cache.concurrent.ConcurrentMapCacheManager;
import org.springframework.context.annotation.AnnotationConfigApplicationContext;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import java.util.List;
import java.util.Optional;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.times;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class RubricCacheServiceTest {

    private static final UUID VERSION_ID = UUID.randomUUID();

    private AnnotationConfigApplicationContext context;
    private RubricCacheService cacheService;
    private RubricVersionRepository versionRepository;
    private RubricCheckRepository checkRepository;

    @Configuration
    @EnableCaching
    static class CacheTestConfig {
        @Bean
        CacheManager cacheManager() {
            return new ConcurrentMapCacheManager("rubricVersionCache", "rubricLatestSemverCache");
        }

        @Bean
        RubricVersionRepository rubricVersionRepository() {
            return mock(RubricVersionRepository.class);
        }

        @Bean
        RubricCheckRepository rubricCheckRepository() {
            return mock(RubricCheckRepository.class);
        }

        @Bean
        RubricCacheService rubricCacheService(RubricVersionRepository versionRepository,
                                              RubricCheckRepository checkRepository,
                                              CacheManager cacheManager) {
            return new RubricCacheService(versionRepository, checkRepository, cacheManager);
        }
    }

    @BeforeEach
    void setUp() {
        context = new AnnotationConfigApplicationContext(CacheTestConfig.class);
        cacheService = context.getBean(RubricCacheService.class);
        versionRepository = context.getBean(RubricVersionRepository.class);
        checkRepository = context.getBean(RubricCheckRepository.class);
    }

    @AfterEach
    void tearDown() {
        context.close();
    }

    private static RubricVersion version(RubricVersionStatus status) {
        return RubricVersion.builder()
                .rubricVersionId(VERSION_ID)
                .rubricId("piqi.core")
                .semver("1.0.0")
                .status(status)
                .checksum("abc")
                .build();
    }

    private static RubricCheck check() {
        return RubricCheck.builder()
                .checkId(UUID.randomUUID())
                .rubricVersionId(VERSION_ID)
                .checkLocalId("c1")
                .type(CheckType.FHIRPATH)
                .dimension(PiqiDimension.CONFORMANCE)
                .parametersJson("{\"expr\":\"name.exists()\"}")
                .severityOverride(Severity.WARNING)
                .ordinal(1)
                .enabled(true)
                .build();
    }

    private void stubDb() {
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0"))
                .thenReturn(Optional.of(version(RubricVersionStatus.PUBLISHED)));
        when(checkRepository.findByRubricVersionIdAndDeletedFalseOrderByOrdinalAsc(VERSION_ID))
                .thenReturn(List.of(check()));
        when(versionRepository.findByRubricIdAndStatus("piqi.core", RubricVersionStatus.PUBLISHED))
                .thenReturn(List.of(version(RubricVersionStatus.PUBLISHED)));
    }

    @Test
    @DisplayName("first get hits the DB and caches; the second identical get is served from the cache")
    void firstGetFromDbSecondFromCache() {
        stubDb();

        RubricVersionSnapshot first = cacheService.getVersion("piqi.core", "1.0.0");
        RubricVersionSnapshot second = cacheService.getVersion("piqi.core", "1.0.0");

        assertThat(first).isNotNull();
        assertThat(second).isEqualTo(first);
        verify(versionRepository, times(1)).findByRubricIdAndSemver("piqi.core", "1.0.0");
        verify(checkRepository, times(1)).findByRubricVersionIdAndDeletedFalseOrderByOrdinalAsc(VERSION_ID);
    }

    @Test
    @DisplayName("evictVersion removes the semver entry AND the latest pointer, so both re-query the DB")
    void evictVersionClearsBothCaches() {
        stubDb();
        cacheService.getVersion("piqi.core", "1.0.0");
        cacheService.getLatestPublishedSemver("piqi.core");

        cacheService.evictVersion("piqi.core", "1.0.0");
        cacheService.getVersion("piqi.core", "1.0.0");
        cacheService.getLatestPublishedSemver("piqi.core");

        verify(versionRepository, times(2)).findByRubricIdAndSemver("piqi.core", "1.0.0");
        verify(versionRepository, times(2)).findByRubricIdAndStatus("piqi.core", RubricVersionStatus.PUBLISHED);
    }

    @Test
    @DisplayName("latest published semver is cached and ordered semantically (1.10.0 > 1.9.0)")
    void latestPublishedSemverCachedAndSemantic() {
        when(versionRepository.findByRubricIdAndStatus("piqi.core", RubricVersionStatus.PUBLISHED))
                .thenReturn(List.of(
                        RubricVersion.builder().rubricId("piqi.core").semver("1.9.0")
                                .status(RubricVersionStatus.PUBLISHED).checksum("x").build(),
                        RubricVersion.builder().rubricId("piqi.core").semver("1.10.0")
                                .status(RubricVersionStatus.PUBLISHED).checksum("x").build()));

        assertThat(cacheService.getLatestPublishedSemver("piqi.core")).isEqualTo("1.10.0");
        assertThat(cacheService.getLatestPublishedSemver("piqi.core")).isEqualTo("1.10.0");
        verify(versionRepository, times(1)).findByRubricIdAndStatus("piqi.core", RubricVersionStatus.PUBLISHED);
    }

    @Test
    @DisplayName("not-found results are never cached, so a version registered after a miss is visible immediately")
    void nullResultsAreNotCached() {
        when(versionRepository.findByRubricIdAndSemver("piqi.core", "1.0.0")).thenReturn(Optional.empty());

        assertThat(cacheService.getVersion("piqi.core", "1.0.0")).isNull();

        stubDb();
        assertThat(cacheService.getVersion("piqi.core", "1.0.0")).isNotNull();
        verify(versionRepository, times(2)).findByRubricIdAndSemver("piqi.core", "1.0.0");
    }

    @Test
    @DisplayName("snapshot mapping round-trips all scalar fields of the version and its checks")
    void snapshotMappingRoundTrip() {
        RubricVersion v = version(RubricVersionStatus.PUBLISHED);
        RubricCheck c = check();

        RubricVersionSnapshot snapshot = RubricVersionSnapshot.from(v, List.of(c));
        RubricVersion mappedVersion = snapshot.toVersionEntity();
        RubricCheck mappedCheck = snapshot.toCheckEntities().get(0);

        assertThat(mappedVersion.getRubricVersionId()).isEqualTo(v.getRubricVersionId());
        assertThat(mappedVersion.getRubricId()).isEqualTo(v.getRubricId());
        assertThat(mappedVersion.getSemver()).isEqualTo(v.getSemver());
        assertThat(mappedVersion.getStatus()).isEqualTo(v.getStatus());
        assertThat(mappedVersion.getChecksum()).isEqualTo(v.getChecksum());

        assertThat(mappedCheck.getCheckId()).isEqualTo(c.getCheckId());
        assertThat(mappedCheck.getRubricVersionId()).isEqualTo(c.getRubricVersionId());
        assertThat(mappedCheck.getCheckLocalId()).isEqualTo(c.getCheckLocalId());
        assertThat(mappedCheck.getType()).isEqualTo(c.getType());
        assertThat(mappedCheck.getDimension()).isEqualTo(c.getDimension());
        assertThat(mappedCheck.getParametersJson()).isEqualTo(c.getParametersJson());
        assertThat(mappedCheck.getSeverityOverride()).isEqualTo(c.getSeverityOverride());
        assertThat(mappedCheck.getOrdinal()).isEqualTo(c.getOrdinal());
        assertThat(mappedCheck.isEnabled()).isEqualTo(c.isEnabled());
    }

    @Test
    @DisplayName("snapshot survives a Redis-style serialization round-trip (NON_FINAL default typing)")
    void snapshotSurvivesRedisSerialization() throws Exception {
        // mirror of RedisCacheConfig.redisCacheConfiguration()'s ObjectMapper: if the snapshot
        // ever becomes a record/final class, this deserializes as LinkedHashMap and fails here
        // instead of at runtime in Redis mode
        ObjectMapper redisStyleMapper = new ObjectMapper()
                .registerModule(new JavaTimeModule())
                .activateDefaultTypingAsProperty(
                        LaissezFaireSubTypeValidator.instance,
                        ObjectMapper.DefaultTyping.NON_FINAL,
                        "@class")
                .setVisibility(PropertyAccessor.ALL, JsonAutoDetect.Visibility.ANY)
                .configure(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES, false);

        RubricVersionSnapshot original =
                RubricVersionSnapshot.from(version(RubricVersionStatus.PUBLISHED), List.of(check()));

        String json = redisStyleMapper.writeValueAsString(original);
        Object roundTripped = redisStyleMapper.readValue(json, Object.class);

        assertThat(roundTripped).isInstanceOf(RubricVersionSnapshot.class);
        assertThat(roundTripped).isEqualTo(original);

        // the latest-pointer cache stores a plain String — it must round-trip as a String too
        Object semver = redisStyleMapper.readValue(redisStyleMapper.writeValueAsString("1.10.0"), Object.class);
        assertThat(semver).isEqualTo("1.10.0");
    }
}
