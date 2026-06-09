// Initialize one MongoDB database per domain service
// This script runs when MongoDB starts for the first time

const databases = [
    "policy_issuance_db",
    "compliance_db",
    "customer_identity_db",
    "integration_db",
    "billing_finance_db",
    "notification_db",
    "file_processing_db",
    "prs_appraisal_db",
    "middleware-platform"
];

databases.forEach(dbName => {
    const targetDb = db.getSiblingDB(dbName);
    // Create a placeholder collection to ensure the DB is listed in Mongo Express
    targetDb.createCollection("_init");
    print(`Initialized database: ${dbName}`);
});
