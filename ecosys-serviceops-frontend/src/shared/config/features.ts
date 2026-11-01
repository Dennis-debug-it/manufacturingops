/**
 * Switches for whole features, matching the backend's "Features" configuration section (appsettings).
 * Off, a feature's screens, settings, menu entries and buttons are hidden; the backend does no work for
 * it and refuses changes. Flip both (here and in the backend's configuration) to bring a feature back.
 */

/**
 * WhatsApp everywhere: the inbox and its settings, WhatsApp reminders and their settings, opt-in cards,
 * the WhatsApp channel in notification preferences, WhatsApp on client contacts and on sales deals.
 */
export const WHATSAPP_ENABLED = false

/**
 * Email intake: the intake mailbox settings (emails become work orders), the sales lead address and
 * replies coming back onto deals, and the Email intake help guide.
 */
export const EMAIL_INTAKE_ENABLED = false
