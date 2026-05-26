package com.ais.middleware.common.events;

/**
 * Base interface for all domain commands, providing schema versioning support.
 * All commands should include these standard fields for forward/backward compatibility.
 */
public interface VersionedCommand {
    
    /**
     * Schema version for this command type. Start at "1.0" and increment on breaking changes.
     * - Minor version (1.1, 1.2): backward-compatible additions (new optional fields)
     * - Major version (2.0, 3.0): breaking changes (removed/renamed fields, changed semantics)
     */
    default String schemaVersion() {
        return "1.0";
    }
    
    /**
     * Command type identifier for routing and schema registry lookup.
     */
    default String commandType() {
        return this.getClass().getSimpleName();
    }
}
