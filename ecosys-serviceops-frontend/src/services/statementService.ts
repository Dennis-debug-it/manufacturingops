import { api } from '../lib/api'

export interface EmailStatementInput {
  clientId: string
  from?: string
  to?: string
  additionalRecipients?: string[]
  message?: string
  overdueOnly?: boolean
}

export const statementService = {
  emailStatement(input: EmailStatementInput): Promise<void> {
    return api.post<void>('/api/finance/client-statement/email', {
      clientId: input.clientId,
      from: input.from ?? null,
      to: input.to ?? null,
      additionalRecipients: input.additionalRecipients ?? null,
      message: input.message ?? null,
      overdueOnly: input.overdueOnly ?? false,
    })
  },

  async previewStatementPdf(
    clientId: string,
    from?: string,
    to?: string,
    overdueOnly?: boolean,
  ): Promise<Blob> {
    const { blob } = await api.download('/api/finance/client-statement/preview-pdf', {
      query: {
        clientId,
        from: from ?? undefined,
        to: to ?? undefined,
        overdueOnly: overdueOnly ?? false,
      },
    })
    return blob
  },
}
