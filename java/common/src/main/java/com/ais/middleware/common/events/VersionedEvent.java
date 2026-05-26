package com.ais.middleware.common.events;

/**
 * Base interface for all domain events, providing schema versioning support.
 * All events should include these standard fields for forward/backward compatibility.
 * 
 * Consumers should check schemaVersion and handle unknown versions gracefully.
 */
public interface VersionedEvent {
    
    /**
     * Schema version for this event type. Start at "1.0" and increment on breaking changes.
     * - Minor version (1.1, 1.2): backward-compatible additions (new optional fields)
     * - Major version (2.0, 3.0): breaking changes (removed/renamed fields, changed semantics)
     */
    default String schemaVersion() {
        return "1.0";
    }
    
    /**
     * Event type identifier for routing and schema registry lookup.
     */
    default String eventType() {
        return this.getClass().getSimpleName();
    }
}
