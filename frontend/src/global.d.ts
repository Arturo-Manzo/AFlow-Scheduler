export {};

declare global {
  interface ASchedulerRuntimeConfig {
    backendUrl?: string;
    port?: number;
  }

  var __ASCHEDULER_RUNTIME_CONFIG__: ASchedulerRuntimeConfig | undefined;
}
