package com.lantanagroup.link.hapi;

import java.net.HttpURLConnection;
import java.net.URL;

public class HealthCheck {
    public static void main(String[] args) {
        try {
            URL url = new URL(args[0]);
            HttpURLConnection connection = (HttpURLConnection) url.openConnection();
            int responseCode = connection.getResponseCode();
            System.out.println(responseCode);
            if (responseCode != HttpURLConnection.HTTP_OK) {
                fail();
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
