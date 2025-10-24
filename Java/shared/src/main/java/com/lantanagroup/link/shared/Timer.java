package com.lantanagroup.link.shared;

public class Timer implements AutoCloseable {
    private final long start;

    private Timer() {
        this.start = System.currentTimeMillis();
    }

    public static Timer start() {
        return new Timer();
    }

    public double getSeconds() {
        return (System.currentTimeMillis() - start) / 1000.0;
    }

    @Override
    public void close() {
        // Optional: Add any cleanup if needed
    }
}
