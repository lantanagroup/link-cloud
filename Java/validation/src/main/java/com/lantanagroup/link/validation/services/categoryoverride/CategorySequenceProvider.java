package com.lantanagroup.link.validation.services.categoryoverride;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import jakarta.annotation.PostConstruct;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.core.io.Resource;
import org.springframework.core.io.support.PathMatchingResourcePatternResolver;
import org.springframework.stereotype.Component;

import java.io.InputStream;
import java.util.HashMap;
import java.util.Map;

@Component
@RequiredArgsConstructor
@Slf4j
public class CategorySequenceProvider {

    static final int UNSEQUENCED = Integer.MAX_VALUE;

    private final ObjectMapper objectMapper;

    private Map<String, Integer> sequenceById = Map.of();

    @PostConstruct
    void load() {
        Resource resource = new PathMatchingResourcePatternResolver().getResource("classpath:categories.json");
        Map<String, Integer> loaded = new HashMap<>();
        try (InputStream stream = resource.getInputStream()) {
            JsonNode root = objectMapper.readTree(stream);
            int sequence = 0;
            for (JsonNode node : root) {
                String id = node.path("id").asText(null);
                if (id != null && !id.isBlank()) {
                    loaded.putIfAbsent(id, sequence++);
                }
            }
        } catch (Exception e) {
            // Not fatal: without the file every category is unsequenced and ties fall to id order,
            // which is still deterministic. Startup must not depend on a tie-break input.
            log.warn("Could not read category sequence from classpath:categories.json ({}); "
                    + "category ties will be broken by id", e.getMessage());
        }
        sequenceById = Map.copyOf(loaded);
        log.debug("Loaded sequence for {} categories", sequenceById.size());
    }

    public int sequenceOf(String categoryId) {
        return sequenceById.getOrDefault(categoryId, UNSEQUENCED);
    }
}
