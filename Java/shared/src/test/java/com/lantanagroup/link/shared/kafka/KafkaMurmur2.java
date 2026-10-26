package com.lantanagroup.link.shared.kafka;

import java.nio.charset.StandardCharsets;

/**
 * Kafka's Utils.murmur2, the partition hash used by the Java client and by librdkafka Murmur2Random.
 */
public final class KafkaMurmur2 {

    private KafkaMurmur2() {
    }

    public static int partition(String key, int partitionCount) {
        if (partitionCount < 1) {
            throw new IllegalArgumentException("partitionCount");
        }
        int hash = murmur2(key.getBytes(StandardCharsets.UTF_8));
        return toPositive(hash) % partitionCount;
    }

    public static int toPositive(int number) {
        return number & 0x7fffffff;
    }

    public static int murmur2(byte[] data) {
        final int seed = 0x9747b28c;
        final int m = 0x5bd1e995;
        final int r = 24;
        int length = data.length;
        int h = seed ^ length;
        int length4 = length / 4;

        for (int i = 0; i < length4; i++) {
            int i4 = i * 4;
            int k = (data[i4] & 0xff)
                    | ((data[i4 + 1] & 0xff) << 8)
                    | ((data[i4 + 2] & 0xff) << 16)
                    | ((data[i4 + 3] & 0xff) << 24);
            k *= m;
            k ^= k >>> r;
            k *= m;
            h *= m;
            h ^= k;
        }

        int remainder = length & ~3;
        switch (length % 4) {
            case 3:
                h ^= (data[remainder + 2] & 0xff) << 16;
            case 2:
                h ^= (data[remainder + 1] & 0xff) << 8;
            case 1:
                h ^= data[remainder] & 0xff;
                h *= m;
                break;
            default:
                break;
        }

        h ^= h >>> 13;
        h *= m;
        h ^= h >>> 15;
        return h;
    }
}
