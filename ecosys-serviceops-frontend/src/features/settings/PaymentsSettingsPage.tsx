import { SettingsForm } from './components/SettingsForm'

export function PaymentsSettingsPage() {
  return (
    <SettingsForm
      title="Payments"
      subtitle="M-Pesa, Stripe, and bank transfer collection settings."
      queryKey="module-payments"
      fetchUrl="/api/settings/module/payments"
      saveUrl="/api/settings/module/payments"
      defaultValues={{
        mpesaEnabled: false,
        mpesaShortcode: '',
        mpesaConsumerKey: '',
        mpesaConsumerSecret: '',
        stripeEnabled: false,
        stripePublishableKey: '',
        stripeSecretKey: '',
        bankTransferEnabled: true,
        bankTransferInstructions: 'Bank: KCB · Account: 1234567890 · Branch: Nairobi CBD',
      }}
      fields={[
        { key: 'mpesaEnabled', label: 'Enable M-Pesa', type: 'checkbox' },
        { key: 'mpesaShortcode', label: 'M-Pesa Shortcode', type: 'text' },
        { key: 'mpesaConsumerKey', label: 'M-Pesa Consumer Key', type: 'text' },
        { key: 'mpesaConsumerSecret', label: 'M-Pesa Consumer Secret', type: 'text' },
        { key: 'stripeEnabled', label: 'Enable Stripe', type: 'checkbox' },
        { key: 'stripePublishableKey', label: 'Stripe Publishable Key', type: 'text' },
        { key: 'stripeSecretKey', label: 'Stripe Secret Key', type: 'text' },
        { key: 'bankTransferEnabled', label: 'Enable Bank Transfer', type: 'checkbox' },
        { key: 'bankTransferInstructions', label: 'Bank Transfer Instructions', type: 'textarea' },
      ]}
    />
  )
}
