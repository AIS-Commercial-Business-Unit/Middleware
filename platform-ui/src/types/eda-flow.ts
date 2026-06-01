export interface FlowEventDetails {
  topic: string;
  direction: "published" | "consumed" | "handled";
  stack: "java" | "dotnet";
  timestamp: string;
  description: string;
}

export interface FlowEvent {
  messageType: string;
  from: string;
  to: string;
  topic: string;
  direction: "published" | "consumed" | "handled";
  stack: "java" | "dotnet";
  timestamp: string;
  issuanceId: string;
  handler?: string;
  payload?: Record<string, unknown>;
  details: FlowEventDetails;
}

const EVENT_DESCRIPTIONS: Record<string, string> = {
  PolicyIssuanceInitiatedEvent: "Policy issuance workflow started. Triggers compliance check.",
  IssuanceSagaStartedEvent: "Saga state initialized for this issuance.",
  IssuePolicyRequestedEvent: "Request sent to Platform.Integration to submit to the PAS system.",
  PolicyAdminSystemResponseReceivedEvent: "PAS system responded. Triggers fan-out to Billing and CustomerIdentity.",
  AccountLookupRequestedEvent: "Customer identity lookup initiated.",
  AccountServiceRecordRetrievedEvent: "Customer account record returned from CRM.",
  ComplianceClearedEvent: "Compliance check passed. Saga can proceed.",
  ComplianceBlockedEvent: "Compliance check failed. Issuance blocked.",
  BillingAssociationCreatedEvent: "Billing account associated with the new policy.",
  CustomerUpdatedEvent: "Customer record updated with new policy reference.",
  PolicyIssuedEvent: "Policy successfully issued. Notification dispatched.",
  IssuanceFailedEvent: "Issuance failed. See saga status for details.",
  PublishNotificationIntentCommand: "Notification intent queued for dispatch.",
  NotificationDispatchedEvent: "Notification sent to policyholder.",
};

export function getEventDescription(messageType: string): string {
  return EVENT_DESCRIPTIONS[messageType] ?? `${messageType} — no description available`;
}
