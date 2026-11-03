# Email Outbox Sensitive Flow Design

## Scope

Stage 1 of the email outbox queues only non-sensitive email traffic so user-facing actions can return quickly without weakening authentication safety.

## Sensitive Flows That Remain Synchronous

- `user-credentials`
- `resend-credentials`
- admin password reset actions that may rotate a temporary password
- self-service password reset link generation and delivery

## Why These Flows Stay Synchronous In Stage 1

- Raw temporary passwords should not sit in a durable queue.
- Resend credentials must not change a user's password unless delivery succeeds.
- Existing safe resend behavior already protects users from being locked out on delivery failure.
- Password reset links are safer than emailed temporary passwords, but they are still part of the auth boundary and should keep their current explicit delivery/audit flow until we add end-to-end queue confirmation rules for security events.

## Stage 1 Queued Flows

- SMTP/platform test emails
- template test emails
- tenant onboarding emails
- platform lead notifications
- other non-sensitive operational notifications when already wired

## Recommended Next Step

Preferred approach: queue reset-link style security emails first.

- Keep the user's current password unchanged.
- Generate a short-lived reset or activation token.
- Queue the email that contains only the link/token reference.
- Let the user complete the password change after opening the link.

This design is safe because delivery failure does not invalidate the current password.

## Alternative For Temporary Password Flows

If the product must continue sending temporary passwords by email, use a pending activation design:

- generate a temporary credential payload in memory
- queue an email referencing a one-time activation token, not the plaintext password itself
- mark the pending credential as inactive until delivery is confirmed
- activate the new credential only after delivery succeeds
- expire the pending credential if delivery fails or the token is never used

## Current Recommendation

- Keep `resend-credentials` synchronous until a token-based activation flow replaces raw temporary password delivery.
- Prefer evolving user onboarding and resend flows toward reset links instead of emailed passwords.
