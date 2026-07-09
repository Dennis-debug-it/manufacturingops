declare module '@microsoft/signalr' {
  export enum LogLevel { Trace = 0, Debug = 1, Information = 2, Warning = 3, Error = 4, Critical = 5, None = 6 }
  export enum HttpTransportType { None = 0, WebSockets = 1, ServerSentEvents = 2, LongPolling = 4 }
  export enum HubConnectionState { Disconnected = 'Disconnected', Connecting = 'Connecting', Connected = 'Connected', Disconnecting = 'Disconnecting', Reconnecting = 'Reconnecting' }

  export interface RetryContext {
    previousRetryCount: number
    elapsedMilliseconds: number
    retryReason: Error
  }
  export interface IHttpConnectionOptions {
    accessTokenFactory?: () => string | Promise<string>
    transport?: HttpTransportType
    headers?: Record<string, string>
    withCredentials?: boolean
  }

  export class HubConnection {
    readonly state: HubConnectionState
    on(methodName: string, newMethod: (...args: unknown[]) => void): void
    off(methodName: string, method?: (...args: unknown[]) => void): void
    onreconnected(callback: (connectionId?: string) => void): void
    onreconnecting(callback: (error?: Error) => void): void
    onclose(callback: (error?: Error) => void): void
    start(): Promise<void>
    stop(): Promise<void>
    invoke<T = unknown>(methodName: string, ...args: unknown[]): Promise<T>
  }

  export class HubConnectionBuilder {
    withUrl(url: string, options?: IHttpConnectionOptions): this
    withAutomaticReconnect(options?: { nextRetryDelayInMilliseconds: (ctx: RetryContext) => number | null }): this
    configureLogging(logLevel: LogLevel): this
    build(): HubConnection
  }
}
