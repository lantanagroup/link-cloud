package com.lantanagroup.link.shared.serdes;

import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.databind.DeserializationContext;
import com.fasterxml.jackson.databind.JsonDeserializer;
import org.apache.commons.lang3.StringUtils;

import java.io.IOException;
import java.text.ParseException;
import java.text.SimpleDateFormat;
import java.util.Date;

public class MultiDateDeserializer extends JsonDeserializer<Date> {
    private static final String[] DATE_FORMATS = new String[]{
            "yyyy-MM-dd'T'HH:mm:ss.SSSX",
            "yyyy-MM-dd'T'HH:mm:ssX"
    };

    @Override
    public Date deserialize(JsonParser jp, DeserializationContext ctxt) throws IOException {
        String date = jp.getText();
        if (StringUtils.isEmpty(date)) {
            return null;
        }

        for (String format : DATE_FORMATS) {
            try {
                return new SimpleDateFormat(format).parse(date);
            } catch (ParseException e) {
                // Try next format
            }
        }

        throw new IOException("Unable to parse date: " + date);
    }
}
