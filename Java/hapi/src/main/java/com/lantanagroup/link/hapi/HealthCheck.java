package com.lantanagroup.link.hapi;

import java.net.HttpURLConnection;
import java.net.URL;

public class HealthCheck {
    private static final int TIMEOUT = 10000;

    public static void main(String[] args) {
        try {
            URL url = new URL(args[0]);
            HttpURLConnection connection = (HttpURLConnection) url.openConnection();
            connection.setConnectTimeout(TIMEOUT);
            connection.setReadTimeout(TIMEOUT);
            connection.connect();
            try {
                int responseCode = connection.getResponseCode();
                System.out.println(responseCode);
                if (responseCode != HttpURLConnection.HTTP_OK) {
                    fail();
                }
            } finally {
                connection.disconnect();
            }
        } catch (Exception e) {
            System.err.println(e);
            fail();
        }
    }

    private static void fail() {
        System.exit(1);
    }
}
