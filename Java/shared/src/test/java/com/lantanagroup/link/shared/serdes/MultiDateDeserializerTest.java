package com.lantanagroup.link.shared.serdes;

import com.fasterxml.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import java.util.Date;

public class MultiDateDeserializerTest {
    @Test
    public void deserializeTest() throws Exception {
        ObjectMapper mapper = new ObjectMapper();
        com.fasterxml.jackson.databind.module.SimpleModule module = new com.fasterxml.jackson.databind.module.SimpleModule();
        module.addDeserializer(Date.class, new MultiDateDeserializer());
        mapper.registerModule(module);

        String dateStr1 = "\"2026-01-20T12:58:00.000Z\"";
        Date date1 = mapper.readValue(dateStr1, Date.class);
        Assertions.assertNotNull(date1);

        String dateStr2 = "\"2026-01-20T12:58:00Z\"";
        Date date2 = mapper.readValue(dateStr2, Date.class);
        Assertions.assertNotNull(date2);
        
        Assertions.assertEquals(date1.getTime(), date2.getTime());
    }
}
