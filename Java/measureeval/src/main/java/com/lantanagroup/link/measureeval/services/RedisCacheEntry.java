package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.Resource;

import java.util.List;

/**
 * One Redis read of a cache entry: its resources and the durable count recorded alongside them,
 * taken from the same HGETALL so the two always describe the same instant.
 *
 * @param resources               the resources parsed from the entry; empty when the key is absent
 * @param durableCount            the {@code __durableResourceCount} recorded on the entry, or
 *                                {@code null} when none is recorded or it does not parse
 * @param durableCountUnparseable {@code true} when a count was recorded but is not a number
 */
public record RedisCacheEntry(List<Resource> resources, Integer durableCount, boolean durableCountUnparseable) {
}
