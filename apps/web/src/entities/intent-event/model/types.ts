import type { IntentsComponents } from "@/shared/api";

export type IntentEvent = IntentsComponents["schemas"]["IntentEventDto"];
export type IntentEventKind = IntentEvent["kind"];
export type IntentEventTextChange =
  IntentsComponents["schemas"]["IntentEventTextChangeDto"];
export type IntentEventLinkPayload =
  IntentsComponents["schemas"]["IntentEventLinkPayloadDto"];
export type IntentEventAuthor = NonNullable<IntentEvent["created_by"]>;
