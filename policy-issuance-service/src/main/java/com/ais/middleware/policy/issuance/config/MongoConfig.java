package com.ais.middleware.policy.issuance.config;

import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.core.convert.converter.Converter;
import org.springframework.data.convert.ReadingConverter;
import org.springframework.data.convert.WritingConverter;
import org.springframework.data.mongodb.core.convert.MongoCustomConversions;

import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.Date;
import java.util.List;

/**
 * Registers custom MongoDB converters for java.time.OffsetDateTime.
 * Spring Data MongoDB 4.x supports Instant and LocalDateTime natively but not OffsetDateTime.
 * These converters store OffsetDateTime as a BSON Date (UTC) and read it back with UTC offset.
 *
 * NOTE: Lambda converters cannot be used here — Spring needs concrete classes to resolve
 * the generic type parameters <From, To> at runtime.
 */
@Configuration
public class MongoConfig {

    @WritingConverter
    public static class OffsetDateTimeToDateConverter implements Converter<OffsetDateTime, Date> {
        @Override
        public Date convert(OffsetDateTime source) {
            return source == null ? null : Date.from(source.toInstant());
        }
    }

    @ReadingConverter
    public static class DateToOffsetDateTimeConverter implements Converter<Date, OffsetDateTime> {
        @Override
        public OffsetDateTime convert(Date source) {
            return source == null ? null : OffsetDateTime.ofInstant(source.toInstant(), ZoneOffset.UTC);
        }
    }

    @Bean
    public MongoCustomConversions mongoCustomConversions() {
        return new MongoCustomConversions(List.of(
                new OffsetDateTimeToDateConverter(),
                new DateToOffsetDateTimeConverter()
        ));
    }
}
