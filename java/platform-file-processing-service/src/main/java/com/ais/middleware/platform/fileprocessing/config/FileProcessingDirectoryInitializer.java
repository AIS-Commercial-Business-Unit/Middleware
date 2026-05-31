package com.ais.middleware.platform.fileprocessing.config;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.ApplicationArguments;
import org.springframework.boot.ApplicationRunner;
import org.springframework.stereotype.Component;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;

@Component
public class FileProcessingDirectoryInitializer implements ApplicationRunner {

    private static final Logger log = LoggerFactory.getLogger(FileProcessingDirectoryInitializer.class);

    @Value("${fileprocessing.inbound-dir:/app/data/renewals/inbound}")
    private String inboundDir;

    @Value("${fileprocessing.processed-dir:/app/data/renewals/processed}")
    private String processedDir;

    @Value("${fileprocessing.error-dir:/app/data/renewals/error}")
    private String errorDir;

    @Override
    public void run(ApplicationArguments args) throws Exception {
        ensureDirectory("inbound", inboundDir);
        ensureDirectory("processed", processedDir);
        ensureDirectory("error", errorDir);
    }

    private void ensureDirectory(String label, String directory) throws IOException {
        Path path = Path.of(directory);
        Files.createDirectories(path);
        if (!Files.isDirectory(path)) {
            throw new IOException("Configured " + label + " path is not a directory: " + path);
        }
        if (!Files.isWritable(path)) {
            throw new IOException("Configured " + label + " path is not writable: " + path);
        }
        log.info("File processing directory ready — type={} path={}", label, path);
    }
}
