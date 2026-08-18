package com.lantanagroup.link.measureeval.configs;

import org.bson.Document;
import org.springframework.core.convert.converter.Converter;
import org.springframework.data.convert.ReadingConverter;

@ReadingConverter
public class DocumentToStringConverter implements Converter<Document, String> {
    @Override
    public String convert(Document source) {
        return source.toJson();
    }
}
